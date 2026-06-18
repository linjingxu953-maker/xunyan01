using DesktopMascot.Core.Tools;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 文件加密工具增强版 — PBKDF2密钥派生 + 随机IV + 文件夹加密 + 密钥文件 + 信息检测
/// </summary>
public class FileEncryptionTool : ITool
{
    private const int KeySize = 256;
    private const int BlockSize = 128;
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int IvSize = 16;
    private static readonly byte[] MagicHeader = "XMYE1"u8.ToArray(); // 文件头标识

    public string Name => "file_encryption";
    public string Description => "文件加密：AES-256 加密/解密、PBKDF2 密钥派生、文件夹批量加密、密钥文件、哈希验证。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["encrypt", "decrypt", "hash", "verify", "folder_encrypt", "folder_decrypt", "info"], "description": "操作类型" },
            "input_path": { "type": "string", "description": "输入文件/文件夹路径" },
            "output_path": { "type": "string", "description": "输出文件/文件夹路径（可选）" },
            "password": { "type": "string", "description": "加密密码" },
            "key_file": { "type": "string", "description": "密钥文件路径（可选，与密码二选一）" },
            "hash_type": { "type": "string", "enum": ["md5", "sha256", "sha512", "sha3_256"], "description": "哈希类型" },
            "iterations": { "type": "integer", "description": "PBKDF2 迭代次数（默认100000）" },
            "delete_source": { "type": "boolean", "description": "加密后删除源文件（默认false）" }
        },
        "required": ["action"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";

            return action switch
            {
                "encrypt" => await EncryptFileAsync(root, ct),
                "decrypt" => await DecryptFileAsync(root, ct),
                "hash" => await HashFileAsync(root, ct),
                "verify" => await VerifyHashAsync(root, ct),
                "folder_encrypt" => await FolderEncryptAsync(root, ct),
                "folder_decrypt" => await FolderDecryptAsync(root, ct),
                "info" => await GetInfoAsync(root, ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"加密操作失败：{ex.Message}");
        }
    }

    #region 单文件操作

    private async Task<ToolResult> EncryptFileAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequired(root, "input_path");
        var password = root.TryGetProperty("password", out var pEl) ? pEl.GetString() : null;
        var keyFile = root.TryGetProperty("key_file", out var kfEl) ? kfEl.GetString() : null;
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        var deleteSource = root.TryGetProperty("delete_source", out var dsEl) && dsEl.GetBoolean();
        var iterations = root.TryGetProperty("iterations", out var itEl) ? itEl.GetInt32() : Iterations;

        if (inputPath == null) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");
        if (password == null && keyFile == null) return Fail("需要 password 或 key_file 参数");

        outputPath ??= inputPath + ".encrypted";

        var keyMaterial = await ResolveKeyMaterialAsync(password, keyFile, ct);
        var fileContent = await File.ReadAllBytesAsync(inputPath, ct);
        var encrypted = EncryptAes256(fileContent, keyMaterial.Key, keyMaterial.Salt, iterations);

        // 写入带文件头的加密文件
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        await fs.WriteAsync(MagicHeader, ct);
        await fs.WriteAsync(encrypted, ct);

        if (deleteSource)
            File.Delete(inputPath);

        var sb = new StringBuilder();
        sb.AppendLine("文件加密完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"原始大小：{FormatSize(fileContent.Length)}");
        sb.AppendLine($"加密后大小：{FormatSize(encrypted.Length + MagicHeader.Length)}");
        sb.AppendLine($"算法：AES-256-CBC + PBKDF2（{iterations:N0} 次迭代）");
        if (deleteSource) sb.AppendLine("源文件已删除");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> DecryptFileAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequired(root, "input_path");
        var password = root.TryGetProperty("password", out var pEl) ? pEl.GetString() : null;
        var keyFile = root.TryGetProperty("key_file", out var kfEl) ? kfEl.GetString() : null;
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (inputPath == null) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");
        if (password == null && keyFile == null) return Fail("需要 password 或 key_file 参数");

        outputPath ??= Path.ChangeExtension(inputPath, null);
        if (outputPath == inputPath) outputPath += ".decrypted";

        var fileContent = await File.ReadAllBytesAsync(inputPath, ct);

        // 检查文件头
        if (fileContent.Length < MagicHeader.Length + SaltSize + IvSize + 32)
            return Fail("文件格式无效或已损坏");

        var hasHeader = fileContent[..MagicHeader.Length].SequenceEqual(MagicHeader);
        byte[] encryptedData;
        byte[] salt;
        int iterations;

        if (hasHeader)
        {
            // 新格式：magic + salt(16) + iterations(4) + iv(16) + ciphertext
            encryptedData = fileContent[MagicHeader.Length..];
            salt = encryptedData[..SaltSize];
            iterations = BitConverter.ToInt32(encryptedData[SaltSize..(SaltSize + 4)]);
            var iv = encryptedData[(SaltSize + 4)..(SaltSize + 4 + IvSize)];
            var ciphertext = encryptedData[(SaltSize + 4 + IvSize)..];

            var keyMaterial = await ResolveKeyMaterialAsync(password, keyFile, salt, ct);
            var decrypted = DecryptAes256WithIv(ciphertext, keyMaterial.Key, iv);
            await File.WriteAllBytesAsync(outputPath, decrypted, ct);
        }
        else
        {
            // 兼容旧格式（零IV）
            var keyMaterial = await ResolveKeyMaterialAsync(password, keyFile, ct);
            var decrypted = DecryptAes256Legacy(fileContent, keyMaterial.Key);
            await File.WriteAllBytesAsync(outputPath, decrypted, ct);
        }

        var outputInfo = new FileInfo(outputPath);
        var sb = new StringBuilder();
        sb.AppendLine("文件解密完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"加密大小：{FormatSize(fileContent.Length)}");
        sb.AppendLine($"解密后大小：{FormatSize(outputInfo.Length)}");
        if (hasHeader) sb.AppendLine("格式：新版（PBKDF2 + 随机 IV）");
        else sb.AppendLine("格式：旧版（兼容解密）");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> HashFileAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequired(root, "input_path");
        var hashType = root.TryGetProperty("hash_type", out var hEl) ? hEl.GetString() ?? "sha256" : "sha256";

        if (inputPath == null) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        await using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920);
        var hash = await ComputeHashStreamAsync(fs, hashType, ct);

        var sb = new StringBuilder();
        sb.AppendLine("文件哈希");
        sb.AppendLine($"文件：{inputPath}");
        sb.AppendLine($"大小：{FormatSize(new FileInfo(inputPath).Length)}");
        sb.AppendLine($"算法：{hashType.ToUpper()}");
        sb.AppendLine($"哈希值：{hash}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> VerifyHashAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequired(root, "input_path");
        var expectedHash = GetRequired(root, "hash");
        var hashType = root.TryGetProperty("hash_type", out var htEl) ? htEl.GetString() ?? "sha256" : "sha256";

        if (inputPath == null) return Fail("缺少 input_path 参数");
        if (expectedHash == null) return Fail("缺少 hash 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        await using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920);
        var actualHash = await ComputeHashStreamAsync(fs, hashType, ct);
        var isValid = string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine("哈希验证");
        sb.AppendLine($"文件：{inputPath}");
        sb.AppendLine($"算法：{hashType.ToUpper()}");
        sb.AppendLine($"期望值：{expectedHash}");
        sb.AppendLine($"实际值：{actualHash}");
        sb.AppendLine($"验证结果：{(isValid ? "通过" : "失败")}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> GetInfoAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequired(root, "input_path");
        if (inputPath == null) return Fail("缺少 input_path 参数");

        if (!File.Exists(inputPath))
            return Fail($"文件不存在：{inputPath}");

        var fileContent = await File.ReadAllBytesAsync(inputPath, ct);
        var hasHeader = fileContent.Length >= MagicHeader.Length && fileContent[..MagicHeader.Length].SequenceEqual(MagicHeader);

        var sb = new StringBuilder();
        sb.AppendLine("文件加密信息");
        sb.AppendLine($"文件：{inputPath}");
        sb.AppendLine($"大小：{FormatSize(fileContent.Length)}");
        sb.AppendLine($"已加密：{(hasHeader ? "是（新版格式）" : "否")}");

        if (hasHeader && fileContent.Length >= MagicHeader.Length + SaltSize + 4 + IvSize)
        {
            var iterations = BitConverter.ToInt32(fileContent[(MagicHeader.Length + SaltSize)..(MagicHeader.Length + SaltSize + 4)]);
            sb.AppendLine($"密钥派生：PBKDF2（{iterations:N0} 次迭代）");
            sb.AppendLine($"算法：AES-256-CBC");
            sb.AppendLine($"IV：随机生成");
        }

        // 检查是否为图片/文本等常见格式
        var ext = Path.GetExtension(inputPath).ToLower();
        var format = ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" => "图片",
            ".txt" or ".md" or ".json" or ".xml" or ".csv" => "文本",
            ".pdf" => "PDF",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "压缩包",
            ".encrypted" => "加密文件",
            _ => "未知"
        };
        sb.AppendLine($"推测格式：{format}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 文件夹操作

    private async Task<ToolResult> FolderEncryptAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequired(root, "input_path");
        var password = root.TryGetProperty("password", out var pEl) ? pEl.GetString() : null;
        var keyFile = root.TryGetProperty("key_file", out var kfEl) ? kfEl.GetString() : null;
        var deleteSource = root.TryGetProperty("delete_source", out var dsEl) && dsEl.GetBoolean();
        var iterations = root.TryGetProperty("iterations", out var itEl) ? itEl.GetInt32() : Iterations;

        if (inputPath == null) return Fail("缺少 input_path 参数");
        if (!Directory.Exists(inputPath)) return Fail($"目录不存在：{inputPath}");
        if (password == null && keyFile == null) return Fail("需要 password 或 key_file 参数");

        var keyMaterial = await ResolveKeyMaterialAsync(password, keyFile, ct);
        var files = Directory.GetFiles(inputPath, "*.*", SearchOption.AllDirectories);
        var encrypted = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            if (Path.GetExtension(file).ToLower() == ".encrypted")
            {
                skipped++;
                continue;
            }

            try
            {
                var outputPath = file + ".encrypted";
                var fileContent = await File.ReadAllBytesAsync(file, ct);
                var encryptedData = EncryptAes256(fileContent, keyMaterial.Key, keyMaterial.Salt, iterations);

                using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                await fs.WriteAsync(MagicHeader, ct);
                await fs.WriteAsync(encryptedData, ct);

                if (deleteSource)
                    File.Delete(file);

                encrypted++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("文件夹批量加密完成");
        sb.AppendLine($"目录：{inputPath}");
        sb.AppendLine($"加密：{encrypted} 个文件");
        sb.AppendLine($"跳过（已加密）：{skipped} 个");
        if (errors.Count > 0)
        {
            sb.AppendLine($"错误：{errors.Count} 个");
            foreach (var e in errors.Take(5))
                sb.AppendLine($"  - {e}");
        }
        if (deleteSource) sb.AppendLine("源文件已删除");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> FolderDecryptAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequired(root, "input_path");
        var password = root.TryGetProperty("password", out var pEl) ? pEl.GetString() : null;
        var keyFile = root.TryGetProperty("key_file", out var kfEl) ? kfEl.GetString() : null;
        var deleteSource = root.TryGetProperty("delete_source", out var dsEl) && dsEl.GetBoolean();

        if (inputPath == null) return Fail("缺少 input_path 参数");
        if (!Directory.Exists(inputPath)) return Fail($"目录不存在：{inputPath}");
        if (password == null && keyFile == null) return Fail("需要 password 或 key_file 参数");

        var files = Directory.GetFiles(inputPath, "*.encrypted", SearchOption.AllDirectories);
        var decrypted = 0;
        var errors = new List<string>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var outputPath = Path.ChangeExtension(file, null);
                if (outputPath == file) outputPath += ".decrypted";

                var tempRoot = new JsonElement();
                var args = $"{{\"action\":\"decrypt\",\"input_path\":\"{EscapeJson(file)}\",\"output_path\":\"{EscapeJson(outputPath)}\"}}";
                if (password != null) args = $"{{\"action\":\"decrypt\",\"input_path\":\"{EscapeJson(file)}\",\"output_path\":\"{EscapeJson(outputPath)}\",\"password\":\"{EscapeJson(password)}\"}}";

                var tempDoc = JsonDocument.Parse(args);
                var decryptResult = await DecryptFileAsync(tempDoc.RootElement, ct);

                if (decryptResult.Success && deleteSource)
                    File.Delete(file);

                if (decryptResult.Success) decrypted++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("文件夹批量解密完成");
        sb.AppendLine($"目录：{inputPath}");
        sb.AppendLine($"解密：{decrypted} 个文件");
        if (errors.Count > 0)
        {
            sb.AppendLine($"错误：{errors.Count} 个");
            foreach (var e in errors.Take(5))
                sb.AppendLine($"  - {e}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region 加密核心

    private static byte[] EncryptAes256(byte[] data, byte[] key, byte[] salt, int iterations)
    {
        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // 随机 IV
        RandomNumberGenerator.Fill(aes.IV);
        aes.Key = key;

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        // 写入 salt + iterations + iv
        ms.Write(salt, 0, salt.Length);
        ms.Write(BitConverter.GetBytes(iterations), 0, 4);
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
        }

        return ms.ToArray();
    }

    private static byte[] DecryptAes256WithIv(byte[] ciphertext, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(ciphertext);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var result = new MemoryStream();
        cs.CopyTo(result);
        return result.ToArray();
    }

    private static byte[] DecryptAes256Legacy(byte[] encryptedData, byte[] key)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = new byte[16];

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(encryptedData);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var result = new MemoryStream();
        cs.CopyTo(result);
        return result.ToArray();
    }

    #endregion

    #region 密钥派生

    private static async Task<(byte[] Key, byte[] Salt)> ResolveKeyMaterialAsync(string? password, string? keyFile, CancellationToken ct)
    {
        byte[] inputBytes;

        if (!string.IsNullOrEmpty(keyFile))
        {
            if (!File.Exists(keyFile))
                throw new FileNotFoundException($"密钥文件不存在：{keyFile}");
            inputBytes = await File.ReadAllBytesAsync(keyFile, ct);
        }
        else
        {
            inputBytes = Encoding.UTF8.GetBytes(password!);
        }

        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(inputBytes, salt, Iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(KeySize / 8);

        return (key, salt);
    }

    private static async Task<(byte[] Key, byte[] Salt)> ResolveKeyMaterialAsync(string? password, string? keyFile, byte[] existingSalt, CancellationToken ct)
    {
        byte[] inputBytes;

        if (!string.IsNullOrEmpty(keyFile))
        {
            if (!File.Exists(keyFile))
                throw new FileNotFoundException($"密钥文件不存在：{keyFile}");
            inputBytes = await File.ReadAllBytesAsync(keyFile, ct);
        }
        else
        {
            inputBytes = Encoding.UTF8.GetBytes(password!);
        }

        using var pbkdf2 = new Rfc2898DeriveBytes(inputBytes, existingSalt, Iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(KeySize / 8);

        return (key, existingSalt);
    }

    #endregion

    #region 哈希

    private static async Task<string> ComputeHashStreamAsync(Stream stream, string hashType, CancellationToken ct)
    {
        var hash = hashType.ToLower() switch
        {
            "md5" => await MD5.HashDataAsync(stream, ct),
            "sha256" => await SHA256.HashDataAsync(stream, ct),
            "sha512" => await SHA512.HashDataAsync(stream, ct),
            "sha3_256" => await SHA3_256.HashDataAsync(stream, ct),
            _ => await SHA256.HashDataAsync(stream, ct)
        };
        return Convert.ToHexString(hash);
    }

    #endregion

    #region Helpers

    private static string? GetRequired(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) ? el.GetString() : null;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:F1} MB",
        _ => $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} GB"
    };

    private static string EscapeJson(string text)
    {
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static ToolResult Fail(string error) => new() { Name = "file_encryption", Success = false, Error = error };

    #endregion
}

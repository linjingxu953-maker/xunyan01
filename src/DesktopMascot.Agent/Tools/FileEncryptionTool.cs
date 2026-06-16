using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 文件加密工具 - AES-256 加密/解密
/// </summary>
public class FileEncryptionTool : ITool
{
    public string Name => "file_encryption";
    public string Description => "文件加密：AES-256 加密/解密文件，支持密码保护。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["encrypt", "decrypt", "hash", "verify"], "description": "操作类型" },
            "input_path": { "type": "string", "description": "输入文件路径" },
            "output_path": { "type": "string", "description": "输出文件路径（可选）" },
            "password": { "type": "string", "description": "加密密码" },
            "hash_type": { "type": "string", "enum": ["md5", "sha256", "sha512"], "description": "哈希类型" }
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
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"加密操作失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> EncryptFileAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = root.TryGetProperty("input_path", out var iEl) ? iEl.GetString() ?? "" : "";
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        var password = root.TryGetProperty("password", out var pEl) ? pEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");
        if (string.IsNullOrEmpty(password)) return Fail("缺少 password 参数");

        outputPath ??= inputPath + ".encrypted";

        var fileContent = await File.ReadAllBytesAsync(inputPath, ct);
        var encrypted = EncryptAes256(fileContent, password);
        await File.WriteAllBytesAsync(outputPath, encrypted, ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("文件加密完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"原始大小：{fileContent.Length / 1024.0:F1} KB");
        sb.AppendLine($"加密后大小：{encrypted.Length / 1024.0:F1} KB");
        sb.AppendLine($"算法：AES-256-CBC");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> DecryptFileAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = root.TryGetProperty("input_path", out var iEl) ? iEl.GetString() ?? "" : "";
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        var password = root.TryGetProperty("password", out var pEl) ? pEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");
        if (string.IsNullOrEmpty(password)) return Fail("缺少 password 参数");

        outputPath ??= Path.ChangeExtension(inputPath, null);

        var encryptedContent = await File.ReadAllBytesAsync(inputPath, ct);
        var decrypted = DecryptAes256(encryptedContent, password);
        await File.WriteAllBytesAsync(outputPath, decrypted, ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("文件解密完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"加密大小：{encryptedContent.Length / 1024.0:F1} KB");
        sb.AppendLine($"解密后大小：{decrypted.Length / 1024.0:F1} KB");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> HashFileAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = root.TryGetProperty("input_path", out var iEl) ? iEl.GetString() ?? "" : "";
        var hashType = root.TryGetProperty("hash_type", out var hEl) ? hEl.GetString() ?? "sha256" : "sha256";

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        var fileContent = await File.ReadAllBytesAsync(inputPath, ct);
        var hash = ComputeHash(fileContent, hashType);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("文件哈希");
        sb.AppendLine($"文件：{inputPath}");
        sb.AppendLine($"算法：{hashType.ToUpper()}");
        sb.AppendLine($"哈希值：{hash}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> VerifyHashAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = root.TryGetProperty("input_path", out var iEl) ? iEl.GetString() ?? "" : "";
        var expectedHash = root.TryGetProperty("hash", out var hEl) ? hEl.GetString() ?? "" : "";
        var hashType = root.TryGetProperty("hash_type", out var htEl) ? htEl.GetString() ?? "sha256" : "sha256";

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
        if (string.IsNullOrEmpty(expectedHash)) return Fail("缺少 hash 参数");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        var fileContent = await File.ReadAllBytesAsync(inputPath, ct);
        var actualHash = ComputeHash(fileContent, hashType);
        var isValid = string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("哈希验证");
        sb.AppendLine($"文件：{inputPath}");
        sb.AppendLine($"算法：{hashType.ToUpper()}");
        sb.AppendLine($"期望值：{expectedHash}");
        sb.AppendLine($"实际值：{actualHash}");
        sb.AppendLine($"验证结果：{(isValid ? "通过" : "失败")}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private static byte[] EncryptAes256(byte[] data, string password)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // 从密码生成密钥和IV
        using var sha256 = SHA256.Create();
        aes.Key = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        aes.IV = new byte[16]; // 使用零 IV（简化实现）

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
        }
        return ms.ToArray();
    }

    private static byte[] DecryptAes256(byte[] encryptedData, string password)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var sha256 = SHA256.Create();
        aes.Key = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        aes.IV = new byte[16];

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(encryptedData);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var result = new MemoryStream();
        cs.CopyTo(result);
        return result.ToArray();
    }

    private static string ComputeHash(byte[] data, string hashType)
    {
        return hashType.ToLower() switch
        {
            "md5" => Convert.ToHexString(MD5.HashData(data)),
            "sha256" => Convert.ToHexString(SHA256.HashData(data)),
            "sha512" => Convert.ToHexString(SHA512.HashData(data)),
            _ => Convert.ToHexString(SHA256.HashData(data))
        };
    }

    private static ToolResult Fail(string error) => new() { Name = "file_encryption", Success = false, Error = error };
}

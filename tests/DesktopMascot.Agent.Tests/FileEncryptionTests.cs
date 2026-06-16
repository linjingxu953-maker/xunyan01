using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class FileEncryptionTests
{
    [Fact]
    public async Task Encrypt_ShouldCreateEncryptedFile()
    {
        var tool = new FileEncryptionTool();
        var tempDir = Path.GetTempPath();
        var inputFile = Path.Combine(tempDir, $"encrypt_test_{Guid.NewGuid():N}.txt");
        var outputFile = Path.Combine(tempDir, $"encrypt_test_{Guid.NewGuid():N}.encrypted");

        try
        {
            await File.WriteAllTextAsync(inputFile, "Hello, World! This is a test file for encryption.");

            var args = JsonSerializer.Serialize(new
            {
                action = "encrypt",
                input_path = inputFile,
                output_path = outputFile,
                password = "TestPassword123!"
            });

            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("加密完成", result.Content);
            Assert.True(File.Exists(outputFile));
            Assert.True(new FileInfo(outputFile).Length > 0);
        }
        finally
        {
            if (File.Exists(inputFile)) File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task Decrypt_ShouldRestoreOriginal()
    {
        var tool = new FileEncryptionTool();
        var tempDir = Path.GetTempPath();
        var inputFile = Path.Combine(tempDir, $"decrypt_test_{Guid.NewGuid():N}.txt");
        var encryptedFile = Path.Combine(tempDir, $"decrypt_test_{Guid.NewGuid():N}.encrypted");
        var decryptedFile = Path.Combine(tempDir, $"decrypt_test_{Guid.NewGuid():N}.decrypted");

        try
        {
            var originalContent = "Hello, World! This is a test file.";
            await File.WriteAllTextAsync(inputFile, originalContent);

            // 先加密
            var encryptArgs = JsonSerializer.Serialize(new
            {
                action = "encrypt",
                input_path = inputFile,
                output_path = encryptedFile,
                password = "TestPassword123!"
            });
            await tool.ExecuteAsync(encryptArgs);

            // 再解密
            var decryptArgs = JsonSerializer.Serialize(new
            {
                action = "decrypt",
                input_path = encryptedFile,
                output_path = decryptedFile,
                password = "TestPassword123!"
            });
            var result = await tool.ExecuteAsync(decryptArgs);

            Assert.True(result.Success);
            Assert.Contains("解密完成", result.Content);
            Assert.Equal(originalContent, await File.ReadAllTextAsync(decryptedFile));
        }
        finally
        {
            if (File.Exists(inputFile)) File.Delete(inputFile);
            if (File.Exists(encryptedFile)) File.Delete(encryptedFile);
            if (File.Exists(decryptedFile)) File.Delete(decryptedFile);
        }
    }

    [Fact]
    public async Task Decrypt_WrongPassword_ShouldFail()
    {
        var tool = new FileEncryptionTool();
        var tempDir = Path.GetTempPath();
        var inputFile = Path.Combine(tempDir, $"wrong_pwd_{Guid.NewGuid():N}.txt");
        var encryptedFile = Path.Combine(tempDir, $"wrong_pwd_{Guid.NewGuid():N}.encrypted");

        try
        {
            await File.WriteAllTextAsync(inputFile, "Test content");

            // 先加密
            var encryptArgs = JsonSerializer.Serialize(new
            {
                action = "encrypt",
                input_path = inputFile,
                output_path = encryptedFile,
                password = "CorrectPassword"
            });
            await tool.ExecuteAsync(encryptArgs);

            // 用错误密码解密
            var decryptArgs = JsonSerializer.Serialize(new
            {
                action = "decrypt",
                input_path = encryptedFile,
                password = "WrongPassword"
            });
            var result = await tool.ExecuteAsync(decryptArgs);

            Assert.False(result.Success);
        }
        finally
        {
            if (File.Exists(inputFile)) File.Delete(inputFile);
            if (File.Exists(encryptedFile)) File.Delete(encryptedFile);
        }
    }

    [Fact]
    public async Task Hash_ShouldComputeHash()
    {
        var tool = new FileEncryptionTool();
        var tempDir = Path.GetTempPath();
        var testFile = Path.Combine(tempDir, $"hash_test_{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllTextAsync(testFile, "Hello, World!");

            var args = JsonSerializer.Serialize(new
            {
                action = "hash",
                input_path = testFile,
                hash_type = "sha256"
            });

            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("哈希值", result.Content);
            Assert.Contains("SHA256", result.Content);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public async Task Verify_ShouldValidateHash()
    {
        var tool = new FileEncryptionTool();
        var tempDir = Path.GetTempPath();
        var testFile = Path.Combine(tempDir, $"verify_test_{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllTextAsync(testFile, "Hello, World!");

            // 先计算哈希
            var hashArgs = JsonSerializer.Serialize(new
            {
                action = "hash",
                input_path = testFile,
                hash_type = "sha256"
            });
            var hashResult = await tool.ExecuteAsync(hashArgs);
            var hash = hashResult.Content.Split("哈希值：")[1].Split("\n")[0].Trim();

            // 验证哈希
            var verifyArgs = JsonSerializer.Serialize(new
            {
                action = "verify",
                input_path = testFile,
                hash = hash,
                hash_type = "sha256"
            });
            var result = await tool.ExecuteAsync(verifyArgs);

            Assert.True(result.Success);
            Assert.Contains("通过", result.Content);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public void FileEncryptionTool_Metadata_ShouldBeCorrect()
    {
        var tool = new FileEncryptionTool();
        Assert.Equal("file_encryption", tool.Name);
        Assert.Contains("encrypt", tool.ParametersSchema);
        Assert.Contains("decrypt", tool.ParametersSchema);
    }
}

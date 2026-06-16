using System.Text.Json;
using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class SecurityScanToolTests
{
    [Fact]
    public async Task ScanVulnerabilities_ShouldFindIssues()
    {
        var tempFile = Path.GetTempPath();
        var testFile = Path.Combine(tempFile, $"test_{Guid.NewGuid():N}.cs");

        try
        {
            await File.WriteAllTextAsync(testFile, "var x = eval(\"code\");\nvar y = document.write(\"test\");");

            var tool = new SecurityScanTool();
            var args = JsonSerializer.Serialize(new { action = "vulnerabilities", file_path = testFile });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("潜在问题", result.Content);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public async Task ScanSecrets_ShouldFindSecrets()
    {
        var tempFile = Path.GetTempPath();
        var testFile = Path.Combine(tempFile, $"test_{Guid.NewGuid():N}.cs");

        try
        {
            await File.WriteAllTextAsync(testFile, "var apiKey = \"sk-1234567890abcdef\";");

            var tool = new SecurityScanTool();
            var args = JsonSerializer.Serialize(new { action = "secrets", file_path = testFile });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("敏感信息", result.Content);
            Assert.Contains("***", result.Content); // 应该被掩码
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public async Task ScanPermissions_ShouldReturnInfo()
    {
        var tempFile = Path.GetTempPath();
        var testFile = Path.Combine(tempFile, $"test_{Guid.NewGuid():N}.cs");

        try
        {
            await File.WriteAllTextAsync(testFile, "test");

            var tool = new SecurityScanTool();
            var args = JsonSerializer.Serialize(new { action = "permissions", file_path = testFile });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("权限检查", result.Content);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public async Task ScanVulnerabilities_NoIssues_ShouldReport()
    {
        var tempFile = Path.GetTempPath();
        var testFile = Path.Combine(tempFile, $"test_{Guid.NewGuid():N}.cs");

        try
        {
            await File.WriteAllTextAsync(testFile, "var x = 1;\nvar y = 2;");

            var tool = new SecurityScanTool();
            var args = JsonSerializer.Serialize(new { action = "vulnerabilities", file_path = testFile });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("0 个潜在问题", result.Content);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public async Task FullScan_ShouldScanDirectory()
    {
        var tempDir = Path.GetTempPath();
        var testDir = Path.Combine(tempDir, $"security_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(testDir, "test.cs"), "eval(\"code\");");

            var tool = new SecurityScanTool();
            var args = JsonSerializer.Serialize(new { action = "full", directory = testDir });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("完整安全扫描", result.Content);
        }
        finally
        {
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public void SecurityScanTool_Metadata_ShouldBeCorrect()
    {
        var tool = new SecurityScanTool();
        Assert.Equal("security_scan", tool.Name);
        Assert.Contains("vulnerabilities", tool.ParametersSchema);
        Assert.Contains("secrets", tool.ParametersSchema);
    }
}

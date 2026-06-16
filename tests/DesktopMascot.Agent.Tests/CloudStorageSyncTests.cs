using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class CloudStorageSyncTests
{
    [Fact]
    public async Task Configure_LocalSync_ShouldWork()
    {
        var tool = new CloudStorageSyncTool();
        var tempDir = Path.Combine(Path.GetTempPath(), $"sync_{Guid.NewGuid():N}");

        try
        {
            var args = JsonSerializer.Serialize(new
            {
                action = "configure",
                provider = "local",
                config = new { sync_directory = tempDir }
            });

            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("已配置", result.Content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task List_BeforeConfigure_ShouldFail()
    {
        var tool = new CloudStorageSyncTool();
        var args = JsonSerializer.Serialize(new { action = "list", folder = "/" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("请先配置", result.Error);
    }

    [Fact]
    public async Task Upload_BeforeConfigure_ShouldFail()
    {
        var tool = new CloudStorageSyncTool();
        var args = JsonSerializer.Serialize(new { action = "upload", local_path = "test.txt", remote_path = "test.txt" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("请先配置", result.Error);
    }

    [Fact]
    public async Task Status_BeforeConfigure_ShouldFail()
    {
        var tool = new CloudStorageSyncTool();
        var args = JsonSerializer.Serialize(new { action = "status" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("请先配置", result.Error);
    }

    [Fact]
    public async Task Upload_And_List_ShouldWork()
    {
        var tool = new CloudStorageSyncTool();
        var tempDir = Path.Combine(Path.GetTempPath(), $"sync_test_{Guid.NewGuid():N}");
        var testFile = Path.Combine(tempDir, "test_upload.txt");

        try
        {
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(testFile, "test content");

            // 配置
            await tool.ExecuteAsync(JsonSerializer.Serialize(new
            {
                action = "configure",
                provider = "local",
                config = new { sync_directory = tempDir }
            }));

            // 上传
            var uploadResult = await tool.ExecuteAsync(JsonSerializer.Serialize(new
            {
                action = "upload",
                local_path = testFile,
                remote_path = "uploaded/test.txt"
            }));
            Assert.True(uploadResult.Success);

            // 列出
            var listResult = await tool.ExecuteAsync(JsonSerializer.Serialize(new
            {
                action = "list",
                folder = "uploaded"
            }));
            Assert.True(listResult.Success);
            Assert.Contains("test.txt", listResult.Content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Status_ShouldReturnInfo()
    {
        var tool = new CloudStorageSyncTool();
        var tempDir = Path.Combine(Path.GetTempPath(), $"sync_status_{Guid.NewGuid():N}");

        try
        {
            await tool.ExecuteAsync(JsonSerializer.Serialize(new
            {
                action = "configure",
                provider = "local",
                config = new { sync_directory = tempDir }
            }));

            var args = JsonSerializer.Serialize(new { action = "status" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("存储状态", result.Content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CloudStorageSyncTool_Metadata_ShouldBeCorrect()
    {
        var tool = new CloudStorageSyncTool();
        Assert.Equal("cloud_sync", tool.Name);
        Assert.Contains("configure", tool.ParametersSchema);
        Assert.Contains("upload", tool.ParametersSchema);
    }
}

using System.Text.Json;
using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class FileOperationToolsTests
{
    [Fact]
    public async Task FileCompareTool_SameFiles_ShouldReportNoDiff()
    {
        var tempDir = Path.GetTempPath();
        var file1 = Path.Combine(tempDir, $"compare1_{Guid.NewGuid():N}.txt");
        var file2 = Path.Combine(tempDir, $"compare2_{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllTextAsync(file1, "Hello\nWorld");
            await File.WriteAllTextAsync(file2, "Hello\nWorld");

            var tool = new FileCompareTool();
            var args = JsonSerializer.Serialize(new { file1, file2 });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("完全相同", result.Content);
        }
        finally
        {
            if (File.Exists(file1)) File.Delete(file1);
            if (File.Exists(file2)) File.Delete(file2);
        }
    }

    [Fact]
    public async Task FileCompareTool_DifferentFiles_ShouldReportDiffs()
    {
        var tempDir = Path.GetTempPath();
        var file1 = Path.Combine(tempDir, $"compare_diff1_{Guid.NewGuid():N}.txt");
        var file2 = Path.Combine(tempDir, $"compare_diff2_{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllTextAsync(file1, "Hello\nWorld");
            await File.WriteAllTextAsync(file2, "Hello\nUniverse");

            var tool = new FileCompareTool();
            var args = JsonSerializer.Serialize(new { file1, file2 });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("差异", result.Content);
            Assert.Contains("World", result.Content);
            Assert.Contains("Universe", result.Content);
        }
        finally
        {
            if (File.Exists(file1)) File.Delete(file1);
            if (File.Exists(file2)) File.Delete(file2);
        }
    }

    [Fact]
    public async Task FileCompareTool_MissingFile_ShouldFail()
    {
        var tool = new FileCompareTool();
        var args = JsonSerializer.Serialize(new { file1 = "nonexistent1.txt", file2 = "nonexistent2.txt" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("不存在", result.Error);
    }

    [Fact]
    public async Task BatchFileProcessorTool_List_ShouldListFiles()
    {
        var tempDir = Path.GetTempPath();
        var batchDir = Path.Combine(tempDir, $"batch_{Guid.NewGuid():N}");
        Directory.CreateDirectory(batchDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(batchDir, "test1.txt"), "a");
            await File.WriteAllTextAsync(Path.Combine(batchDir, "test2.txt"), "b");
            await File.WriteAllTextAsync(Path.Combine(batchDir, "other.js"), "c");

            var tool = new BatchFileProcessorTool();
            var args = JsonSerializer.Serialize(new { directory = batchDir, action = "list", pattern = "*.txt" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("test1.txt", result.Content);
            Assert.Contains("test2.txt", result.Content);
            Assert.DoesNotContain("other.js", result.Content);
        }
        finally
        {
            if (Directory.Exists(batchDir)) Directory.Delete(batchDir, true);
        }
    }

    [Fact]
    public async Task BatchFileProcessorTool_Delete_ShouldDeleteFiles()
    {
        var tempDir = Path.GetTempPath();
        var batchDir = Path.Combine(tempDir, $"batch_del_{Guid.NewGuid():N}");
        Directory.CreateDirectory(batchDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(batchDir, "delete_me.txt"), "x");

            var tool = new BatchFileProcessorTool();
            var args = JsonSerializer.Serialize(new { directory = batchDir, action = "delete", pattern = "*.txt" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.False(File.Exists(Path.Combine(batchDir, "delete_me.txt")));
        }
        finally
        {
            if (Directory.Exists(batchDir)) Directory.Delete(batchDir, true);
        }
    }

    [Fact]
    public async Task FileVersionTool_Snapshot_ShouldCreateVersion()
    {
        var tempDir = Path.GetTempPath();
        var testFile = Path.Combine(tempDir, $"version_test_{Guid.NewGuid():N}.txt");
        var versionDir = Path.Combine(tempDir, $"versions_{Guid.NewGuid():N}");

        try
        {
            await File.WriteAllTextAsync(testFile, "initial content");

            var tool = new FileVersionTool(versionDir);
            var args = JsonSerializer.Serialize(new { action = "snapshot", file_path = testFile, description = "初始版本" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("已创建快照", result.Content);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
            if (Directory.Exists(versionDir)) Directory.Delete(versionDir, true);
        }
    }

    [Fact]
    public async Task FileVersionTool_Restore_ShouldRestoreVersion()
    {
        var tempDir = Path.GetTempPath();
        var testFile = Path.Combine(tempDir, $"version_restore_{Guid.NewGuid():N}.txt");
        var versionDir = Path.Combine(tempDir, $"versions_restore_{Guid.NewGuid():N}");

        try
        {
            await File.WriteAllTextAsync(testFile, "original content");
            var tool = new FileVersionTool(versionDir);

            // 创建快照
            var snapshotArgs = JsonSerializer.Serialize(new { action = "snapshot", file_path = testFile, description = "v1" });
            await tool.ExecuteAsync(snapshotArgs);

            // 修改文件
            await File.WriteAllTextAsync(testFile, "modified content");

            // 查找版本文件（在子目录中）
            var subDir = Path.Combine(versionDir, Path.GetFileNameWithoutExtension(testFile));
            var versions = Directory.GetFiles(subDir, "*.meta.json");
            var metaJson = await File.ReadAllTextAsync(versions.First());
            var meta = JsonSerializer.Deserialize<JsonElement>(metaJson);
            var versionId = meta.GetProperty("VersionId").GetString();

            var restoreArgs = JsonSerializer.Serialize(new { action = "restore", file_path = testFile, version_id = versionId });
            var result = await tool.ExecuteAsync(restoreArgs);

            Assert.True(result.Success);
            Assert.Equal("original content", await File.ReadAllTextAsync(testFile));
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
            if (Directory.Exists(versionDir)) Directory.Delete(versionDir, true);
        }
    }

    [Fact]
    public void FileOperationTools_Metadata_ShouldBeCorrect()
    {
        var compareTool = new FileCompareTool();
        Assert.Equal("file_compare", compareTool.Name);

        var batchTool = new BatchFileProcessorTool();
        Assert.Equal("batch_file_process", batchTool.Name);

        var versionTool = new FileVersionTool();
        Assert.Equal("file_version", versionTool.Name);
    }
}

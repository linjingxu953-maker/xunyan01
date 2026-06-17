using System.Data.SQLite;
using System.Text.Json;
using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class DatabaseToolTests
{
    private string _testDbPath = "";

    private string GetTestDbPath()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        return _testDbPath;
    }

    private void Cleanup()
    {
        if (!string.IsNullOrEmpty(_testDbPath) && File.Exists(_testDbPath))
            File.Delete(_testDbPath);
    }

    [Fact]
    public async Task Connect_ShouldConnectToDatabase()
    {
        var tool = new DatabaseTool();
        var dbPath = GetTestDbPath();

        try
        {
            // 创建测试数据库
            SQLiteConnection.CreateFile(dbPath);

            var args = JsonSerializer.Serialize(new { action = "connect", db_path = dbPath });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("已连接", result.Content);
        }
        finally
        {
            Cleanup();
        }
    }

    [Fact]
    public async Task Connect_MissingPath_ShouldFail()
    {
        var tool = new DatabaseTool();
        var args = JsonSerializer.Serialize(new { action = "connect" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("缺少 db_path", result.Error);
    }

    [Fact]
    public async Task Query_BeforeConnect_ShouldFail()
    {
        var tool = new DatabaseTool();
        var args = JsonSerializer.Serialize(new { action = "query", sql = "SELECT 1" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("请先连接", result.Error);
    }

    [Fact]
    public async Task Tables_BeforeConnect_ShouldFail()
    {
        var tool = new DatabaseTool();
        var args = JsonSerializer.Serialize(new { action = "tables" });
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("请先连接", result.Error);
    }

    [Fact]
    public async Task Query_WithConnection_ShouldWork()
    {
        var tool = new DatabaseTool();
        var dbPath = GetTestDbPath();

        try
        {
            SQLiteConnection.CreateFile(dbPath);
            await tool.ExecuteAsync(JsonSerializer.Serialize(new { action = "connect", db_path = dbPath }));

            var args = JsonSerializer.Serialize(new { action = "query", sql = "SELECT 1 as test" });
            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("test", result.Content);
        }
        finally
        {
            Cleanup();
        }
    }

    [Fact]
    public void DatabaseTool_Metadata_ShouldBeCorrect()
    {
        var tool = new DatabaseTool();
        Assert.Equal("database", tool.Name);
        Assert.Contains("query", tool.ParametersSchema);
        Assert.Contains("tables", tool.ParametersSchema);
    }
}

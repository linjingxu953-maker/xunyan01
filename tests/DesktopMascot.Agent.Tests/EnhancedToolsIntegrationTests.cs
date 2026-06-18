using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

/// <summary>
/// 增强工具集成测试 — 覆盖 P1-P3 新增特性
/// </summary>
public class EnhancedToolsIntegrationTests
{
    #region SecurityScan 增强

    [Fact]
    public async Task SecurityScan_Dependencies_ShouldDetect()
    {
        var tool = new SecurityScanTool();
        var dir = Path.Combine(Path.GetTempPath(), $"scan_dep_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            // 创建 package.json 含危险包
            File.WriteAllText(Path.Combine(dir, "package.json"), """
            {"dependencies": {"event-stream": "1.0.0", "express": "4.18.0"}}
            """);

            var args = $"{{\"action\":\"dependencies\",\"directory\":\"{EscapePath(dir)}\"}}";
            var result = await tool.ExecuteAsync(args);
            Assert.True(result.Success);
            Assert.Contains("event-stream", result.Content);
        }
        finally { CleanupDir(dir); }
    }

    [Fact]
    public async Task SecurityScan_ConfigAudit_ShouldDetectDebugMode()
    {
        var tool = new SecurityScanTool();
        var dir = Path.Combine(Path.GetTempPath(), $"scan_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            File.WriteAllText(Path.Combine(dir, "appsettings.json"), """
            {"Debug": true, "ConnectionStrings": {"Default": "Server=localhost"}}
            """);

            var args = $"{{\"action\":\"config_audit\",\"directory\":\"{EscapePath(dir)}\"}}";
            var result = await tool.ExecuteAsync(args);
            Assert.True(result.Success);
            Assert.Contains("配置安全审计", result.Content);
        }
        finally { CleanupDir(dir); }
    }

    [Fact]
    public async Task SecurityScan_SeverityFilter_ShouldFilter()
    {
        var tool = new SecurityScanTool();
        var tempFile = Path.Combine(Path.GetTempPath(), $"scan_sev_{Guid.NewGuid():N}.cs");
        File.WriteAllText(tempFile, "var x = new Random();\neval(\"test\");\n");

        try
        {
            var lowArgs = $"{{\"action\":\"vulnerabilities\",\"file_path\":\"{EscapePath(tempFile)}\",\"severity\":\"low\"}}";
            var lowResult = await tool.ExecuteAsync(lowArgs);
            Assert.True(lowResult.Success);

            var highArgs = $"{{\"action\":\"vulnerabilities\",\"file_path\":\"{EscapePath(tempFile)}\",\"severity\":\"critical\"}}";
            var highResult = await tool.ExecuteAsync(highArgs);
            Assert.True(highResult.Success);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    #endregion

    #region DatabaseTool 增强

    [Fact]
    public async Task Database_BatchInsert_ShouldInsertMultiple()
    {
        var tool = new DatabaseTool();
        var dbPath = Path.Combine(Path.GetTempPath(), $"db_batch_{Guid.NewGuid():N}.db");

        try
        {
            tool.ExecuteAsync($"{{\"action\":\"connect\",\"db_path\":\"{EscapePath(dbPath)}\"}}").Wait();
            await tool.ExecuteAsync("{\"action\":\"create_table\",\"table\":\"test\",\"columns\":\"id INTEGER PRIMARY KEY, name TEXT, value INTEGER\"}");
            var data = "[{\"name\":\"a\",\"value\":1},{\"name\":\"b\",\"value\":2},{\"name\":\"c\",\"value\":3}]";
            var result = await tool.ExecuteAsync($"{{\"action\":\"batch_insert\",\"table\":\"test\",\"data\":\"{EscapeJsonString(data)}\"}}");
            Assert.True(result.Success);
            Assert.Contains("3", result.Content);

            var queryResult = await tool.ExecuteAsync("{\"action\":\"query\",\"sql\":\"SELECT COUNT(*) FROM test\"}");
            Assert.Contains("3", queryResult.Content);
        }
        finally { if (File.Exists(dbPath)) File.Delete(dbPath); }
    }

    [Fact]
    public async Task Database_ExportImport_ShouldRoundtrip()
    {
        var tool = new DatabaseTool();
        var dbPath = Path.Combine(Path.GetTempPath(), $"db_export_{Guid.NewGuid():N}.db");
        var exportPath = Path.Combine(Path.GetTempPath(), $"db_export_{Guid.NewGuid():N}.json");

        try
        {
            tool.ExecuteAsync($"{{\"action\":\"connect\",\"db_path\":\"{EscapePath(dbPath)}\"}}").Wait();
            await tool.ExecuteAsync("{\"action\":\"create_table\",\"table\":\"users\",\"columns\":\"id INTEGER PRIMARY KEY, name TEXT\"}");
            await tool.ExecuteAsync("{\"action\":\"insert\",\"table\":\"users\",\"data\":\"[{\\\"name\\\":\\\"Alice\\\"},{\\\"name\\\":\\\"Bob\\\"}]\"}");

            var exportResult = await tool.ExecuteAsync($"{{\"action\":\"export\",\"table\":\"users\",\"export_path\":\"{EscapePath(exportPath)}\",\"export_format\":\"json\"}}");
            Assert.True(exportResult.Success);

            // 新数据库导入
            var dbPath2 = Path.Combine(Path.GetTempPath(), $"db_import_{Guid.NewGuid():N}.db");
            var tool2 = new DatabaseTool();
            tool2.ExecuteAsync($"{{\"action\":\"connect\",\"db_path\":\"{EscapePath(dbPath2)}\"}}").Wait();
            await tool2.ExecuteAsync("{\"action\":\"create_table\",\"table\":\"users\",\"columns\":\"id INTEGER PRIMARY KEY, name TEXT\"}");

            var importResult = await tool2.ExecuteAsync($"{{\"action\":\"import\",\"table\":\"users\",\"export_path\":\"{EscapePath(exportPath)}\"}}");
            Assert.True(importResult.Success);
            Assert.Contains("2", importResult.Content);

            File.Delete(dbPath2);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
            if (File.Exists(exportPath)) File.Delete(exportPath);
        }
    }

    [Fact]
    public async Task Database_CreateIndex_ShouldWork()
    {
        var tool = new DatabaseTool();
        var dbPath = Path.Combine(Path.GetTempPath(), $"db_idx_{Guid.NewGuid():N}.db");

        try
        {
            tool.ExecuteAsync($"{{\"action\":\"connect\",\"db_path\":\"{EscapePath(dbPath)}\"}}").Wait();
            await tool.ExecuteAsync("{\"action\":\"create_table\",\"table\":\"items\",\"columns\":\"id INTEGER PRIMARY KEY, name TEXT\"}");

            var result = await tool.ExecuteAsync("{\"action\":\"create_index\",\"table\":\"items\",\"index_name\":\"idx_name\",\"index_columns\":\"name\"}");
            Assert.True(result.Success);

            var listResult = await tool.ExecuteAsync("{\"action\":\"index_list\"}");
            Assert.Contains("idx_name", listResult.Content);
        }
        finally { if (File.Exists(dbPath)) File.Delete(dbPath); }
    }

    [Fact]
    public async Task Database_Backup_ShouldCreateFile()
    {
        var tool = new DatabaseTool();
        var dbPath = Path.Combine(Path.GetTempPath(), $"db_bak_{Guid.NewGuid():N}.db");
        var backupPath = Path.Combine(Path.GetTempPath(), $"db_bak_{Guid.NewGuid():N}_backup.db");

        try
        {
            tool.ExecuteAsync($"{{\"action\":\"connect\",\"db_path\":\"{EscapePath(dbPath)}\"}}").Wait();
            await tool.ExecuteAsync("{\"action\":\"create_table\",\"table\":\"test\",\"columns\":\"id INTEGER PRIMARY KEY\"}");

            var result = await tool.ExecuteAsync($"{{\"action\":\"backup\",\"backup_path\":\"{EscapePath(backupPath)}\"}}");
            Assert.True(result.Success);
            Assert.True(File.Exists(backupPath));
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
    }

    #endregion

    #region CodeAnalysis 增强

    [Fact]
    public async Task CodeAnalysis_Smells_ShouldDetectIssues()
    {
        var tool = new CodeAnalysisTool();
        var tempFile = Path.Combine(Path.GetTempPath(), $"code_smell_{Guid.NewGuid():N}.cs");
        // 创建含代码异味的文件
        var longMethod = "public void Method()\n{\n" + string.Join("\n", Enumerable.Range(0, 50).Select(i => $"    var x{i} = {i};")) + "\n}";
        File.WriteAllText(tempFile, longMethod);

        try
        {
            var result = await tool.ExecuteAsync($"{{\"action\":\"smells\",\"file_path\":\"{EscapePath(tempFile)}\"}}");
            Assert.True(result.Success);
            Assert.Contains("代码异味分析", result.Content);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact]
    public async Task CodeAnalysis_Methods_ShouldListMethods()
    {
        var tool = new CodeAnalysisTool();
        var tempFile = Path.Combine(Path.GetTempPath(), $"code_meth_{Guid.NewGuid():N}.cs");
        File.WriteAllText(tempFile, """
        public class Test
        {
            public void Hello()
            {
                System.Console.WriteLine("hi");
            }
            public int Add(int a, int b)
            {
                return a + b;
            }
        }
        """);

        try
        {
            var result = await tool.ExecuteAsync($"{{\"action\":\"methods\",\"file_path\":\"{EscapePath(tempFile)}\"}}");
            Assert.True(result.Success);
            Assert.Contains("方法分析", result.Content);
            Assert.Contains("方法数量", result.Content);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact]
    public async Task CodeAnalysis_Full_ShouldCombineResults()
    {
        var tool = new CodeAnalysisTool();
        var tempFile = Path.Combine(Path.GetTempPath(), $"code_full_{Guid.NewGuid():N}.cs");
        File.WriteAllText(tempFile, "public class Test { public void Hello() { } }");

        try
        {
            var result = await tool.ExecuteAsync($"{{\"action\":\"full\",\"file_path\":\"{EscapePath(tempFile)}\"}}");
            Assert.True(result.Success);
            Assert.Contains("代码质量分析", result.Content);
            Assert.Contains("复杂度分析", result.Content);
            Assert.Contains("代码异味分析", result.Content);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact]
    public async Task CodeAnalysis_Python_ShouldDetectLanguage()
    {
        var tool = new CodeAnalysisTool();
        var tempFile = Path.Combine(Path.GetTempPath(), $"code_py_{Guid.NewGuid():N}.py");
        File.WriteAllText(tempFile, "import os\ndef hello():\n    pass\n");

        try
        {
            var result = await tool.ExecuteAsync($"{{\"action\":\"dependencies\",\"file_path\":\"{EscapePath(tempFile)}\"}}");
            Assert.True(result.Success);
            Assert.Contains("python", result.Content);
            Assert.Contains("os", result.Content);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    #endregion

    #region NotificationTool 增强

    [Fact]
    public async Task Notification_History_ShouldTrackNotifications()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"notify_hist_{Guid.NewGuid():N}");
        var tool = new NotificationTool(dir);

        var result = await tool.ExecuteAsync("""{"action":"notify","title":"Test","message":"Hello","priority":"normal"}""");
        Assert.True(result.Success);

        var historyResult = await tool.ExecuteAsync("""{"action":"history"}""");
        Assert.True(historyResult.Success);
        Assert.Contains("Test", historyResult.Content);
    }

    [Fact]
    public async Task Notification_Priority_ShouldRecordPriority()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"notify_prio_{Guid.NewGuid():N}");
        var tool = new NotificationTool(dir);

        await tool.ExecuteAsync("""{"action":"notify","title":"Urgent","message":"Now!","priority":"urgent"}""");

        var historyResult = await tool.ExecuteAsync("""{"action":"history"}""");
        Assert.Contains("urgent", historyResult.Content);
    }

    [Fact]
    public async Task Notification_ClearHistory_ShouldWork()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"notify_clr_{Guid.NewGuid():N}");
        var tool = new NotificationTool(dir);

        await tool.ExecuteAsync("""{"action":"notify","title":"Test","message":"Hello"}""");
        var clearResult = await tool.ExecuteAsync("""{"action":"clear_history"}""");
        Assert.True(clearResult.Success);

        var historyResult = await tool.ExecuteAsync("""{"action":"history"}""");
        Assert.Contains("暂无历史", historyResult.Content);
    }

    [Fact]
    public async Task Notification_Snooze_ShouldDelayReminder()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"notify_snooze_{Guid.NewGuid():N}");
        var tool = new NotificationTool(dir);

        var setResult = await tool.ExecuteAsync("""{"action":"reminder","title":"Test","message":"Hello","delay_seconds":1}""");
        var id = setResult.Content.Split("ID：")[1].Split("\n")[0].Trim();

        var snoozeResult = await tool.ExecuteAsync($"{{\"action\":\"snooze\",\"reminder_id\":\"{id}\",\"delay_seconds\":60}}");
        Assert.True(snoozeResult.Success);
        Assert.Contains("推迟", snoozeResult.Content);
    }

    [Fact]
    public async Task Notification_Persist_ShouldSurviveRestart()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"notify_persist_{Guid.NewGuid():N}");

        // 第一次实例设置提醒
        var tool1 = new NotificationTool(dir);
        await tool1.ExecuteAsync("""{"action":"reminder","title":"Persistent","message":"Hello","delay_seconds":3600}""");

        // 第二次实例应该能看到提醒
        var tool2 = new NotificationTool(dir);
        var listResult = await tool2.ExecuteAsync("""{"action":"list_reminders"}""");
        Assert.Contains("Persistent", listResult.Content);
    }

    #endregion

    #region Helpers

    private static string EscapePath(string path) => path.Replace("\\", "\\\\");

    private static string EscapeJsonString(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void CleanupDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }

    #endregion
}

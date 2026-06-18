using DesktopMascot.Core.Learning;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Storage;

namespace DesktopMascot.Core.Tests;

public class LearningEnginePersistenceTests
{
    [Fact]
    public void SaveAndLoad_ShouldPreservePreferences()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"learn_persist_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "learning.json");

        try
        {
            // 保存
            var engine = new LearningEngine(path);
            engine.RecordFeedback("task1", FeedbackType.Preference, "theme:dark");
            engine.Save();

            Assert.True(File.Exists(path), "Save should create file");

            // 加载
            var engine2 = new LearningEngine(path);
            var allPrefs = engine2.GetAllPreferences();
            Assert.NotEmpty(allPrefs);

            var pref = engine2.GetPreference("theme");
            Assert.NotNull(pref);
            Assert.Equal("dark", pref.Value);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPreserveTaskPatterns()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"learn_patterns_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "learning.json");

        try
        {
            var engine = new LearningEngine(path);
            engine.AnalyzeTaskPattern("summarize", true);
            engine.AnalyzeTaskPattern("summarize", true);
            engine.AnalyzeTaskPattern("summarize", false);
            engine.Save();

            var engine2 = new LearningEngine(path);
            var rate = engine2.GetTaskSuccessRate("summarize");
            Assert.True(rate > 0);
            Assert.True(rate < 1);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPreserveEvolutionHistory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"learn_evo_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "learning.json");

        try
        {
            var engine = new LearningEngine(path);
            engine.RecordFeedback("t1", FeedbackType.Positive, "great work");
            engine.RecordFeedback("t2", FeedbackType.Negative, "too slow");
            engine.Save();

            var engine2 = new LearningEngine(path);
            var history = engine2.GetEvolutionHistory();
            Assert.Equal(2, history.Count);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPreserveSkillSuggestions()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"learn_skills_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "learning.json");

        try
        {
            var engine = new LearningEngine(path);
            engine.AnalyzeTaskPattern("code_review", true);
            engine.AnalyzeTaskPattern("code_review", true);
            engine.AnalyzeTaskPattern("code_review", true);
            engine.Save();

            var engine2 = new LearningEngine(path);
            var report = engine2.GenerateReport();
            Assert.NotNull(report);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_NonExistentFile_ShouldStartEmpty()
    {
        var engine = new LearningEngine("/nonexistent/path/learning.json");
        var prefs = engine.GetAllPreferences();
        Assert.Empty(prefs);
    }

    [Fact]
    public void Save_DirtyFlag_ShouldOnlySaveWhenDirty()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"learn_dirty_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "learning.json");

        try
        {
            var engine = new LearningEngine(path);
            // 没有修改，Save 不应该写入文件
            engine.Save();
            Assert.False(File.Exists(path));

            // 修改后 Save 应该写入文件
            engine.RecordFeedback("t1", FeedbackType.Positive, "good");
            engine.Save();
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void RecordToolUsage_ShouldTrackPatterns()
    {
        var engine = new LearningEngine();
        engine.RecordToolUsage("read_file", true);
        engine.RecordToolUsage("read_file", true);
        engine.RecordToolUsage("read_file", false);

        var rate = engine.GetTaskSuccessRate("tool_read_file");
        Assert.True(rate > 0);
    }

    [Fact]
    public void RecordActiveTime_ShouldTrackHours()
    {
        var engine = new LearningEngine();
        engine.RecordActiveTime(DateTime.Now);
        engine.RecordActiveTime(DateTime.Now.AddHours(2));

        var report = engine.GenerateReport();
        Assert.NotNull(report);
        Assert.Equal(2, report.TotalTasks);
    }
}

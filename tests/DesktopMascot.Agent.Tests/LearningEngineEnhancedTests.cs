using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Learning;
using DesktopMascot.Core.Storage;

namespace DesktopMascot.Agent.Tests;

public class LearningEngineEnhancedTests
{
    [Fact]
    public void RecordToolUsage_ShouldTrackPatterns()
    {
        var engine = new LearningEngine();
        engine.RecordToolUsage("screen_capture", true);
        engine.RecordToolUsage("screen_capture", true);
        engine.RecordToolUsage("screen_capture", false);

        var report = engine.GenerateReport();
        Assert.True(report.TotalTasks > 0);
    }

    [Fact]
    public void RecordTaskDuration_ShouldTrackAverage()
    {
        var engine = new LearningEngine();
        engine.RecordTaskDuration("Chat", TimeSpan.FromSeconds(5));
        engine.RecordTaskDuration("Chat", TimeSpan.FromSeconds(10));
        engine.RecordTaskDuration("Chat", TimeSpan.FromSeconds(15));

        var report = engine.GenerateReport();
        Assert.True(report.TotalTasks > 0);
    }

    [Fact]
    public void RecordActiveTime_ShouldTrackHours()
    {
        var engine = new LearningEngine();
        engine.RecordActiveTime(new DateTime(2026, 6, 14, 10, 0, 0));
        engine.RecordActiveTime(new DateTime(2026, 6, 14, 10, 30, 0));
        engine.RecordActiveTime(new DateTime(2026, 6, 14, 14, 0, 0));

        var report = engine.GenerateReport();
        Assert.True(report.TotalTasks > 0);
    }

    [Fact]
    public void GenerateSkillsFromHistory_ShouldCreateSkills()
    {
        var engine = new LearningEngine();
        var history = new List<TaskHistoryRecord>
        {
            new() { Type = TaskType.Chat, Status = AppTaskStatus.Completed, CreatedAt = DateTime.UtcNow.AddMinutes(-10), CompletedAt = DateTime.UtcNow.AddMinutes(-5) },
            new() { Type = TaskType.Chat, Status = AppTaskStatus.Completed, CreatedAt = DateTime.UtcNow.AddMinutes(-8), CompletedAt = DateTime.UtcNow.AddMinutes(-3) },
            new() { Type = TaskType.Chat, Status = AppTaskStatus.Completed, CreatedAt = DateTime.UtcNow.AddMinutes(-6), CompletedAt = DateTime.UtcNow.AddMinutes(-1) },
        };

        var skills = engine.GenerateSkillsFromHistory(history);

        Assert.Single(skills);
        Assert.Equal("Chat", skills[0].TaskType);
        Assert.Equal(1.0f, skills[0].SuccessRate);
    }

    [Fact]
    public void AnalyzeBehaviorPattern_ShouldIdentifyPatterns()
    {
        var engine = new LearningEngine();
        var history = new List<TaskHistoryRecord>
        {
            new() { Type = TaskType.Chat, CreatedAt = new DateTime(2026, 6, 14, 10, 0, 0), CompletedAt = DateTime.UtcNow },
            new() { Type = TaskType.Chat, CreatedAt = new DateTime(2026, 6, 14, 10, 30, 0), CompletedAt = DateTime.UtcNow },
            new() { Type = TaskType.SummarizePage, CreatedAt = new DateTime(2026, 6, 14, 14, 0, 0), CompletedAt = DateTime.UtcNow },
        };

        var pattern = engine.AnalyzeBehaviorPattern(history);

        Assert.NotEmpty(pattern.MostFrequentTasks);
        Assert.Contains("Chat", pattern.MostFrequentTasks);
    }
}

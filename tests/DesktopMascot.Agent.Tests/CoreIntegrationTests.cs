using DesktopMascot.Core.Conversation;
using DesktopMascot.Core.Learning;
using DesktopMascot.Core.Summary;
using DesktopMascot.Core.Workflow;

namespace DesktopMascot.Agent.Tests;

public class ConversationTests
{
    [Fact]
    public void CreateConversation_ShouldCreateNewConversation()
    {
        var manager = new ConversationManager();
        var conversation = manager.CreateConversation("测试对话");

        Assert.NotNull(conversation);
        Assert.Equal("测试对话", conversation.Title);
        Assert.Same(conversation, manager.ActiveConversation);
    }

    [Fact]
    public void AddUserMessage_ShouldAddToActiveConversation()
    {
        var manager = new ConversationManager();
        manager.CreateConversation("测试");

        var message = manager.AddUserMessage("你好");

        Assert.NotNull(message);
        Assert.Equal("user", message.Role);
        Assert.Equal("你好", message.Content);
        Assert.Single(manager.ActiveConversation!.Messages);
    }

    [Fact]
    public void AddAssistantMessage_ShouldAddToActiveConversation()
    {
        var manager = new ConversationManager();
        manager.CreateConversation("测试");

        var message = manager.AddAssistantMessage("你好！有什么可以帮你的？");

        Assert.NotNull(message);
        Assert.Equal("assistant", message.Role);
        Assert.Single(manager.ActiveConversation!.Messages);
    }

    [Fact]
    public void GetContextForLLM_ShouldReturnRecentMessages()
    {
        var manager = new ConversationManager();
        manager.CreateConversation("测试");

        for (int i = 0; i < 15; i++)
        {
            manager.AddUserMessage($"消息 {i}");
        }

        var context = manager.GetContextForLLM(5);

        Assert.Equal(5, context.Count);
        Assert.Equal("消息 10", context[0].Content);
    }

    [Fact]
    public void SwitchConversation_ShouldChangeActive()
    {
        var manager = new ConversationManager();
        var conv1 = manager.CreateConversation("对话1");
        var conv2 = manager.CreateConversation("对话2");

        Assert.Same(conv2, manager.ActiveConversation);

        manager.SwitchConversation(conv1.Id);
        Assert.Same(conv1, manager.ActiveConversation);
    }

    [Fact]
    public void GetContextSummary_ShouldContainKeyInfo()
    {
        var manager = new ConversationManager();
        var conv = manager.CreateConversation("测试对话");
        conv.Summary = "这是一个测试摘要";
        manager.AddUserMessage("测试消息");

        var summary = conv.GetContextSummary();

        Assert.Contains("测试对话", summary);
        Assert.Contains("测试摘要", summary);
        Assert.Contains("测试消息", summary);
    }
}

public class LearningTests
{
    [Fact]
    public void RecordFeedback_Positive_ShouldIncreaseScore()
    {
        var engine = new LearningEngine();
        engine.RecordFeedback("task1", FeedbackType.Positive, "做得好");

        var history = engine.GetEvolutionHistory();
        Assert.Single(history);
        Assert.Equal("positive_feedback", history[0].Type);
        Assert.True(history[0].ImpactScore > 0);
    }

    [Fact]
    public void RecordFeedback_Negative_ShouldDecreaseScore()
    {
        var engine = new LearningEngine();
        engine.RecordFeedback("task1", FeedbackType.Negative, "做得不好");

        var history = engine.GetEvolutionHistory();
        Assert.Single(history);
        Assert.Equal("negative_feedback", history[0].Type);
        Assert.True(history[0].ImpactScore < 0);
    }

    [Fact]
    public void RecordPreference_ShouldStorePreference()
    {
        var engine = new LearningEngine();
        engine.RecordFeedback("task1", FeedbackType.Preference, "语言:中文");

        var pref = engine.GetPreference("语言");
        Assert.NotNull(pref);
        Assert.Equal("中文", pref.Value);
    }

    [Fact]
    public void AnalyzeTaskPattern_ShouldTrackPatterns()
    {
        var engine = new LearningEngine();
        
        engine.AnalyzeTaskPattern("Chat", true);
        engine.AnalyzeTaskPattern("Chat", true);
        engine.AnalyzeTaskPattern("Chat", true);

        var rate = engine.GetTaskSuccessRate("Chat");
        Assert.Equal(1.0f, rate);
    }

    [Fact]
    public void GenerateReport_ShouldContainStats()
    {
        var engine = new LearningEngine();
        engine.AnalyzeTaskPattern("Chat", true);
        engine.AnalyzeTaskPattern("Chat", true);
        engine.AnalyzeTaskPattern("Chat", false);

        var report = engine.GenerateReport();

        Assert.Equal(3, report.TotalTasks);
        Assert.True(report.SuccessRate > 0);
    }
}

public class WorkflowTemplateTests
{
    [Fact]
    public void GetBuiltInTemplates_ShouldReturnTemplates()
    {
        var templates = WorkflowTemplates.GetBuiltInTemplates();

        Assert.NotEmpty(templates);
        Assert.Contains(templates, t => t.Name == "代码审查");
        Assert.Contains(templates, t => t.Name == "文件整理");
    }

    [Fact]
    public void FindTemplate_ShouldFindByName()
    {
        var template = WorkflowTemplates.FindTemplate("代码审查");

        Assert.NotNull(template);
        Assert.Equal("代码审查", template.Name);
    }

    [Fact]
    public void FindTemplate_ShouldFindByTag()
    {
        var template = WorkflowTemplates.FindTemplate("修复");

        Assert.NotNull(template);
        Assert.Equal("Bug修复", template.Name);
    }

    [Fact]
    public void GetCategories_ShouldReturnUniqueCategories()
    {
        var categories = WorkflowTemplates.GetCategories();

        Assert.NotEmpty(categories);
        Assert.Equal(categories.Count, categories.Distinct().Count());
    }
}

public class TaskSummaryTests
{
    [Fact]
    public void TaskSummary_ShouldContainRequiredFields()
    {
        var summary = new TaskSummary
        {
            TaskId = "task1",
            Title = "测试任务",
            Summary = "这是一个测试任务的总结"
        };

        Assert.Equal("task1", summary.TaskId);
        Assert.Equal("测试任务", summary.Title);
        Assert.Equal("这是一个测试任务的总结", summary.Summary);
    }

    [Fact]
    public void ConversationSummaryResult_ShouldContainKeyTopics()
    {
        var result = new ConversationSummaryResult
        {
            ConversationId = "conv1",
            Summary = "对话总结",
            KeyTopics = new List<string> { "主题1", "主题2" }
        };

        Assert.Equal(2, result.KeyTopics.Count);
    }
}

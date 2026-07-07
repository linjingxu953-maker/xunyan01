using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Engines;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Agent.Tools;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Security;
using Microsoft.Extensions.Logging;
using Moq;

namespace DesktopMascot.Agent.Tests;

/// <summary>
/// 端到端集成测试 - 验证各场景的完整流程
/// </summary>
public class EndToEndTests
{
    private readonly Mock<ITaskEventBus> _mockEventBus = new();
    private readonly Mock<ILogger<AgentOrchestrator>> _mockLogger = new();

    [Fact]
    public async Task ChatFlow_UserAskQuestion_ShouldReturnAnswer()
    {
        var (orchestrator, mockLlm) = CreateOrchestrator();
        SetupLlmResponse(mockLlm, "你是一个友善、专业的 AI 助手");

        var task = CreateTask("用户对话", "什么是量子计算？", TaskType.Chat);
        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Content);
        VerifyPromptContains(mockLlm, "AI 助手");
    }

    [Fact]
    public async Task AnalyzeErrorFlow_ErrorInput_ShouldReturnAnalysis()
    {
        var (orchestrator, mockLlm) = CreateOrchestrator();
        SetupLlmResponse(mockLlm, "错误分析助手");

        var task = CreateTask("分析当前报错", "TypeError: Cannot read property of undefined", TaskType.AnalyzeError);
        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Content);
        VerifyPromptContains(mockLlm, "错误分析");
    }

    [Fact]
    public async Task InspectProjectFlow_ProjectInput_ShouldReturnAnalysis()
    {
        var (orchestrator, mockLlm) = CreateOrchestrator();
        SetupLlmResponse(mockLlm, "项目结构分析");

        var task = CreateTask("诊断项目目录", "分析这个项目", TaskType.InspectProject);
        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Content);
        VerifyPromptContains(mockLlm, "项目结构");
    }

    [Fact]
    public async Task SolveProblemFlow_MathProblem_ShouldReturnSolution()
    {
        var (orchestrator, mockLlm) = CreateOrchestrator();
        SetupLlmResponse(mockLlm, "解题助手");

        var task = CreateTask("题目解答", "求解 x^2 + 5x + 6 = 0", TaskType.SolveProblem);
        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Content);
        VerifyPromptContains(mockLlm, "解题");
    }

    [Fact]
    public async Task WriteFileFlow_CodeRequest_ShouldReturnCode()
    {
        var (orchestrator, mockLlm) = CreateOrchestrator();
        SetupLlmResponse(mockLlm, "文件生成助手");

        var task = CreateTask("生成文件", "写一个Python Hello World", TaskType.WriteFile);
        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Content);
        VerifyPromptContains(mockLlm, "文件生成");
    }

    [Fact]
    public async Task RunCommandFlow_CommandQuestion_ShouldReturnCommand()
    {
        var (orchestrator, mockLlm) = CreateOrchestrator();
        SetupLlmResponse(mockLlm, "命令执行助手");

        var task = CreateTask("执行命令", "怎么查看文件？", TaskType.RunCommand);
        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Content);
        VerifyPromptContains(mockLlm, "命令执行");
    }

    [Fact]
    public async Task SummarizePageFlow_WithScreenshot_ShouldReturnSummary()
    {
        var mockContext = new MockContextProvider
        {
            MockWindowTitle = "GitHub - dotnet/runtime",
            MockAppName = "chrome"
        };
        var mockLlm = new Mock<ILlmProvider>();
        var registry = CreateRegistry(mockContext, mockLlm.Object);

        var orchestrator = CreateOrchestratorWithAllowingPipeline(mockLlm.Object, registry);

        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = "这是一个关于 GitHub 项目的总结"
            });

        var task = CreateTask("总结当前内容", "总结这个页面", TaskType.SummarizePage);
        task.Parameters["TaskType"] = TaskType.SummarizePage;

        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.Contains("GitHub", result.Content);
    }

    [Fact]
    public async Task ScreenUnderstandFlow_WithScreenshot_ShouldReturnAnalysis()
    {
        var mockContext = new MockContextProvider
        {
            MockScreenshotPath = Path.Combine(Path.GetTempPath(), "test_e2e.png")
        };
        var mockLlm = new Mock<ILlmProvider>();
        var registry = CreateRegistry(mockContext, mockLlm.Object);

        var orchestrator = CreateOrchestratorWithAllowingPipeline(mockLlm.Object, registry);

        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = """{"identification":"test","understanding":"test","suggestions":[],"needsAction":false,"confidence":0.9}"""
            });

        try
        {
            File.WriteAllBytes(mockContext.MockScreenshotPath, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

            var task = CreateTask("屏幕理解", "帮我看看这个", TaskType.ScreenUnderstand);
            task.Parameters["TaskType"] = TaskType.ScreenUnderstand;

            var result = await orchestrator.ExecuteAsync(task);

            Assert.True(result.Success);
            Assert.Contains("test", result.Content);
        }
        finally
        {
            if (File.Exists(mockContext.MockScreenshotPath))
                File.Delete(mockContext.MockScreenshotPath);
        }
    }

    [Fact]
    public async Task AllTaskTypes_ShouldHaveDedicatedPrompt()
    {
        var taskTypes = new[]
        {
            TaskType.Chat,
            TaskType.AnalyzeError,
            TaskType.InspectProject,
            TaskType.SolveProblem,
            TaskType.WriteFile,
            TaskType.RunCommand
        };

        foreach (var taskType in taskTypes)
        {
            var mockLlm = new Mock<ILlmProvider>();
            var registry = new ToolRegistry();
            var orchestrator = new AgentOrchestrator(
                mockLlm.Object, registry, _mockEventBus.Object, _mockLogger.Object, maxIterations: 3);

            var capturedMessages = new List<LlmMessage>();
            mockLlm.Setup(x => x.ChatAsync(
                    It.IsAny<IEnumerable<LlmMessage>>(),
                    It.IsAny<IEnumerable<ToolDefinition>?>(),
                    It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<LlmMessage>, IEnumerable<ToolDefinition>?, CancellationToken>(
                    (msgs, _, _) => capturedMessages.AddRange(msgs))
                .ReturnsAsync(new LlmResponse
                {
                    Success = true,
                    Content = "test response"
                });

            var task = CreateTask("测试", "测试输入", taskType);
            task.Parameters["TaskType"] = taskType;

            await orchestrator.ExecuteAsync(task);

            Assert.True(capturedMessages.Count > 0, $"TaskType {taskType} should call LLM");
            var systemMessage = capturedMessages.FirstOrDefault(m => m.Role == "system");
            Assert.NotNull(systemMessage);
            Assert.False(string.IsNullOrEmpty(systemMessage.Content), $"TaskType {taskType} should have system prompt");
        }
    }

    private (AgentOrchestrator orchestrator, Mock<ILlmProvider> mockLlm) CreateOrchestrator()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var registry = new ToolRegistry();
        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, registry, _mockEventBus.Object, _mockLogger.Object, maxIterations: 3);
        return (orchestrator, mockLlm);
    }

    private static ToolRegistry CreateRegistry(MockContextProvider context, ILlmProvider llmProvider)
    {
        var registry = new ToolRegistry();
        registry.SetContextProvider(context);
        registry.Register(new ScreenUnderstandTool(context, llmProvider));
        return registry;
    }

    private AgentOrchestrator CreateOrchestratorWithAllowingPipeline(ILlmProvider llmProvider, ToolRegistry registry)
    {
        return new AgentOrchestrator(new AgentOrchestratorOptions
        {
            LlmProvider = llmProvider,
            ToolRegistry = registry,
            EventBus = _mockEventBus.Object,
            Logger = _mockLogger.Object,
            ToolPipeline = new DesktopMascot.Core.Tools.ToolExecutionPipeline(
                registry,
                new PermissionConfirmationService(new PermissionManager(), new AllowingPermissionPrompt()))
        });
    }

    private sealed class AllowingPermissionPrompt : IPermissionPrompt
    {
        public Task<PermissionPromptResponse> PromptAsync(PermissionPromptRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new PermissionPromptResponse
            {
                RequestId = request.RequestId,
                Decision = PermissionDecision.AllowOnce
            });
        }

        public bool HasPermission(PromptPermissionType type, string scope) => false;
        public void RevokePermission(PromptPermissionType type, string scope) { }
    }

    private static AgentTask CreateTask(string title, string input, TaskType type)
    {
        return new AgentTask
        {
            Title = title,
            Input = input,
            Type = type,
            Parameters = new Dictionary<string, object> { ["TaskType"] = type }
        };
    }

    private static void SetupLlmResponse(Mock<ILlmProvider> mockLlm, string promptKeyword)
    {
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = $"这是基于 {promptKeyword} 的回答"
            });
    }

    private static void VerifyPromptContains(Mock<ILlmProvider> mockLlm, string expected)
    {
        var call = mockLlm.Invocations.First(i => i.Method.Name == "ChatAsync");
        var messages = call.Arguments[0] as IEnumerable<LlmMessage>;
        var systemMessage = messages?.First(m => m.Role == "system");
        Assert.Contains(expected, systemMessage?.Content ?? "");
    }
}

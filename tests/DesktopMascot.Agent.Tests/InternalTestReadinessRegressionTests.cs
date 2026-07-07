using System.Reflection;
using DesktopMascot.Agent.Context;
using DesktopMascot.Agent.Engines;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using DesktopMascot.Agent.Tools;
using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Security;
using DesktopMascot.Core.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using AgentToolDefinition = DesktopMascot.Agent.Models.ToolDefinition;
using CoreToolResult = DesktopMascot.Core.Tools.ToolResult;

namespace DesktopMascot.Agent.Tests;

public sealed class InternalTestReadinessRegressionTests : IDisposable
{
    private readonly List<string> _tempPaths = [];

    [Fact]
    public async Task GenericToolCall_WhenPermissionDenied_DoesNotExecuteTool()
    {
        var registry = new DesktopMascot.Agent.Tools.ToolRegistry();
        var tool = new CountingConfirmationTool();
        registry.Register(tool);

        var prompt = new DenyingPermissionPrompt();
        var pipeline = CreatePipeline(registry, prompt);

        var llm = new Mock<ILlmProvider>();
        llm.SetupSequence(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<AgentToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = "calling tool",
                ToolCalls =
                [
                    new ToolCall
                    {
                        Name = tool.Name,
                        Arguments = """{"path":"blocked.txt","content":"must not write"}"""
                    }
                ]
            })
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = "done"
            });

        var orchestrator = new AgentOrchestrator(new AgentOrchestratorOptions
        {
            LlmProvider = llm.Object,
            ToolRegistry = registry,
            ToolPipeline = pipeline,
            EventBus = new Mock<ITaskEventBus>().Object,
            Logger = new Mock<ILogger<AgentOrchestrator>>().Object,
            MaxIterations = 2
        });

        var result = await InvokeGenericTaskAsync(orchestrator, new AgentTask
        {
            Id = "permission-denied-task",
            Title = "permission regression",
            Input = "write a file",
            Type = TaskType.Chat
        });

        Assert.True(result.Success);
        Assert.Equal(1, prompt.RequestCount);
        Assert.Equal(0, tool.ExecuteCount);
    }

    [Fact]
    public async Task ScreenUnderstand_WhenPipelineDeniesPermission_DoesNotCaptureScreen()
    {
        var screenshotPath = CreateTempFile([1, 2, 3, 4]);
        var context = new MockContextProvider
        {
            MockScreenshotPath = screenshotPath,
            MockRegionScreenshotPath = screenshotPath
        };

        var llm = new Mock<ILlmProvider>();
        llm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<AgentToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = """{"identification":"screen","understanding":"ok","needsAction":false,"confidence":0.9}"""
            });

        var registry = new DesktopMascot.Agent.Tools.ToolRegistry();
        ToolRegistryInitializer.RegisterBuiltInTools(registry, context, llm.Object);

        var prompt = new DenyingPermissionPrompt();
        var pipeline = CreatePipeline(registry, prompt);
        var orchestrator = new AgentOrchestrator(new AgentOrchestratorOptions
        {
            LlmProvider = llm.Object,
            ToolRegistry = registry,
            ToolPipeline = pipeline,
            EventBus = new Mock<ITaskEventBus>().Object,
            Logger = new Mock<ILogger<AgentOrchestrator>>().Object
        });

        var result = await orchestrator.ExecuteAsync(new AgentTask
        {
            Id = "screen-denied-task",
            Title = "screen denied",
            Input = "understand screen",
            Type = TaskType.ScreenUnderstand,
            Parameters = new Dictionary<string, object>
            {
                ["Region"] = new { x = 10, y = 20, width = 100, height = 80 }
            }
        });

        Assert.False(result.Success);
        Assert.Equal(1, prompt.RequestCount);
        Assert.Equal(0, context.FullScreenshotCaptureCount);
        Assert.Equal(0, context.RegionScreenshotCaptureCount);
    }

    [Fact]
    public async Task ScreenUnderstandTool_UsesLatestProviderFromAccessor()
    {
        var screenshotPath = CreateTempFile([8, 6, 7, 5]);
        var context = new MockContextProvider
        {
            MockScreenshotPath = screenshotPath
        };

        var startupProvider = new Mock<ILlmProvider>();
        var currentProvider = new Mock<ILlmProvider>();
        currentProvider.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<AgentToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = """{"identification":"current","understanding":"used current provider","needsAction":false,"confidence":0.9}"""
            });

        ILlmProvider activeProvider = startupProvider.Object;
        var tool = new ScreenUnderstandTool(context, () => activeProvider);
        activeProvider = currentProvider.Object;

        var result = await tool.ExecuteAsync("{}", CancellationToken.None);

        Assert.True(result.Success);
        startupProvider.Verify(x => x.ChatAsync(
            It.IsAny<IEnumerable<LlmMessage>>(),
            It.IsAny<IEnumerable<AgentToolDefinition>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        currentProvider.Verify(x => x.ChatAsync(
            It.IsAny<IEnumerable<LlmMessage>>(),
            It.IsAny<IEnumerable<AgentToolDefinition>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose()
    {
        foreach (var path in _tempPaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for temporary test artifacts.
            }
        }
    }

    private string CreateTempFile(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"desktop_mascot_test_{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, bytes);
        _tempPaths.Add(path);
        return path;
    }

    private static ToolExecutionPipeline CreatePipeline(DesktopMascot.Agent.Tools.ToolRegistry registry, IPermissionPrompt prompt)
    {
        var permissionManager = new Mock<IPermissionManager>();
        permissionManager
            .Setup(x => x.LogAuditAsync(It.IsAny<AuditLogEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ToolExecutionPipeline(
            registry,
            new PermissionConfirmationService(permissionManager.Object, prompt));
    }

    private static async Task<TaskResult> InvokeGenericTaskAsync(AgentOrchestrator orchestrator, AgentTask task)
    {
        var method = typeof(AgentOrchestrator).GetMethod(
            "ExecuteGenericTaskAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var value = method!.Invoke(orchestrator, [task, CancellationToken.None]);
        var genericTask = Assert.IsType<Task<TaskResult>>(value);
        return await genericTask;
    }

    private sealed class CountingConfirmationTool : ITool
    {
        public int ExecuteCount { get; private set; }
        public string Name => "danger_confirmation_tool";
        public string Description => "A test tool that must not execute without permission.";
        public string ParametersSchema => "{}";
        public bool RequiresConfirmation => true;
        public string ConfirmationMessage => "confirm test tool";

        public Task<CoreToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
        {
            ExecuteCount++;
            return Task.FromResult(new CoreToolResult
            {
                Name = Name,
                Success = true,
                Content = "executed"
            });
        }
    }

    private sealed class DenyingPermissionPrompt : IPermissionPrompt
    {
        public int RequestCount { get; private set; }

        public Task<PermissionPromptResponse> PromptAsync(
            PermissionPromptRequest request,
            CancellationToken ct = default)
        {
            RequestCount++;
            return Task.FromResult(new PermissionPromptResponse
            {
                RequestId = request.RequestId,
                Decision = PermissionDecision.Deny,
                DenyReason = "denied by test"
            });
        }

        public bool HasPermission(PromptPermissionType type, string scope) => false;

        public void RevokePermission(PromptPermissionType type, string scope)
        {
        }
    }
}

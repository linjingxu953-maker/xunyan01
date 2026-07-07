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

public class SummarizePageTests
{
    private readonly Mock<ITaskEventBus> _mockEventBus;
    private readonly Mock<ILogger<AgentOrchestrator>> _mockLogger;

    public SummarizePageTests()
    {
        _mockEventBus = new Mock<ITaskEventBus>();
        _mockLogger = new Mock<ILogger<AgentOrchestrator>>();
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

    [Fact]
    public async Task ExecuteAsync_SummarizePage_ShouldCallLlmWithVisionMessage()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var mockContext = new MockContextProvider
        {
            MockWindowTitle = "ChatGPT - Google Chrome",
            MockAppName = "chrome",
            MockBrowserContent = "这是一篇关于AI的文章..."
        };

        var registry = new ToolRegistry();
        registry.SetContextProvider(mockContext);

        var orchestrator = CreateOrchestratorWithAllowingPipeline(mockLlm.Object, registry);

        var task = new AgentTask
        {
            Title = "总结当前内容",
            Input = "总结这个页面",
            Type = TaskType.SummarizePage,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.SummarizePage }
        };

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
                Content = "**页面标题**: ChatGPT\n**核心内容**: AI对话工具"
            });

        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.Contains("ChatGPT", result.Content);
        Assert.Equal(2, capturedMessages.Count);
        Assert.Equal("system", capturedMessages[0].Role);
        Assert.Contains("网页内容分析", capturedMessages[0].Content);
        Assert.Equal("user", capturedMessages[1].Role);
        Assert.Contains("ChatGPT - Google Chrome", capturedMessages[1].Content);
    }

    [Fact]
    public void ToolRegistry_SetContextProvider_ShouldWork()
    {
        var registry = new ToolRegistry();
        var mockContext = new MockContextProvider();

        registry.SetContextProvider(mockContext);
        var retrieved = registry.GetContextProvider();

        Assert.NotNull(retrieved);
        Assert.Same(mockContext, retrieved);
    }

    [Fact]
    public void ToolRegistry_GetContextProvider_Default_ShouldBeNull()
    {
        var registry = new ToolRegistry();
        var result = registry.GetContextProvider();
        Assert.Null(result);
    }

    [Fact]
    public void LlmMessage_Images_ShouldDefaultToNull()
    {
        var msg = new LlmMessage { Role = "user", Content = "hello" };
        Assert.Null(msg.Images);
    }

    [Fact]
    public void LlmMessage_Images_CanBeSet()
    {
        var msg = new LlmMessage
        {
            Role = "user",
            Content = "看这张图",
            Images = new List<VisionContent>
            {
                new() { Base64Data = "abc123", MediaType = "image/png" }
            }
        };

        Assert.NotNull(msg.Images);
        Assert.Single(msg.Images);
        Assert.Equal("abc123", msg.Images[0].Base64Data);
        Assert.Equal("image/png", msg.Images[0].MediaType);
    }

    [Fact]
    public void VisionContent_DefaultMediaType_ShouldBePng()
    {
        var vision = new VisionContent { Base64Data = "data" };
        Assert.Equal("image/png", vision.MediaType);
    }

    [Fact]
    public async Task BrowserContextTool_WithMockProvider_ShouldReturnRealData()
    {
        var mockContext = new MockContextProvider
        {
            MockWindowTitle = "GitHub - dotnet/runtime",
            MockAppName = "chrome"
        };

        var tool = new BrowserContextTool(mockContext);
        var result = await tool.ExecuteAsync("{}");

        Assert.True(result.Success);
        Assert.Contains("GitHub - dotnet/runtime", result.Content);
        Assert.Contains("chrome", result.Content);
    }

    [Fact]
    public async Task BrowserContextTool_Metadata_ShouldBeCorrect()
    {
        var mockContext = new MockContextProvider();
        var tool = new BrowserContextTool(mockContext);

        Assert.Equal("browser_context", tool.Name);
        Assert.NotEmpty(tool.Description);
    }

    [Fact]
    public async Task ExecuteAsync_GenericTask_ShouldUseReActLoop()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var registry = new ToolRegistry();

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, registry, _mockEventBus.Object, _mockLogger.Object, maxIterations: 3);

        var task = new AgentTask
        {
            Title = "普通聊天",
            Input = "你好",
            Type = TaskType.Chat
        };

        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = "你好！"
            });

        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.Equal("你好！", result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_SummarizePage_LlmFailure_ShouldReturnFailed()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var mockContext = new MockContextProvider();
        var registry = new ToolRegistry();
        registry.SetContextProvider(mockContext);

        var orchestrator = CreateOrchestratorWithAllowingPipeline(mockLlm.Object, registry);

        var task = new AgentTask
        {
            Title = "总结当前内容",
            Input = "总结这个页面",
            Type = TaskType.SummarizePage,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.SummarizePage }
        };

        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = false,
                Error = "模型不可用"
            });

        var result = await orchestrator.ExecuteAsync(task);

        Assert.False(result.Success);
        Assert.Contains("模型不可用", result.Error);
    }

    [Fact]
    public async Task ScreenUnderstandTool_Metadata_ShouldBeCorrect()
    {
        var mockContext = new MockContextProvider();
        var mockLlm = new Mock<ILlmProvider>();
        var tool = new ScreenUnderstandTool(mockContext, mockLlm.Object);

        Assert.Equal("screen_understand", tool.Name);
        Assert.Contains("区域", tool.Description);
        Assert.Contains("region", tool.ParametersSchema);
    }

    [Fact]
    public async Task ScreenUnderstandTool_WithMockProvider_ShouldReturnStructuredResult()
    {
        var mockContext = new MockContextProvider
        {
            MockScreenshotPath = Path.Combine(Path.GetTempPath(), "test_screenshot.png")
        };

        var mockLlm = new Mock<ILlmProvider>();
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = """
                {
                    "identification": "VS Code 报错弹窗 - 无法找到模块 'lodash'",
                    "understanding": "用户可能想修复这个导入错误",
                    "userIntent": null,
                    "suggestions": ["检查 node_modules", "运行 npm install"],
                    "needsAction": true,
                    "recommendedActions": [
                        {
                            "name": "安装依赖",
                            "description": "运行 npm install 安装缺失的依赖",
                            "actionType": "run_command",
                            "parameters": {"command": "npm install"},
                            "riskLevel": "low"
                        }
                    ],
                    "confidence": 0.85
                }
                """
            });

        var tool = new ScreenUnderstandTool(mockContext, mockLlm.Object);

        try
        {
            File.WriteAllBytes(mockContext.MockScreenshotPath, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

            var result = await tool.ExecuteAsync("{}");

            Assert.True(result.Success);
            Assert.Contains("VS Code", result.Content);
            Assert.Contains("npm install", result.Content);
        }
        finally
        {
            if (File.Exists(mockContext.MockScreenshotPath))
                File.Delete(mockContext.MockScreenshotPath);
        }
    }

    [Fact]
    public async Task ScreenUnderstandTool_WithRegion_ShouldCaptureSelectedRegion()
    {
        var mockContext = new MockContextProvider
        {
            MockScreenshotPath = Path.Combine(Path.GetTempPath(), $"full_{Guid.NewGuid():N}.png"),
            MockRegionScreenshotPath = Path.Combine(Path.GetTempPath(), $"region_{Guid.NewGuid():N}.png")
        };
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = """
                {
                    "identification": "圈选区域",
                    "understanding": "正在分析用户圈选的屏幕部分",
                    "needsAction": false,
                    "confidence": 0.8
                }
                """
            });

        var tool = new ScreenUnderstandTool(mockContext, mockLlm.Object);

        try
        {
            File.WriteAllBytes(mockContext.MockScreenshotPath, [0x89, 0x50, 0x4E, 0x47]);
            File.WriteAllBytes(mockContext.MockRegionScreenshotPath, [0x89, 0x50, 0x4E, 0x47, 0x01]);

            var result = await tool.ExecuteAsync("""
            {
                "region": { "x": -120, "y": 80, "width": 320, "height": 180 },
                "user_hint": "用户圈选的屏幕区域"
            }
            """);

            Assert.True(result.Success);
            Assert.Equal((-120, 80, 320, 180), mockContext.LastCapturedRegion);
            Assert.Equal(0, mockContext.FullScreenshotCaptureCount);
            Assert.Equal(1, mockContext.RegionScreenshotCaptureCount);
        }
        finally
        {
            if (File.Exists(mockContext.MockScreenshotPath))
                File.Delete(mockContext.MockScreenshotPath);
            if (File.Exists(mockContext.MockRegionScreenshotPath))
                File.Delete(mockContext.MockRegionScreenshotPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ScreenUnderstand_WithUiRegionParameters_ShouldCaptureRegionAndReturnScreenshotPath()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var registry = new ToolRegistry();
        var mockContext = new MockContextProvider
        {
            MockScreenshotPath = Path.Combine(Path.GetTempPath(), $"full_{Guid.NewGuid():N}.png"),
            MockRegionScreenshotPath = Path.Combine(Path.GetTempPath(), $"region_{Guid.NewGuid():N}.png")
        };

        registry.SetContextProvider(mockContext);
        registry.Register(new ScreenUnderstandTool(mockContext, mockLlm.Object));

        var orchestrator = CreateOrchestratorWithAllowingPipeline(mockLlm.Object, registry);

        var task = new AgentTask
        {
            Title = "Screen understand selected region",
            Input = "Analyze selected area",
            Type = TaskType.ScreenUnderstand,
            Parameters = new Dictionary<string, object>
            {
                ["TaskType"] = TaskType.ScreenUnderstand,
                ["Region"] = new
                {
                    region = new
                    {
                        x = -120,
                        y = 80,
                        width = 320,
                        height = 180
                    }
                },
                ["UserHint"] = "selected screen area"
            }
        };

        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = """{"identification":"selected region","understanding":"selected region understanding","suggestions":["check details"],"needsAction":false,"confidence":0.9}"""
            });

        try
        {
            File.WriteAllBytes(mockContext.MockScreenshotPath, [0x89, 0x50, 0x4E, 0x47]);
            File.WriteAllBytes(mockContext.MockRegionScreenshotPath, [0x89, 0x50, 0x4E, 0x47, 0x01]);

            var result = await orchestrator.ExecuteAsync(task);

            Assert.True(result.Success, result.Error);
            Assert.Equal((-120, 80, 320, 180), mockContext.LastCapturedRegion);
            Assert.Equal(0, mockContext.FullScreenshotCaptureCount);
            Assert.Equal(1, mockContext.RegionScreenshotCaptureCount);
            Assert.Contains("screenshotPath", result.Content);
            Assert.Contains(Path.GetFileName(mockContext.MockRegionScreenshotPath), result.Content);
        }
        finally
        {
            if (File.Exists(mockContext.MockScreenshotPath))
                File.Delete(mockContext.MockScreenshotPath);
            if (File.Exists(mockContext.MockRegionScreenshotPath))
                File.Delete(mockContext.MockRegionScreenshotPath);
        }
    }

    [Fact]
    public async Task ScreenUnderstandTool_LlmFailure_ShouldReturnError()
    {
        var mockContext = new MockContextProvider
        {
            MockScreenshotPath = Path.Combine(Path.GetTempPath(), "test_llm_fail.png")
        };
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = false,
                Error = "API 超时"
            });

        var tool = new ScreenUnderstandTool(mockContext, mockLlm.Object);

        try
        {
            File.WriteAllBytes(mockContext.MockScreenshotPath, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            var result = await tool.ExecuteAsync("{}");

            Assert.False(result.Success);
            Assert.Contains("API 超时", result.Error);
        }
        finally
        {
            if (File.Exists(mockContext.MockScreenshotPath))
                File.Delete(mockContext.MockScreenshotPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ScreenUnderstand_ShouldCallScreenTool()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var registry = new ToolRegistry();
        var mockContext = new MockContextProvider
        {
            MockScreenshotPath = Path.Combine(Path.GetTempPath(), "test_screen_exec.png")
        };

        registry.SetContextProvider(mockContext);
        registry.Register(new ScreenUnderstandTool(mockContext, mockLlm.Object));

        var orchestrator = CreateOrchestratorWithAllowingPipeline(mockLlm.Object, registry);

        var task = new AgentTask
        {
            Title = "屏幕理解",
            Input = "帮我看看这个报错",
            Type = TaskType.ScreenUnderstand,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.ScreenUnderstand }
        };

        mockLlm.Setup(x => x.ChatAsync(
                It.IsAny<IEnumerable<LlmMessage>>(),
                It.IsAny<IEnumerable<ToolDefinition>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = """{"identification":"test","understanding":"test understanding","suggestions":["suggestion1"],"needsAction":false,"confidence":0.9}"""
            });

        try
        {
            File.WriteAllBytes(mockContext.MockScreenshotPath, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
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
    public async Task ExecuteAsync_AnalyzeError_ShouldUseSpecializedPrompt()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var mockContext = new MockContextProvider
        {
            MockWindowTitle = "VS Code",
            MockAppName = "Code"
        };
        var registry = new ToolRegistry();
        registry.SetContextProvider(mockContext);

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, registry, _mockEventBus.Object, _mockLogger.Object);

        var task = new AgentTask
        {
            Title = "分析当前报错",
            Input = "帮我看这个报错：TypeError: Cannot read property 'map' of undefined",
            Type = TaskType.AnalyzeError,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.AnalyzeError }
        };

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
                Content = "**错误类型**: TypeError\n**可能原因**: 数组未定义\n**解决方案**: 添加空值检查"
            });

        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.Contains("TypeError", result.Content);
        Assert.Equal(2, capturedMessages.Count);
        Assert.Contains("错误分析", capturedMessages[0].Content);
    }

    [Fact]
    public async Task ExecuteAsync_InspectProject_ShouldUseSpecializedPrompt()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var mockContext = new MockContextProvider();
        var registry = new ToolRegistry();
        registry.SetContextProvider(mockContext);

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, registry, _mockEventBus.Object, _mockLogger.Object);

        var task = new AgentTask
        {
            Title = "诊断项目目录",
            Input = "帮我分析这个项目结构",
            Type = TaskType.InspectProject,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.InspectProject }
        };

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
                Content = "**项目类型**: C# 应用\n**技术栈**: .NET 8 + Avalonia"
            });

        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.Contains("C#", result.Content);
        Assert.Contains("项目结构分析", capturedMessages[0].Content);
    }

    [Fact]
    public async Task ExecuteAsync_SolveProblem_ShouldUseSpecializedPrompt()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var registry = new ToolRegistry();

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, registry, _mockEventBus.Object, _mockLogger.Object);

        var task = new AgentTask
        {
            Title = "题目解答",
            Input = "求解：x^2 + 5x + 6 = 0",
            Type = TaskType.SolveProblem,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.SolveProblem }
        };

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
                Content = "**答案**: x = -2 或 x = -3"
            });

        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.Contains("x =", result.Content);
        Assert.Contains("解题助手", capturedMessages[0].Content);
    }

    [Fact]
    public async Task ListDirectoryTool_ShouldReturnDirectoryStructure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_dir_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "test.cs"), "class Test {}");
            File.WriteAllText(Path.Combine(tempDir, "test.json"), "{}");

            var provider = new MockContextProvider();
            var tool = new ListDirectoryTool(provider);
            var args = $"{{\"path\": \"{tempDir.Replace("\\", "\\\\")}\"}}";

            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("test.cs", result.Content);
            Assert.Contains("test.json", result.Content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ListDirectoryTool_Recursive_ShouldListSubdirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_recursive_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "root.cs"), "class Root {}");
            Directory.CreateDirectory(Path.Combine(tempDir, "sub"));
            File.WriteAllText(Path.Combine(tempDir, "sub", "inner.cs"), "class Inner {}");

            var provider = new MockContextProvider();
            var tool = new ListDirectoryTool(provider);
            var args = $"{{\"path\": \"{tempDir.Replace("\\", "\\\\")}\", \"recursive\": true}}";

            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("root.cs", result.Content);
            Assert.Contains("sub", result.Content);
            Assert.Contains("inner.cs", result.Content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ListDirectoryTool_NonexistentPath_ShouldFail()
    {
        var provider = new MockContextProvider();
        var tool = new ListDirectoryTool(provider);

        var result = await tool.ExecuteAsync("""{"path": "C:\\nonexistent_path_12345"}""");

        Assert.False(result.Success);
        Assert.Contains("不存在", result.Error);
    }

    [Fact]
    public void ListDirectoryTool_Metadata_ShouldBeCorrect()
    {
        var provider = new MockContextProvider();
        var tool = new ListDirectoryTool(provider);

        Assert.Equal("list_directory", tool.Name);
        Assert.Contains("path", tool.ParametersSchema);
        Assert.Contains("recursive", tool.ParametersSchema);
    }

    [Fact]
    public async Task ExecuteAsync_Chat_ShouldUseChatPrompt()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var registry = new ToolRegistry();

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, registry, _mockEventBus.Object, _mockLogger.Object);

        var task = new AgentTask
        {
            Title = "用户对话",
            Input = "什么是量子计算？",
            Type = TaskType.Chat,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.Chat }
        };

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
                Content = "量子计算是一种利用量子力学原理进行计算的技术..."
            });

        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.Contains("量子", result.Content);
        Assert.Contains("AI 助手", capturedMessages[0].Content);
    }

    [Fact]
    public async Task ExecuteAsync_WriteFile_ShouldUseWriteFilePrompt()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var registry = new ToolRegistry();

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, registry, _mockEventBus.Object, _mockLogger.Object);

        var task = new AgentTask
        {
            Title = "生成文件",
            Input = "帮我写一个 Hello World 的 Python 脚本",
            Type = TaskType.WriteFile,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.WriteFile }
        };

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
                Content = "**文件类型**: Python 脚本\n**文件名**: hello.py\n**内容**:\n```python\nprint('Hello World')\n```"
            });

        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.Contains("python", result.Content.ToLower());
        Assert.Contains("文件生成助手", capturedMessages[0].Content);
    }

    [Fact]
    public async Task ExecuteAsync_RunCommand_ShouldUseRunCommandPrompt()
    {
        var mockLlm = new Mock<ILlmProvider>();
        var registry = new ToolRegistry();

        var orchestrator = new AgentOrchestrator(
            mockLlm.Object, registry, _mockEventBus.Object, _mockLogger.Object);

        var task = new AgentTask
        {
            Title = "执行命令",
            Input = "怎么查看当前目录的文件？",
            Type = TaskType.RunCommand,
            Parameters = new Dictionary<string, object> { ["TaskType"] = TaskType.RunCommand }
        };

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
                Content = "**命令**: ls\n**作用**: 列出当前目录文件"
            });

        var result = await orchestrator.ExecuteAsync(task);

        Assert.True(result.Success);
        Assert.Contains("ls", result.Content);
        Assert.Contains("命令执行助手", capturedMessages[0].Content);
    }
}

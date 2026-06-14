using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Tools;
using DesktopMascot.Agent.Providers;
using Moq;

namespace DesktopMascot.Agent.Tests;

public class ScreenUnderstandingTests
{
    [Fact]
    public void DetectContentType_Code_ShouldDetectCode()
    {
        var result = ScreenPromptBuilder.DetectContentType("program.cs - Visual Studio", "devenv");
        Assert.Equal(ScreenContentType.Code, result);
    }

    [Fact]
    public void DetectContentType_Error_ShouldDetectError()
    {
        var result = ScreenPromptBuilder.DetectContentType("TypeError: Cannot read property", "devenv");
        Assert.Equal(ScreenContentType.Error, result);
    }

    [Fact]
    public void DetectContentType_WebPage_ShouldDetectWebPage()
    {
        var result = ScreenPromptBuilder.DetectContentType("GitHub - dotnet/runtime", "chrome");
        Assert.Equal(ScreenContentType.WebPage, result);
    }

    [Fact]
    public void DetectContentType_Terminal_ShouldDetectTerminal()
    {
        var result = ScreenPromptBuilder.DetectContentType("PowerShell", "WindowsTerminal");
        Assert.Equal(ScreenContentType.Terminal, result);
    }

    [Fact]
    public void DetectContentType_Document_ShouldDetectDocument()
    {
        var result = ScreenPromptBuilder.DetectContentType("报告.docx", "WINWORD");
        Assert.Equal(ScreenContentType.Document, result);
    }

    [Fact]
    public void DetectContentType_Data_ShouldDetectData()
    {
        var result = ScreenPromptBuilder.DetectContentType("销售数据.xlsx", "explorer");
        Assert.Equal(ScreenContentType.Data, result);
    }

    [Fact]
    public void BuildPrompt_WithContentType_ShouldContainTypeSpecificInfo()
    {
        var prompt = ScreenPromptBuilder.BuildPrompt(ScreenContentType.Code);
        Assert.Contains("代码", prompt);
    }

    [Fact]
    public void BuildPrompt_WithError_ShouldContainErrorInfo()
    {
        var prompt = ScreenPromptBuilder.BuildPrompt(ScreenContentType.Error);
        Assert.Contains("错误", prompt);
    }

    [Fact]
    public void BuildPrompt_WithUserHint_ShouldIncludeHint()
    {
        var prompt = ScreenPromptBuilder.BuildPrompt(ScreenContentType.Unknown, "帮我看看这个");
        Assert.Contains("帮我看看这个", prompt);
    }

    [Fact]
    public void EnhancedScreenResult_ShouldContainAllFields()
    {
        var result = new EnhancedScreenResult
        {
            Identification = "VS Code 编辑器",
            Understanding = "用户可能想编辑代码",
            ContentType = ScreenContentType.Code,
            DetectedLanguage = "csharp",
            Confidence = 0.85f
        };

        Assert.Equal("VS Code 编辑器", result.Identification);
        Assert.Equal(ScreenContentType.Code, result.ContentType);
        Assert.Equal(0.85f, result.Confidence);
    }

    [Fact]
    public void ScreenAction_ShouldContainRequiredFields()
    {
        var action = new ScreenAction
        {
            Name = "点击按钮",
            Description = "点击确认按钮",
            ActionType = "click",
            RiskLevel = "low"
        };

        Assert.Equal("click", action.ActionType);
        Assert.Equal("low", action.RiskLevel);
    }

    [Fact]
    public async Task ScreenActionExecutor_UnsupportedAction_ShouldReturnError()
    {
        var executor = new ScreenActionExecutor();
        var action = new ScreenAction { ActionType = "unknown_action" };

        var result = await executor.ExecuteActionAsync(action);

        Assert.False(result.Success);
        Assert.Contains("不支持", result.Error);
    }

    [Fact]
    public async Task ScreenActionExecutor_ClickWithoutCoords_ShouldReturnError()
    {
        var executor = new ScreenActionExecutor();
        var action = new ScreenAction
        {
            ActionType = "click",
            Parameters = new Dictionary<string, string>()
        };

        var result = await executor.ExecuteActionAsync(action);

        Assert.False(result.Success);
        Assert.Contains("缺少", result.Error);
    }

    [Fact]
    public async Task ScreenActionExecutor_TypeWithoutText_ShouldReturnError()
    {
        var executor = new ScreenActionExecutor();
        var action = new ScreenAction
        {
            ActionType = "type",
            Parameters = new Dictionary<string, string>()
        };

        var result = await executor.ExecuteActionAsync(action);

        Assert.False(result.Success);
        Assert.Contains("缺少", result.Error);
    }

    [Fact]
    public async Task ScreenActionExecutor_HotkeyWithoutKeys_ShouldReturnError()
    {
        var executor = new ScreenActionExecutor();
        var action = new ScreenAction
        {
            ActionType = "hotkey",
            Parameters = new Dictionary<string, string>()
        };

        var result = await executor.ExecuteActionAsync(action);

        Assert.False(result.Success);
        Assert.Contains("缺少", result.Error);
    }

    [Fact]
    public async Task ScreenActionExecutor_OpenUrlWithoutUrl_ShouldReturnError()
    {
        var executor = new ScreenActionExecutor();
        var action = new ScreenAction
        {
            ActionType = "open_url",
            Parameters = new Dictionary<string, string>()
        };

        var result = await executor.ExecuteActionAsync(action);

        Assert.False(result.Success);
        Assert.Contains("缺少", result.Error);
    }

    [Fact]
    public async Task ScreenActionExecutor_CopyWithoutText_ShouldReturnError()
    {
        var executor = new ScreenActionExecutor();
        var action = new ScreenAction
        {
            ActionType = "copy_text",
            Parameters = new Dictionary<string, string>()
        };

        var result = await executor.ExecuteActionAsync(action);

        Assert.False(result.Success);
        Assert.Contains("缺少", result.Error);
    }

    [Fact]
    public async Task ScreenActionExecutor_RunCommandWithoutCommand_ShouldReturnError()
    {
        var executor = new ScreenActionExecutor();
        var action = new ScreenAction
        {
            ActionType = "run_command",
            Parameters = new Dictionary<string, string>()
        };

        var result = await executor.ExecuteActionAsync(action);

        Assert.False(result.Success);
        Assert.Contains("缺少", result.Error);
    }
}

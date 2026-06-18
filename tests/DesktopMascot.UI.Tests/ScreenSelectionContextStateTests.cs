using DesktopMascot.UI.Services;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Tests;

public sealed class ScreenSelectionContextStateTests
{
    [Fact]
    public void From_FormatsConfirmedRegionForTaskContextCard()
    {
        var result = new ScreenSelectionResult
        {
            X = -1890,
            Y = 135,
            Width = 120,
            Height = 105,
            IsConfirmed = true
        };

        var state = ScreenSelectionContextState.From(result, "等待视觉理解");

        Assert.True(state.HasRegion);
        Assert.Equal("屏幕圈选区域", state.Title);
        Assert.Equal("屏幕坐标 -1890, 135", state.RegionText);
        Assert.Equal("120 x 105", state.SizeText);
        Assert.Equal("等待视觉理解", state.StatusText);
        Assert.Equal("将把该屏幕区域交给视觉理解：(-1890, 135) 120x105", state.DetailText);
    }

    [Fact]
    public void From_ReturnsEmptyStateForUnconfirmedRegion()
    {
        var result = new ScreenSelectionResult
        {
            X = 20,
            Y = 30,
            Width = 120,
            Height = 105,
            IsConfirmed = false
        };

        var state = ScreenSelectionContextState.From(result, "等待视觉理解");

        Assert.False(state.HasRegion);
        Assert.Equal("屏幕圈选", state.Title);
        Assert.Equal("暂无圈选区域", state.RegionText);
        Assert.Equal("等待 Ctrl+Shift+S 或点击圈选", state.SizeText);
    }

    [Fact]
    public void WithResult_UsesReadableScreenUnderstandJsonSummary()
    {
        var state = ScreenSelectionContextState.From(new ScreenSelectionResult
        {
            X = 12,
            Y = 24,
            Width = 320,
            Height = 180,
            IsConfirmed = true
        });

        var updated = state.WithResult(
            success: true,
            content: """
            {
              "identification": "这是一个 PowerShell 报错窗口",
              "understanding": "用户需要定位命令启动失败原因",
              "confidence": 0.86
            }
            """,
            error: null);

        Assert.True(updated.HasRegion);
        Assert.Equal("识别完成", updated.StatusText);
        Assert.Equal("识别：这是一个 PowerShell 报错窗口；理解：用户需要定位命令启动失败原因", updated.DetailText);
        Assert.Equal("屏幕坐标 12, 24", updated.RegionText);
        Assert.Equal("320 x 180", updated.SizeText);
    }

    [Fact]
    public void WithResult_ReportsFailureWithoutDroppingRegion()
    {
        var state = ScreenSelectionContextState.From(new ScreenSelectionResult
        {
            X = -100,
            Y = 40,
            Width = 200,
            Height = 120,
            IsConfirmed = true
        });

        var updated = state.WithResult(success: false, content: null, error: "视觉模型调用失败");

        Assert.True(updated.HasRegion);
        Assert.Equal("识别失败", updated.StatusText);
        Assert.Equal("视觉模型调用失败", updated.DetailText);
        Assert.Equal("屏幕坐标 -100, 40", updated.RegionText);
        Assert.Equal("200 x 120", updated.SizeText);
    }

    [Fact]
    public void WithResult_ExtractsSuggestedActionsFromScreenUnderstandJson()
    {
        var state = ScreenSelectionContextState.From(new ScreenSelectionResult
        {
            X = 12,
            Y = 24,
            Width = 320,
            Height = 180,
            IsConfirmed = true
        });

        var updated = state.WithResult(
            success: true,
            content: """
            {
              "identification": "这是一个报错窗口",
              "understanding": "需要排查命令启动失败",
              "suggestions": [
                "复制错误信息",
                "分析失败原因"
              ],
              "recommendedActions": [
                {
                  "name": "检查命令路径",
                  "description": "确认 mimo CLI 路径是否指向可执行文件",
                  "actionType": "inspect_file",
                  "riskLevel": "low"
                },
                {
                  "name": "重新运行命令",
                  "description": "使用正确入口重新启动 mimo",
                  "actionType": "run_command",
                  "riskLevel": "high"
                }
              ]
            }
            """,
            error: null);

        Assert.True(updated.HasSuggestedActions);
        Assert.Equal(4, updated.SuggestedActions.Count);
        Assert.Equal("复制错误信息", updated.SuggestedActions[0].Title);
        Assert.Equal("建议", updated.SuggestedActions[0].KindText);
        Assert.Equal("检查命令路径", updated.SuggestedActions[2].Title);
        Assert.Equal("inspect_file · low", updated.SuggestedActions[2].KindText);
        Assert.Equal("确认 mimo CLI 路径是否指向可执行文件", updated.SuggestedActions[2].PromptText);
        Assert.Equal("使用正确入口重新启动 mimo", updated.SuggestedActions[3].PromptText);
    }

    [Fact]
    public void WithResult_ExtractsExpandableDetailRowsFromScreenUnderstandJson()
    {
        var state = ScreenSelectionContextState.From(new ScreenSelectionResult
        {
            X = 12,
            Y = 24,
            Width = 320,
            Height = 180,
            IsConfirmed = true
        });

        var updated = state.WithResult(
            success: true,
            content: """
            {
              "identification": "这是一个 PowerShell 报错窗口",
              "understanding": "用户需要定位命令启动失败原因",
              "extractedText": "The specified executable is not a valid application",
              "contentType": "terminal",
              "confidence": 0.86,
              "screenshotPath": "C:\\tmp\\screen-area.png"
            }
            """,
            error: null);

        Assert.True(updated.HasDetailItems);
        Assert.Collection(
            updated.DetailItems,
            item =>
            {
                Assert.Equal("识别", item.Label);
                Assert.Equal("这是一个 PowerShell 报错窗口", item.Value);
            },
            item =>
            {
                Assert.Equal("理解", item.Label);
                Assert.Equal("用户需要定位命令启动失败原因", item.Value);
            },
            item =>
            {
                Assert.Equal("提取文本", item.Label);
                Assert.Equal("The specified executable is not a valid application", item.Value);
            },
            item =>
            {
                Assert.Equal("内容类型", item.Label);
                Assert.Equal("terminal", item.Value);
            },
            item =>
            {
                Assert.Equal("置信度", item.Label);
                Assert.Equal("86%", item.Value);
            },
            item =>
            {
                Assert.Equal("截图", item.Label);
                Assert.Equal("C:\\tmp\\screen-area.png", item.Value);
            });
    }

    [Fact]
    public void WithResult_ExtractsScreenshotEvidenceFromScreenUnderstandJson()
    {
        var state = ScreenSelectionContextState.From(new ScreenSelectionResult
        {
            X = 12,
            Y = 24,
            Width = 320,
            Height = 180,
            IsConfirmed = true
        });

        var updated = state.WithResult(
            success: true,
            content: """
            {
              "identification": "这是一个表格区域",
              "imagePath": "C:\\tmp\\screen-area.png"
            }
            """,
            error: null);

        Assert.True(updated.HasScreenshotEvidence);
        Assert.Equal("C:\\tmp\\screen-area.png", updated.ScreenshotPath);
        Assert.Equal("screen-area.png", updated.ScreenshotFileName);
    }

    [Fact]
    public void WithStatus_PreservesScreenshotEvidence()
    {
        var state = ScreenSelectionContextState.From(new ScreenSelectionResult
        {
            X = 12,
            Y = 24,
            Width = 320,
            Height = 180,
            IsConfirmed = true
        }).WithResult(
            success: true,
            content: """
            {
              "identification": "这是一个表格区域",
              "screenshotPath": "C:\\tmp\\screen-area.png"
            }
            """,
            error: null);

        var updated = state.WithStatus("等待后续操作");

        Assert.True(updated.HasScreenshotEvidence);
        Assert.Equal("C:\\tmp\\screen-area.png", updated.ScreenshotPath);
        Assert.Equal("screen-area.png", updated.ScreenshotFileName);
    }

    [Fact]
    public void WithResult_MarksScreenshotPreviewOnlyWhenFileExists()
    {
        var screenshotPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(screenshotPath, [137, 80, 78, 71]);

        try
        {
            var state = ScreenSelectionContextState.From(new ScreenSelectionResult
            {
                X = 12,
                Y = 24,
                Width = 320,
                Height = 180,
                IsConfirmed = true
            });

            var updated = state.WithResult(
                success: true,
                content: $$"""
                {
                  "identification": "这是一个表格区域",
                  "screenshotPath": "{{screenshotPath.Replace("\\", "\\\\")}}"
                }
                """,
                error: null);

            Assert.True(updated.HasScreenshotEvidence);
            Assert.True(updated.HasScreenshotPreview);
        }
        finally
        {
            File.Delete(screenshotPath);
        }
    }
}

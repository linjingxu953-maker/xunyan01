using DesktopMascot.Core.Enums;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Tests;

public sealed class ToolLauncherCatalogTests
{
    [Fact]
    public void DefaultItems_CoverCoreTaskEntrypointsAndToolFamilies()
    {
        var items = ToolLauncherCatalog.CreateDefaultItems();

        Assert.True(items.Count >= 30);
        Assert.Contains(items, item => item.TaskType == TaskType.ScreenUnderstand);
        Assert.Contains(items, item => item.TaskType == TaskType.ComputerUse);
        Assert.Contains(items, item => item.TaskType == TaskType.WriteFile);
        Assert.Contains(items, item => item.TaskType == TaskType.RunCommand);
        Assert.Contains(items, item => item.Id == "ocr");
        Assert.Contains(items, item => item.Id == "short_video");
    }

    [Fact]
    public void Filter_MatchesTitleDescriptionAndKeywords()
    {
        var items = ToolLauncherCatalog.CreateDefaultItems();

        var pdfResults = ToolLauncherCatalog.Filter(items, "PDF", ToolLauncherCatalog.AllCategory).ToList();
        var homeworkResults = ToolLauncherCatalog.Filter(items, "作业", ToolLauncherCatalog.AllCategory).ToList();

        Assert.Contains(pdfResults, item => item.Id == "pdf");
        Assert.Contains(homeworkResults, item => item.Id == "course_assist");
    }

    [Fact]
    public void Filter_RestrictsByCategory()
    {
        var items = ToolLauncherCatalog.CreateDefaultItems();

        var fileResults = ToolLauncherCatalog.Filter(items, string.Empty, "文件").ToList();

        Assert.NotEmpty(fileResults);
        Assert.All(fileResults, item => Assert.Equal("文件", item.Category));
        Assert.DoesNotContain(fileResults, item => item.Id == "browser_context");
    }

    [Fact]
    public void BuildLaunchPrompt_IncludesToolIdentityAndUserInstruction()
    {
        var item = ToolLauncherCatalog.CreateDefaultItems().First(item => item.Id == "security_scan");

        Assert.Contains("security_scan", item.LaunchPrompt);
        Assert.Contains(item.Title, item.LaunchPrompt);
        Assert.Contains("请说明目标", item.LaunchPrompt);
    }

    [Fact]
    public void DefaultItems_MarkHighFrequencyEntriesWithDirectLaunchModes()
    {
        var items = ToolLauncherCatalog.CreateDefaultItems();

        Assert.Equal(ToolLauncherLaunchMode.ScreenSelection, items.First(item => item.Id == "screen_understand").LaunchMode);
        Assert.Equal(ToolLauncherLaunchMode.ScreenSelection, items.First(item => item.Id == "ocr").LaunchMode);
        Assert.Equal(ToolLauncherLaunchMode.ComputerUsePanel, items.First(item => item.Id == "computer_use").LaunchMode);
        Assert.Equal(ToolLauncherLaunchMode.ComputerUsePanel, items.First(item => item.Id == "browser_automation").LaunchMode);
        Assert.Equal(ToolLauncherLaunchMode.FillPrompt, items.First(item => item.Id == "security_scan").LaunchMode);
    }

    [Fact]
    public void DefaultItems_MarkStructuredFormKindsForCommonToolGroups()
    {
        var items = ToolLauncherCatalog.CreateDefaultItems();

        Assert.Equal(ToolLauncherFormKind.Command, items.First(item => item.Id == "run_command").FormKind);
        Assert.Equal(ToolLauncherFormKind.Path, items.First(item => item.Id == "read_file").FormKind);
        Assert.Equal(ToolLauncherFormKind.Path, items.First(item => item.Id == "pdf").FormKind);
        Assert.Equal(ToolLauncherFormKind.Content, items.First(item => item.Id == "translate").FormKind);
        Assert.Equal(ToolLauncherFormKind.Content, items.First(item => item.Id == "course_assist").FormKind);
        Assert.Equal(ToolLauncherFormKind.None, items.First(item => item.Id == "screen_understand").FormKind);
    }
}

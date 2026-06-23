namespace DesktopMascot.UI.Tests;

public sealed class FloatingWindowXamlStructureTests
{
    [Fact]
    public void FloatingWindow_SettingsMode_HasDedicatedLargerGeometry()
    {
        var code = ReadWorkspaceFile("src/DesktopMascot.UI/Views/FloatingWindow.axaml.cs");

        Assert.Contains("SettingsWidth", code);
        Assert.Contains("SettingsHeight", code);
        Assert.Contains("nameof(FloatingWindowViewModel.IsSettingsPageVisible)", code);
    }

    [Fact]
    public void FloatingWindow_AppearanceSection_PromotesStatePreviewAndEditor()
    {
        var xaml = ReadWorkspaceFile("src/DesktopMascot.UI/Views/FloatingWindow.axaml");
        var selectedEditorIndex = xaml.IndexOf("Classes=\"selectedCharacterStateEditor\"", StringComparison.Ordinal);
        var previewItemsIndex = xaml.IndexOf(
            "ItemsSource=\"{Binding InlineSettings.CharacterStatePreviewItems}\"",
            StringComparison.Ordinal);
        var profileLibraryIndex = xaml.IndexOf("Text=\"角色库\"", StringComparison.Ordinal);

        Assert.True(selectedEditorIndex >= 0, "The selected state editor should have a stable style hook.");
        Assert.True(previewItemsIndex >= 0, "The state preview cards should be present in the inline appearance page.");
        Assert.True(profileLibraryIndex >= 0, "The appearance page should still expose the character profile library.");
        Assert.True(selectedEditorIndex < previewItemsIndex, "The state editor should sit before the preview cards.");
        Assert.True(previewItemsIndex < profileLibraryIndex, "The state preview cards should be visible before the profile library.");
    }

    [Fact]
    public void FloatingWindow_AppearanceSection_UsesFullWidthFlowBelowPreview()
    {
        var xaml = ReadWorkspaceFile("src/DesktopMascot.UI/Views/FloatingWindow.axaml");

        Assert.DoesNotContain("ColumnDefinitions=\"250,*\"", xaml);
        Assert.Contains("RowDefinitions=\"Auto,Auto\"", xaml);
        Assert.Contains("Grid.Row=\"1\"", xaml);
    }

    [Fact]
    public void ComputerUsePanel_HasDismissAction()
    {
        var xaml = ReadWorkspaceFile("src/DesktopMascot.UI/Views/Controls/ComputerUsePanel.axaml");

        Assert.Contains("HideComputerUsePanelCommand", xaml);
        Assert.Contains("收起", xaml);
    }

    [Fact]
    public void MciAudioPlaybackService_DoesNotLaunchExternalPlayer()
    {
        var code = ReadWorkspaceFile("src/DesktopMascot.UI/Services/MciAudioPlaybackService.cs");
        var wmpIndex = code.IndexOf("TryPlayWithWindowsMediaPlayer(fullPath)", StringComparison.Ordinal);
        var mciIndex = code.IndexOf("TryPlayWithMci(fullPath)", StringComparison.Ordinal);

        Assert.True(wmpIndex >= 0, "MP3 playback should first try in-process Windows Media Player.");
        Assert.True(mciIndex > wmpIndex, "MCI should stay as the second in-process fallback.");
        Assert.DoesNotContain("TryPlayWithShell", code);
        Assert.DoesNotContain("Process.Start", code);
        Assert.DoesNotContain("UseShellExecute", code);
    }

    [Fact]
    public void ComputerUsePanel_IsCompactAndScrollable()
    {
        var xaml = ReadWorkspaceFile("src/DesktopMascot.UI/Views/Controls/ComputerUsePanel.axaml");
        var styles = ReadWorkspaceFile("src/DesktopMascot.UI/Styles/AppStyles.axaml");

        Assert.Contains("MaxHeight=\"320\"", xaml);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", xaml);
        Assert.Contains("<Setter Property=\"MaxHeight\" Value=\"320\"/>", styles);
    }

    private static string ReadWorkspaceFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not locate workspace file '{relativePath}'.");
    }
}

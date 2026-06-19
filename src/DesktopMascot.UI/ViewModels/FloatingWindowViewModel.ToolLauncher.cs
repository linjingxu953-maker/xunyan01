using System.Text;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;

namespace DesktopMascot.UI.ViewModels;

public partial class FloatingWindowViewModel
{
    private Func<CancellationToken, Task<string?>>? _toolLauncherFilePicker;
    private Func<CancellationToken, Task<string?>>? _toolLauncherFolderPicker;

    public void SetToolLauncherPathPickers(
        Func<CancellationToken, Task<string?>>? filePicker,
        Func<CancellationToken, Task<string?>>? folderPicker)
    {
        _toolLauncherFilePicker = filePicker;
        _toolLauncherFolderPicker = folderPicker;
    }

    private void InitializeToolLauncher()
    {
        ToolLauncherItems.Clear();
        foreach (var item in ToolLauncherCatalog.CreateDefaultItems())
            ToolLauncherItems.Add(item);

        ToolLauncherCategories.Clear();
        foreach (var category in ToolLauncherCatalog.CreateCategories(ToolLauncherItems))
            ToolLauncherCategories.Add(category);

        SelectedToolCategory = ToolLauncherCatalog.AllCategory;
        RefreshToolLauncherItems();
    }

    [RelayCommand]
    private void ToggleToolLauncher()
    {
        IsToolLauncherVisible = !IsToolLauncherVisible;
        if (IsToolLauncherVisible)
            OpenToolLauncherPanel();
    }

    [RelayCommand]
    private void OpenToolLauncher()
    {
        IsToolLauncherVisible = true;
        OpenToolLauncherPanel();
    }

    public void SelectToolLauncherCategory(string? category)
    {
        SelectedToolCategory = string.IsNullOrWhiteSpace(category)
            ? ToolLauncherCatalog.AllCategory
            : category.Trim();
    }

    public void UseToolLauncherItem(ToolLauncherItem? item)
    {
        if (item is null)
            return;

        switch (item.LaunchMode)
        {
            case ToolLauncherLaunchMode.ScreenSelection:
                UseScreenSelectionToolLauncherItem(item);
                return;
            case ToolLauncherLaunchMode.ComputerUsePanel:
                UseComputerUseToolLauncherItem(item);
                return;
        }

        if (item.FormKind != ToolLauncherFormKind.None)
        {
            OpenToolLauncherForm(item);
            return;
        }

        InputText = item.LaunchPrompt;
        IsToolLauncherVisible = false;
        IsCharacterPanelVisible = false;
        IsChatVisible = true;
        IsChatPageVisible = true;
        IsSettingsPageVisible = false;
        TaskActionStatus = $"已选择工具入口：{item.Title}";
        StatusMessage = "补充目标后可直接发送。";
    }

    private void UseScreenSelectionToolLauncherItem(ToolLauncherItem item)
    {
        IsToolLauncherVisible = false;
        IsCharacterPanelVisible = false;
        IsChatVisible = true;
        IsChatDialogVisible = true;
        IsChatPageVisible = true;
        IsSettingsPageVisible = false;
        InputText = string.Empty;
        TaskActionStatus = $"已打开工具入口：{item.Title}";
        RequestScreenSelection();
    }

    private void UseComputerUseToolLauncherItem(ToolLauncherItem item)
    {
        InputText = item.LaunchPrompt;
        IsToolLauncherVisible = false;
        IsCharacterPanelVisible = false;
        IsChatVisible = true;
        IsChatDialogVisible = true;
        IsChatPageVisible = true;
        IsSettingsPageVisible = false;
        ResetComputerUsePanel(isVisible: true);
        AddComputerUseActionRecord(item.Title, "当前桌面", "待补充", "请在输入框描述桌面目标和允许范围。", DateTime.UtcNow);
        AddComputerUseLogRecord(item.Title, "工具入口已打开 Computer Use 控制面板。", "待补充", DateTime.UtcNow);
        TaskActionStatus = $"已打开 Computer Use 入口：{item.Title}";
        StatusMessage = "补充桌面目标后可发送，敏感动作仍会请求确认。";
    }

    public void ApplyToolLauncherForm()
    {
        if (SelectedToolLauncherFormItem is not { } item || !CanApplyToolLauncherForm)
            return;

        InputText = BuildToolLauncherFormPrompt(item);
        ClearToolLauncherForm();
        IsToolLauncherVisible = false;
        IsCharacterPanelVisible = false;
        IsChatVisible = true;
        IsChatDialogVisible = true;
        IsChatPageVisible = true;
        IsSettingsPageVisible = false;
        TaskActionStatus = $"已生成工具任务：{item.Title}";
        StatusMessage = "检查输入内容后可直接发送。";
    }

    public void CancelToolLauncherForm()
    {
        ClearToolLauncherForm();
        StatusMessage = "已取消工具参数填写。";
    }

    public async Task PickToolLauncherFilePathAsync(CancellationToken ct = default)
    {
        if (!IsToolLauncherPathForm)
            return;

        var path = _toolLauncherFilePicker is not null
            ? await _toolLauncherFilePicker(ct)
            : await PickToolLauncherFilePathFromOwnerAsync(ct);

        ApplyPickedToolLauncherPath(path, "文件");
    }

    public async Task PickToolLauncherFolderPathAsync(CancellationToken ct = default)
    {
        if (!IsToolLauncherPathForm)
            return;

        var path = _toolLauncherFolderPicker is not null
            ? await _toolLauncherFolderPicker(ct)
            : await PickToolLauncherFolderPathFromOwnerAsync(ct);

        ApplyPickedToolLauncherPath(path, "目录");
    }

    private void OpenToolLauncherPanel()
    {
        IsCharacterPanelVisible = false;
        IsChatVisible = true;
        IsChatDialogVisible = true;
        IsChatPageVisible = true;
        IsSettingsPageVisible = false;
        RefreshToolLauncherItems();
    }

    private void OpenToolLauncherForm(ToolLauncherItem item)
    {
        SelectedToolLauncherFormItem = item;
        ToolLauncherPrimaryInput = string.Empty;
        ToolLauncherSecondaryInput = string.Empty;
        ToolLauncherObjectiveInput = string.Empty;
        ToolLauncherOutputInput = string.Empty;
        IsToolLauncherVisible = true;
        IsCharacterPanelVisible = false;
        IsChatVisible = true;
        IsChatDialogVisible = true;
        IsChatPageVisible = true;
        IsSettingsPageVisible = false;
        TaskActionStatus = $"正在填写工具参数：{item.Title}";
        StatusMessage = "填写关键字段后生成任务输入。";
    }

    private void ClearToolLauncherForm()
    {
        SelectedToolLauncherFormItem = null;
        ToolLauncherPrimaryInput = string.Empty;
        ToolLauncherSecondaryInput = string.Empty;
        ToolLauncherObjectiveInput = string.Empty;
        ToolLauncherOutputInput = string.Empty;
    }

    private string BuildToolLauncherFormPrompt(ToolLauncherItem item)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"请使用工具入口 {item.ToolName}（{item.Title}）处理任务。");
        builder.AppendLine($"工具说明：{item.Instruction}");
        AppendFormLine(builder, ToolLauncherPrimaryLabel, ToolLauncherPrimaryInput);
        AppendFormLine(builder, ToolLauncherSecondaryLabel, ToolLauncherSecondaryInput);
        AppendFormLine(builder, "处理目标", ToolLauncherObjectiveInput);
        AppendFormLine(builder, "期望输出", ToolLauncherOutputInput);
        builder.AppendLine($"权限提示：{item.RiskText}");
        builder.Append("请按现有权限确认流程执行；如果信息不足，先向用户确认缺失字段。");
        return builder.ToString();
    }

    private static void AppendFormLine(StringBuilder builder, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            builder.AppendLine($"{label}：{value.Trim()}");
    }

    private async Task<string?> PickToolLauncherFilePathFromOwnerAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var owner = _inlineSettingsOwner;
        if (owner is null)
            return null;

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择工具输入文件",
            AllowMultiple = false
        });

        ct.ThrowIfCancellationRequested();
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private async Task<string?> PickToolLauncherFolderPathFromOwnerAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var owner = _inlineSettingsOwner;
        if (owner is null)
            return null;

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择工具输入目录",
            AllowMultiple = false
        });

        ct.ThrowIfCancellationRequested();
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    private void ApplyPickedToolLauncherPath(string? path, string kindText)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = $"已取消选择{kindText}。";
            return;
        }

        ToolLauncherPrimaryInput = path;
        TaskActionStatus = $"已选择{kindText}：{path}";
        StatusMessage = "检查参数后可填入输入框。";
    }

    private void RefreshToolLauncherItems()
    {
        FilteredToolLauncherItems.Clear();
        foreach (var item in ToolLauncherCatalog.Filter(ToolLauncherItems, ToolSearchText, SelectedToolCategory))
            FilteredToolLauncherItems.Add(item);

        NotifyToolLauncherResultStateChanged();
    }

    private void NotifyToolLauncherResultStateChanged()
    {
        OnPropertyChanged(nameof(HasToolLauncherResults));
        OnPropertyChanged(nameof(HasNoToolLauncherResults));
    }

    partial void OnToolSearchTextChanged(string value) => RefreshToolLauncherItems();

    partial void OnSelectedToolCategoryChanged(string value) => RefreshToolLauncherItems();

    partial void OnToolLauncherPrimaryInputChanged(string value) => OnPropertyChanged(nameof(CanApplyToolLauncherForm));

    partial void OnToolLauncherObjectiveInputChanged(string value) => OnPropertyChanged(nameof(CanApplyToolLauncherForm));
}

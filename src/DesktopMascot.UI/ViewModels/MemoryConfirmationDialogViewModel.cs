using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopMascot.Core.Memory;

namespace DesktopMascot.UI.ViewModels;

public sealed partial class MemoryConfirmationDialogViewModel : ObservableObject
{
    private readonly MemoryConfirmationRequest _request;

    [ObservableProperty] private string _title = "确认保存记忆";
    [ObservableProperty] private string _subtitle = "这条内容会影响后续回复和任务执行。";
    [ObservableProperty] private string _memoryTypeText = string.Empty;
    [ObservableProperty] private string _sourceText = string.Empty;
    [ObservableProperty] private string _reasonText = string.Empty;
    [ObservableProperty] private string _requestedAtText = string.Empty;
    [ObservableProperty] private string _contentDraft = string.Empty;
    [ObservableProperty] private string _statusText = "确认后会返回给 M30 记忆确认服务。";

    private MemoryConfirmationDialogViewModel(MemoryConfirmationRequest request)
    {
        _request = request;
    }

    public ObservableCollection<ConfirmationDetailItem> MetadataItems { get; } = new();

    public event EventHandler<MemoryDecision>? CloseRequested;

    public static MemoryConfirmationDialogViewModel FromRequest(MemoryConfirmationRequest request)
    {
        var vm = new MemoryConfirmationDialogViewModel(request)
        {
            MemoryTypeText = request.MemoryType.ToString(),
            SourceText = string.IsNullOrWhiteSpace(request.Source) ? "未指定" : request.Source,
            ReasonText = string.IsNullOrWhiteSpace(request.Reason) ? "系统建议保存这条记忆。" : request.Reason,
            RequestedAtText = request.RequestedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            ContentDraft = request.Content
        };

        vm.MetadataItems.Add(new ConfirmationDetailItem("请求 ID", request.RequestId));
        if (!string.IsNullOrWhiteSpace(request.TaskId))
        {
            vm.MetadataItems.Add(new ConfirmationDetailItem("任务 ID", request.TaskId));
        }

        foreach (var item in request.Metadata)
        {
            if (!string.IsNullOrWhiteSpace(item.Value))
            {
                vm.MetadataItems.Add(new ConfirmationDetailItem(item.Key, item.Value));
            }
        }

        return vm;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ContentDraft))
        {
            StatusText = "记忆内容不能为空。";
            return;
        }

        CloseRequested?.Invoke(this, MemoryDecision.Save);
    }

    [RelayCommand]
    private void TempOnly()
    {
        CloseRequested?.Invoke(this, MemoryDecision.TempOnly);
    }

    [RelayCommand]
    private void Reject()
    {
        CloseRequested?.Invoke(this, MemoryDecision.Reject);
    }

    public MemoryConfirmationResponse BuildResponse(MemoryDecision decision)
    {
        return new MemoryConfirmationResponse
        {
            RequestId = _request.RequestId,
            Decision = decision,
            EditedContent = decision == MemoryDecision.Save ? ContentDraft.Trim() : null
        };
    }
}

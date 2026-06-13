using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Security;

namespace DesktopMascot.UI.ViewModels;

public sealed partial class PermissionDialogViewModel : ObservableObject
{
    private readonly string _denyReason;

    [ObservableProperty] private string _title = "确认操作";
    [ObservableProperty] private string _subtitle = "请确认是否允许本次操作。";
    [ObservableProperty] private string _categoryText = "权限确认";
    [ObservableProperty] private string _riskText = "中风险";
    [ObservableProperty] private string _targetLabel = "目标";
    [ObservableProperty] private string _targetText = string.Empty;
    [ObservableProperty] private string _previewTitle = "详情";
    [ObservableProperty] private string _previewText = string.Empty;
    [ObservableProperty] private string _primaryActionText = "允许本次";
    [ObservableProperty] private bool _showAllowAlways = true;

    public ObservableCollection<ConfirmationDetailItem> Details { get; } = new();

    public event EventHandler<PermissionDecision>? CloseRequested;

    private PermissionDialogViewModel(string denyReason)
    {
        _denyReason = denyReason;
    }

    public static PermissionDialogViewModel FromPermissionRequest(PermissionRequest request)
    {
        var vm = new PermissionDialogViewModel("用户拒绝了权限请求。")
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? "确认权限请求" : request.Title,
            Subtitle = string.IsNullOrWhiteSpace(request.Description) ? "该操作需要你确认后继续。" : request.Description,
            CategoryText = GetPermissionCategory(request.Level),
            RiskText = string.IsNullOrWhiteSpace(request.Risk) ? GetPermissionCategory(request.Level) : request.Risk,
            TargetLabel = "操作目标",
            TargetText = string.IsNullOrWhiteSpace(request.Target) ? "未指定" : request.Target,
            PreviewTitle = "请求详情",
            PreviewText = BuildPermissionPreview(request),
            PrimaryActionText = request.Level >= PermissionLevel.L4_FileWrite ? "允许本次" : "允许",
            ShowAllowAlways = request.Level is >= PermissionLevel.L1_WindowTitle and <= PermissionLevel.L3_FileRead
        };

        vm.Details.Add(new ConfirmationDetailItem("权限等级", request.Level.ToString()));
        vm.Details.Add(new ConfirmationDetailItem("请求时间", request.RequestedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));

        foreach (var item in request.Metadata)
        {
            if (!string.IsNullOrWhiteSpace(item.Value))
            {
                vm.Details.Add(new ConfirmationDetailItem(item.Key, item.Value));
            }
        }

        return vm;
    }

    public static PermissionDialogViewModel FromMemoryRequest(MemoryConfirmRequest request)
    {
        var memory = request.ProposedMemory;
        var vm = new PermissionDialogViewModel("用户拒绝保存记忆。")
        {
            Title = "确认保存记忆",
            Subtitle = string.IsNullOrWhiteSpace(request.Reason) ? "这条内容会保存为后续可复用的记忆。" : request.Reason,
            CategoryText = "记忆保存",
            RiskText = "可长期影响回复",
            TargetLabel = "记忆键",
            TargetText = string.IsNullOrWhiteSpace(memory.Key) ? "未命名记忆" : memory.Key,
            PreviewTitle = "将保存的内容",
            PreviewText = string.IsNullOrWhiteSpace(memory.Content) ? "无内容" : memory.Content,
            PrimaryActionText = "保存记忆",
            ShowAllowAlways = false
        };

        vm.Details.Add(new ConfirmationDetailItem("记忆类型", memory.Type.ToString()));
        vm.Details.Add(new ConfirmationDetailItem("来源", string.IsNullOrWhiteSpace(memory.Source) ? "未指定" : memory.Source));
        vm.Details.Add(new ConfirmationDetailItem("请求时间", request.RequestedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));

        if (memory.Tags.Count > 0)
        {
            vm.Details.Add(new ConfirmationDetailItem("标签", string.Join(", ", memory.Tags.Select(x => $"{x.Key}:{x.Value}"))));
        }

        return vm;
    }

    public static PermissionDialogViewModel FromPermissionPromptRequest(PermissionPromptRequest request)
    {
        var scope = string.IsNullOrWhiteSpace(request.Scope) ? "未指定" : request.Scope;
        var target = string.IsNullOrWhiteSpace(request.CommandOrPath) ? scope : request.CommandOrPath;
        var vm = new PermissionDialogViewModel("用户拒绝了权限请求。")
        {
            Title = GetPromptTitle(request.PermissionType),
            Subtitle = string.IsNullOrWhiteSpace(request.Reason) ? "该工具调用需要你确认后继续。" : request.Reason,
            CategoryText = GetPromptCategory(request.PermissionType),
            RiskText = GetPromptRiskText(request.RiskLevel),
            TargetLabel = GetPromptTargetLabel(request.PermissionType),
            TargetText = target,
            PreviewTitle = "请求详情",
            PreviewText = BuildPermissionPromptPreview(request),
            PrimaryActionText = request.RiskLevel == PromptRiskLevel.Low ? "允许" : "允许本次",
            ShowAllowAlways = request.RiskLevel != PromptRiskLevel.High
        };

        vm.Details.Add(new ConfirmationDetailItem("权限类型", request.PermissionType.ToString()));
        vm.Details.Add(new ConfirmationDetailItem("权限范围", scope));
        vm.Details.Add(new ConfirmationDetailItem("风险等级", request.RiskLevel.ToString()));
        vm.Details.Add(new ConfirmationDetailItem("请求时间", request.RequestedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));

        if (!string.IsNullOrWhiteSpace(request.TaskId))
        {
            vm.Details.Add(new ConfirmationDetailItem("任务 ID", request.TaskId));
        }

        foreach (var item in request.Metadata)
        {
            if (!string.IsNullOrWhiteSpace(item.Value))
            {
                vm.Details.Add(new ConfirmationDetailItem(item.Key, item.Value));
            }
        }

        return vm;
    }

    [RelayCommand]
    private void AllowOnce()
    {
        CloseRequested?.Invoke(this, PermissionDecision.AllowOnce);
    }

    [RelayCommand]
    private void AllowAlways()
    {
        CloseRequested?.Invoke(this, PermissionDecision.AllowAlways);
    }

    [RelayCommand]
    private void Deny()
    {
        CloseRequested?.Invoke(this, PermissionDecision.Deny);
    }

    public string BuildReason(PermissionDecision decision)
    {
        return decision switch
        {
            PermissionDecision.AllowAlways => "用户选择始终允许。",
            PermissionDecision.AllowOnce => "用户选择允许本次。",
            PermissionDecision.Allow => "用户选择允许。",
            PermissionDecision.Deny => _denyReason,
            _ => _denyReason
        };
    }

    private static string GetPermissionCategory(PermissionLevel level) => level switch
    {
        PermissionLevel.L0_Chat => "普通聊天",
        PermissionLevel.L1_WindowTitle => "窗口读取",
        PermissionLevel.L2_ScreenBrowser => "屏幕/浏览器读取",
        PermissionLevel.L3_FileRead => "文件读取",
        PermissionLevel.L4_FileWrite => "文件写入",
        PermissionLevel.L5_CommandExec => "命令执行",
        PermissionLevel.L6_Forbidden => "禁止操作",
        _ => "权限确认"
    };

    private static string BuildPermissionPreview(PermissionRequest request)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            lines.Add(request.Description);
        }

        if (!string.IsNullOrWhiteSpace(request.Target))
        {
            lines.Add(string.Empty);
            lines.Add($"目标：{request.Target}");
        }

        if (!string.IsNullOrWhiteSpace(request.Risk))
        {
            lines.Add($"风险：{request.Risk}");
        }

        return lines.Count == 0 ? "该操作需要用户确认。" : string.Join(Environment.NewLine, lines);
    }

    private static string GetPromptTitle(PromptPermissionType type) => type switch
    {
        PromptPermissionType.FileRead => "确认读取文件",
        PromptPermissionType.FileWrite => "确认写入文件",
        PromptPermissionType.FileDelete => "确认删除文件",
        PromptPermissionType.CommandExecute => "确认执行命令",
        PromptPermissionType.ScreenCapture => "确认屏幕截图",
        PromptPermissionType.BrowserRead => "确认读取浏览器",
        PromptPermissionType.WindowRead => "确认读取窗口",
        PromptPermissionType.SelectedTextRead => "确认读取选中文本",
        PromptPermissionType.MemorySave => "确认保存记忆",
        PromptPermissionType.MemoryDelete => "确认删除记忆",
        PromptPermissionType.ApiCall => "确认 API 调用",
        PromptPermissionType.WebhookSend => "确认发送 Webhook",
        _ => "确认权限请求"
    };

    private static string GetPromptCategory(PromptPermissionType type) => type switch
    {
        PromptPermissionType.FileRead or PromptPermissionType.FileWrite or PromptPermissionType.FileDelete => "文件权限",
        PromptPermissionType.CommandExecute => "命令执行",
        PromptPermissionType.ScreenCapture or PromptPermissionType.BrowserRead or PromptPermissionType.WindowRead or
            PromptPermissionType.SelectedTextRead => "上下文读取",
        PromptPermissionType.MemorySave or PromptPermissionType.MemoryDelete => "记忆权限",
        PromptPermissionType.ApiCall or PromptPermissionType.WebhookSend => "外部调用",
        _ => "权限确认"
    };

    private static string GetPromptRiskText(PromptRiskLevel riskLevel) => riskLevel switch
    {
        PromptRiskLevel.Low => "低风险",
        PromptRiskLevel.Medium => "中风险",
        PromptRiskLevel.High => "高风险",
        _ => "风险未知"
    };

    private static string GetPromptTargetLabel(PromptPermissionType type) => type switch
    {
        PromptPermissionType.FileRead or PromptPermissionType.FileWrite or PromptPermissionType.FileDelete => "文件路径",
        PromptPermissionType.CommandExecute => "命令",
        PromptPermissionType.ApiCall => "API 范围",
        PromptPermissionType.WebhookSend => "Webhook",
        _ => "权限范围"
    };

    private static string BuildPermissionPromptPreview(PermissionPromptRequest request)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            lines.Add(request.Reason);
        }

        if (!string.IsNullOrWhiteSpace(request.Scope))
        {
            lines.Add(string.Empty);
            lines.Add($"范围：{request.Scope}");
        }

        if (!string.IsNullOrWhiteSpace(request.CommandOrPath))
        {
            lines.Add($"目标：{request.CommandOrPath}");
        }

        lines.Add($"风险：{GetPromptRiskText(request.RiskLevel)}");

        return string.Join(Environment.NewLine, lines);
    }
}

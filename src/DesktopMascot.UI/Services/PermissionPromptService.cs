using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Security;
using DesktopMascot.UI.ViewModels;
using DesktopMascot.UI.Views.Dialogs;

namespace DesktopMascot.UI.Services;

public sealed class PermissionPromptService : IConfirmationHandler, IMemoryConfirmationHandler, IPermissionPrompt
{
    private readonly Dictionary<(PromptPermissionType Type, string Scope), bool> _permissions = new();

    public async Task<PermissionResponse> RequestConfirmationAsync(PermissionRequest request, CancellationToken ct = default)
    {
        var vm = PermissionDialogViewModel.FromPermissionRequest(request);
        var decision = await ShowDialogAsync(vm, ct);

        return new PermissionResponse
        {
            RequestId = request.Id,
            Decision = decision,
            Reason = vm.BuildReason(decision)
        };
    }

    public async Task<bool> RequestConfirmationAsync(MemoryConfirmRequest request, CancellationToken ct = default)
    {
        var vm = PermissionDialogViewModel.FromMemoryRequest(request);
        var decision = await ShowDialogAsync(vm, ct);
        return decision != PermissionDecision.Deny;
    }

    public async Task<PermissionPromptResponse> PromptAsync(PermissionPromptRequest request, CancellationToken ct = default)
    {
        var key = (request.PermissionType, request.Scope);
        if (_permissions.TryGetValue(key, out var allowed) && allowed)
        {
            return new PermissionPromptResponse
            {
                RequestId = request.RequestId,
                Decision = PermissionDecision.AllowAlways
            };
        }

        var vm = PermissionDialogViewModel.FromPermissionPromptRequest(request);
        var decision = await ShowDialogAsync(vm, ct);

        if (decision == PermissionDecision.AllowAlways)
        {
            _permissions[key] = true;
        }

        return new PermissionPromptResponse
        {
            RequestId = request.RequestId,
            Decision = decision,
            DenyReason = decision == PermissionDecision.Deny ? vm.BuildReason(decision) : null
        };
    }

    public bool HasPermission(PromptPermissionType type, string scope)
    {
        return _permissions.TryGetValue((type, scope), out var allowed) && allowed;
    }

    public void RevokePermission(PromptPermissionType type, string scope)
    {
        _permissions.Remove((type, scope));
    }

    private static async Task<PermissionDecision> ShowDialogAsync(PermissionDialogViewModel viewModel, CancellationToken ct)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return await ShowDialogOnUiThreadAsync(viewModel, ct);
        }

        var completion = new TaskCompletionSource<PermissionDecision>();

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                completion.SetResult(await ShowDialogOnUiThreadAsync(viewModel, ct));
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        return await completion.Task.WaitAsync(ct);
    }

    private static async Task<PermissionDecision> ShowDialogOnUiThreadAsync(PermissionDialogViewModel viewModel, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return PermissionDecision.Deny;

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return PermissionDecision.Deny;

        var dialog = new PermissionDialog
        {
            DataContext = viewModel
        };

        var owner = desktop.MainWindow;
        if (owner is null)
            return PermissionDecision.Deny;

        var decision = await dialog.ShowDialog<PermissionDecision?>(owner);
        return decision ?? PermissionDecision.Deny;
    }
}

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DesktopMascot.Core.Memory;
using DesktopMascot.UI.ViewModels;
using DesktopMascot.UI.Views.Dialogs;

namespace DesktopMascot.UI.Services;

public sealed class MemoryConfirmationPromptService : IMemoryConfirmationPrompt
{
    public async Task<MemoryConfirmationResponse> PromptAsync(
        MemoryConfirmationRequest request,
        CancellationToken ct = default)
    {
        var viewModel = MemoryConfirmationDialogViewModel.FromRequest(request);
        var decision = await ShowDialogAsync(viewModel, ct);
        return viewModel.BuildResponse(decision);
    }

    private static async Task<MemoryDecision> ShowDialogAsync(
        MemoryConfirmationDialogViewModel viewModel,
        CancellationToken ct)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return await ShowDialogOnUiThreadAsync(viewModel, ct);
        }

        var completion = new TaskCompletionSource<MemoryDecision>();

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

    private static async Task<MemoryDecision> ShowDialogOnUiThreadAsync(
        MemoryConfirmationDialogViewModel viewModel,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return MemoryDecision.Reject;

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return MemoryDecision.Reject;

        var owner = desktop.MainWindow;
        if (owner is null)
            return MemoryDecision.Reject;

        var dialog = new MemoryConfirmationDialog
        {
            DataContext = viewModel
        };

        var decision = await dialog.ShowDialog<MemoryDecision?>(owner);
        return decision ?? MemoryDecision.Reject;
    }
}

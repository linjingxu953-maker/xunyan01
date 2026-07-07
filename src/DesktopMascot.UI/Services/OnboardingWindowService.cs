using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Services;
using DesktopMascot.UI.ViewModels;
using DesktopMascot.UI.Views;

namespace DesktopMascot.UI.Services;

public sealed class OnboardingWindowService : IOnboardingWindowService
{
    private readonly IConfigurationManager _configurationManager;
    private readonly ISettingsDiagnosticsService _diagnosticsService;
    private readonly ISettingsService? _settingsService;
    private OnboardingWindow? _window;

    public OnboardingWindowService(
        IConfigurationManager configurationManager,
        ISettingsDiagnosticsService diagnosticsService,
        ISettingsService? settingsService = null)
    {
        _configurationManager = configurationManager;
        _diagnosticsService = diagnosticsService;
        _settingsService = settingsService;
    }

    public async Task ShowOnboardingWindowAsync(CancellationToken ct = default)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            var completion = new TaskCompletionSource();
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    await ShowOnboardingWindowAsync(ct);
                    completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            });

            await completion.Task;
            return;
        }

        if (_window is { IsVisible: true })
        {
            _window.Activate();
            return;
        }

        var viewModel = new OnboardingWindowViewModel(_configurationManager, _diagnosticsService, _settingsService);
        await viewModel.LoadAsync(ct);

        var window = new OnboardingWindow
        {
            DataContext = viewModel
        };

        _window = window;

        void CloseFromViewModel(object? sender, EventArgs e)
        {
            if (window.IsVisible)
            {
                window.Close();
            }
        }

        void Closed(object? sender, EventArgs e)
        {
            viewModel.Completed -= CloseFromViewModel;
            viewModel.Skipped -= CloseFromViewModel;
            window.Closed -= Closed;

            if (ReferenceEquals(_window, window))
            {
                _window = null;
            }
        }

        viewModel.Completed += CloseFromViewModel;
        viewModel.Skipped += CloseFromViewModel;
        window.Closed += Closed;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        window.Activate();
    }
}

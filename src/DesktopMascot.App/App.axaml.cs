using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DesktopMascot.App.Services;
using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Logging;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Security;
using DesktopMascot.Core.Services;
using DesktopMascot.UI.Services;
using DesktopMascot.UI.ViewModels;
using DesktopMascot.UI.Views;

namespace DesktopMascot.App;

public partial class App : Application
{
    private static IHost? _host;
    private static ApplicationCoordinator? _coordinator;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        // 创建 Host 并注册服务
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // 注册所有应用服务（M22）
                services.AddAppServices();

                // 注册 UI 服务（保留原有注册）
                services.AddSingleton<IWindowPlacementStore, JsonWindowPlacementStore>();
                services.AddSingleton<IMascotCharacterStore, JsonMascotCharacterStore>();
                services.AddSingleton<ICharacterImageService, CharacterImageService>();
                services.AddSingleton<ICharacterAssetImportService, CharacterAssetImportService>();
                services.AddSingleton<ITaskResultActionService, TaskResultActionService>();
                services.AddSingleton<IAudioPlaybackService, MciAudioPlaybackService>();
                services.AddSingleton<ITextToSpeechPreviewService, TextToSpeechPreviewService>();
                services.AddSingleton<IVoiceInputService, WindowsVoiceInputService>();
                services.AddSingleton<ComputerUseControlService>();
                services.AddSingleton<IComputerUseControlService>(sp => sp.GetRequiredService<ComputerUseControlService>());
                services.AddSingleton<PermissionPromptService>();
                services.AddSingleton<IConfirmationHandler>(sp => sp.GetRequiredService<PermissionPromptService>());
                services.AddSingleton<IMemoryConfirmationHandler>(sp => sp.GetRequiredService<PermissionPromptService>());
                services.AddSingleton<IPermissionPrompt>(sp => sp.GetRequiredService<PermissionPromptService>());
                services.AddSingleton<IMemoryConfirmationPrompt, MemoryConfirmationPromptService>();
                services.AddSingleton<ISettingsWindowService, SettingsWindowService>();
                services.AddSingleton<ISettingsDiagnosticsService, SettingsDiagnosticsService>();
                services.AddSingleton<IOnboardingWindowService, OnboardingWindowService>();
                services.AddSingleton<IGlobalHotkeyService, WindowsGlobalHotkeyService>();
                services.AddSingleton<DesktopShellService>();

                // 注册 ViewModel
                services.AddSingleton<FloatingWindowViewModel>();
                services.AddTransient<OnboardingWindowViewModel>();

                // 注册 View
                services.AddSingleton<FloatingWindow>();
                services.AddTransient<OnboardingWindow>();
            })
            .Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 初始化服务协调器
            _coordinator = _host.Services.GetRequiredService<ApplicationCoordinator>();
            
            try
            {
                await _coordinator.InitializeAsync();
            }
            catch (Exception ex)
            {
                var logger = _host.Services.GetRequiredService<ILogger>();
                logger.Error($"应用初始化失败: {ex.Message}", exception: ex);
            }

            await ShowOnboardingIfNeededAsync(desktop);

            // 创建并显示主窗口
            var window = _host.Services.GetRequiredService<FloatingWindow>();
            var vm = _host.Services.GetRequiredService<FloatingWindowViewModel>();
            var desktopShell = _host.Services.GetRequiredService<DesktopShellService>();

            window.DataContext = vm;
            desktop.MainWindow = window;
            desktopShell.Attach(desktop, window, vm);
            
            // 退出时清理资源
            desktop.Exit += async (_, _) =>
            {
                if (_coordinator != null)
                {
                    await _coordinator.DisposeAsync();
                }
                _host?.Dispose();
            };
            
            window.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task ShowOnboardingIfNeededAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_host is null)
            return;

        var configurationManager = _host.Services.GetRequiredService<IConfigurationManager>();
        var settings = await configurationManager.GetAppSettingsAsync();

        if (settings.OnboardingCompleted)
            return;

        var window = _host.Services.GetRequiredService<OnboardingWindow>();
        var viewModel = _host.Services.GetRequiredService<OnboardingWindowViewModel>();
        await viewModel.LoadAsync();

        var completion = new TaskCompletionSource();

        void Finish(object? sender, EventArgs e)
        {
            completion.TrySetResult();

            if (window.IsVisible)
            {
                window.Close();
            }
        }

        void Closed(object? sender, EventArgs e)
        {
            completion.TrySetResult();
        }

        viewModel.Completed += Finish;
        viewModel.Skipped += Finish;
        window.Closed += Closed;

        try
        {
            window.DataContext = viewModel;
            desktop.MainWindow = window;
            window.Show();
            await completion.Task;
        }
        finally
        {
            viewModel.Completed -= Finish;
            viewModel.Skipped -= Finish;
            window.Closed -= Closed;
        }
    }
}

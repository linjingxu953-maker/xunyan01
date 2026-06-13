using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DesktopMascot.UI.ViewModels;
using DesktopMascot.UI.Views;

namespace DesktopMascot.UI.Services;

public sealed class DesktopShellService : IDisposable
{
    private readonly IWindowPlacementStore _placementStore;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsWindowService _settingsWindowService;
    private readonly DispatcherTimer _saveTimer;
    private FloatingWindow? _window;
    private FloatingWindowViewModel? _viewModel;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private TrayIcon? _trayIcon;
    private bool _isExiting;

    public DesktopShellService(
        IWindowPlacementStore placementStore,
        IGlobalHotkeyService hotkeyService,
        ISettingsWindowService settingsWindowService)
    {
        _placementStore = placementStore;
        _hotkeyService = hotkeyService;
        _settingsWindowService = settingsWindowService;
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveWindowPlacement();
        };
    }

    public void Attach(
        IClassicDesktopStyleApplicationLifetime desktop,
        FloatingWindow window,
        FloatingWindowViewModel viewModel)
    {
        _desktop = desktop;
        _window = window;
        _viewModel = viewModel;

        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        RestoreWindowPlacement();
        CreateTrayIcon();
        _hotkeyService.RegisterDefaultHotkey();

        viewModel.HideRequested += OnHideRequested;
        viewModel.ExitRequested += OnExitRequested;
        viewModel.SettingsRequested += OnSettingsRequested;
        viewModel.AppearanceSettingsRequested += OnAppearanceSettingsRequested;
        window.PositionChanged += OnWindowPositionChanged;
        window.Resized += OnWindowResized;
        window.Closing += OnWindowClosing;
    }

    public void Dispose()
    {
        _saveTimer.Stop();
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.Dispose();
        _trayIcon?.Dispose();

        if (_window is not null)
        {
            _window.PositionChanged -= OnWindowPositionChanged;
            _window.Resized -= OnWindowResized;
            _window.Closing -= OnWindowClosing;
        }

        if (_viewModel is not null)
        {
            _viewModel.HideRequested -= OnHideRequested;
            _viewModel.ExitRequested -= OnExitRequested;
            _viewModel.SettingsRequested -= OnSettingsRequested;
            _viewModel.AppearanceSettingsRequested -= OnAppearanceSettingsRequested;
        }
    }

    private void CreateTrayIcon()
    {
        var showItem = new NativeMenuItem
        {
            Header = "显示/隐藏"
        };
        showItem.Click += (_, _) => ToggleWindowFromTray();

        var openChatItem = new NativeMenuItem
        {
            Header = $"唤起输入 ({_hotkeyService.DisplayText})"
        };
        openChatItem.Click += (_, _) => ShowWindow(openChat: true);

        var settingsItem = new NativeMenuItem
        {
            Header = "设置"
        };
        settingsItem.Click += (_, _) => _settingsWindowService.ShowSettingsWindow();

        var exitItem = new NativeMenuItem
        {
            Header = "退出"
        };
        exitItem.Click += (_, _) => ExitApplication();

        var menu = new NativeMenu();
        menu.Items.Add(showItem);
        menu.Items.Add(openChatItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            Icon = TrayIconFactory.CreateDefaultIcon(),
            ToolTipText = $"DesktopMascot - {_hotkeyService.DisplayText} 唤起",
            Menu = menu,
            IsVisible = true
        };
        _trayIcon.Clicked += (_, _) => ToggleWindowFromTray();
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        ShowWindow(openChat: true);
    }

    private void OnHideRequested(object? sender, EventArgs e)
    {
        HideWindow();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        ExitApplication();
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        _settingsWindowService.ShowSettingsWindow();
    }

    private void OnAppearanceSettingsRequested(object? sender, EventArgs e)
    {
        _settingsWindowService.ShowSettingsWindow("appearance");
    }

    private void OnWindowPositionChanged(object? sender, PixelPointEventArgs e)
    {
        ScheduleSave();
    }

    private void OnWindowResized(object? sender, WindowResizedEventArgs e)
    {
        ScheduleSave();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowPlacement();

        if (_isExiting)
            return;

        e.Cancel = true;
        HideWindow();
    }

    private void ToggleWindowFromTray()
    {
        if (_window?.IsVisible == true)
        {
            HideWindow();
            return;
        }

        ShowWindow(openChat: false);
    }

    private void ShowWindow(bool openChat)
    {
        if (_window is null)
            return;

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        if (openChat)
        {
            _viewModel?.OpenChatPanel();
        }

        _window.Activate();
        _window.FocusInput();
    }

    private void HideWindow()
    {
        if (_window is null)
            return;

        SaveWindowPlacement();
        _window.Hide();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        SaveWindowPlacement();
        _trayIcon?.Dispose();
        _desktop?.Shutdown();
    }

    private void RestoreWindowPlacement()
    {
        if (_window is null)
            return;

        var state = _placementStore.Load();
        if (state is null || !IsReasonablePosition(state))
            return;

        _window.Position = new PixelPoint(state.X, state.Y);
        _window.Width = Math.Clamp(state.Width, 180, 700);
        _window.Height = Math.Clamp(state.Height, 240, 700);
    }

    private void ScheduleSave()
    {
        if (_isExiting)
            return;

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveWindowPlacement()
    {
        if (_window is null)
            return;

        _placementStore.Save(new WindowPlacementState
        {
            X = _window.Position.X,
            Y = _window.Position.Y,
            Width = _window.Width,
            Height = _window.Height
        });
    }

    private static bool IsReasonablePosition(WindowPlacementState state)
    {
        return state.X is > -10000 and < 10000 &&
               state.Y is > -10000 and < 10000 &&
               state.Width is >= 180 and <= 1000 &&
               state.Height is >= 240 and <= 1000;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Runtime.InteropServices;
using DesktopMascot.UI.ViewModels;
using DesktopMascot.UI.Views;

namespace DesktopMascot.UI.Services;

public sealed class DesktopShellService : IDisposable
{
    private const double CollapsedWindowWidth = 180;
    private const double CollapsedWindowHeight = 240;

    private readonly IWindowPlacementStore _placementStore;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ScreenSelectionOverlayService _screenSelectionOverlayService = new();
    private readonly DispatcherTimer _saveTimer;
    private FloatingWindow? _window;
    private FloatingWindowViewModel? _viewModel;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _openChatMenuItem;
    private NativeMenuItem? _screenSelectionMenuItem;
    private bool _isExiting;
    private bool _isSelectingScreenRegion;

    public DesktopShellService(
        IWindowPlacementStore placementStore,
        IGlobalHotkeyService hotkeyService)
    {
        _placementStore = placementStore;
        _hotkeyService = hotkeyService;
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _hotkeyService.ScreenSelectionHotkeyPressed += OnScreenSelectionHotkeyPressed;
        _hotkeyService.HotkeysChanged += OnHotkeysChanged;

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
        viewModel.CollapseChatPanel();
        RestoreWindowPlacement();
        CreateTrayIcon();
        _hotkeyService.RegisterDefaultHotkey();

        viewModel.HideRequested += OnHideRequested;
        viewModel.ExitRequested += OnExitRequested;
        viewModel.ScreenSelectionRequested += OnScreenSelectionRequested;
        window.PositionChanged += OnWindowPositionChanged;
        window.Resized += OnWindowResized;
        window.Closing += OnWindowClosing;
    }

    public void Dispose()
    {
        _saveTimer.Stop();
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.ScreenSelectionHotkeyPressed -= OnScreenSelectionHotkeyPressed;
        _hotkeyService.HotkeysChanged -= OnHotkeysChanged;
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
            _viewModel.ScreenSelectionRequested -= OnScreenSelectionRequested;
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
        _openChatMenuItem = openChatItem;

        var screenSelectionItem = new NativeMenuItem
        {
            Header = $"圈选屏幕 ({_hotkeyService.ScreenSelectionDisplayText})"
        };
        screenSelectionItem.Click += (_, _) => StartScreenSelectionFromShell();
        _screenSelectionMenuItem = screenSelectionItem;

        var settingsItem = new NativeMenuItem
        {
            Header = "设置"
        };
        settingsItem.Click += (_, _) => ShowSettingsFromShell();

        var exitItem = new NativeMenuItem
        {
            Header = "退出"
        };
        exitItem.Click += (_, _) => ExitApplication();

        var menu = new NativeMenu();
        menu.Items.Add(showItem);
        menu.Items.Add(openChatItem);
        menu.Items.Add(screenSelectionItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            Icon = TrayIconFactory.CreateDefaultIcon(),
            ToolTipText = $"寻研01 - {_hotkeyService.DisplayText} 唤起",
            Menu = menu,
            IsVisible = true
        };
        _trayIcon.Clicked += (_, _) => ToggleWindowFromTray();
        RefreshTrayHotkeyText();
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        ShowWindow(openChat: true);
    }

    private void OnScreenSelectionHotkeyPressed(object? sender, EventArgs e)
    {
        StartScreenSelectionFromShell();
    }

    private void OnHotkeysChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshTrayHotkeyText);
    }

    private void StartScreenSelectionFromShell()
    {
        if (_viewModel is null || _isSelectingScreenRegion || !_viewModel.CanStartScreenSelection)
            return;

        _viewModel.OpenChatPanel();
        _viewModel.RequestScreenSelection();
    }

    private void RefreshTrayHotkeyText()
    {
        if (_openChatMenuItem is not null)
        {
            _openChatMenuItem.Header = $"唤起输入 ({_hotkeyService.DisplayText})";
        }

        if (_screenSelectionMenuItem is not null)
        {
            _screenSelectionMenuItem.Header = $"圈选屏幕 ({_hotkeyService.ScreenSelectionDisplayText})";
        }

        if (_trayIcon is not null)
        {
            _trayIcon.ToolTipText = $"寻研01 - {_hotkeyService.DisplayText} 唤起";
        }
    }

    private void OnHideRequested(object? sender, EventArgs e)
    {
        HideWindow();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        ExitApplication();
    }

    private void ShowSettingsFromShell(string? section = null)
    {
        ShowWindow(openChat: false);
        _viewModel?.OpenSettingsPanel(section);
        EnsureWindowOnScreen();
        _window?.Activate();
    }

    private async void OnScreenSelectionRequested(object? sender, EventArgs e)
    {
        if (_isSelectingScreenRegion || _viewModel is null)
            return;

        _isSelectingScreenRegion = true;
        var shouldRestoreWindow = _window?.IsVisible == true;

        try
        {
            if (_window is not null && _window.IsVisible)
            {
                SaveWindowPlacement();
                _window.Hide();
            }

            var result = await _screenSelectionOverlayService.SelectRegionAsync(_window);
            if (result is { HasRegion: true })
            {
                ShowWindow(openChat: true);
                await _viewModel.AnalyzeSelectedScreenRegionAsync(result);
            }
            else
            {
                if (shouldRestoreWindow)
                {
                    ShowWindow(openChat: true);
                }

                _viewModel.CancelScreenSelection();
            }
        }
        finally
        {
            _isSelectingScreenRegion = false;
        }
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

        EnsureWindowOnScreen();
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

        var width = CollapsedWindowWidth;
        var height = CollapsedWindowHeight;

        _window.Width = width;
        _window.Height = height;
        _window.Position = GetDefaultWindowPosition(width, height);
    }

    private void EnsureWindowOnScreen()
    {
        if (_window is null)
            return;

        _window.Position = ClampWindowPosition(_window.Position, _window.Width, _window.Height);
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

        var position = _window.Position;
        var width = _window.Width;
        var height = _window.Height;

        if (_viewModel?.IsChatDialogVisible == true)
        {
            position = GetDefaultWindowPosition(CollapsedWindowWidth, CollapsedWindowHeight);
            width = CollapsedWindowWidth;
            height = CollapsedWindowHeight;
        }

        _placementStore.Save(new WindowPlacementState
        {
            X = position.X,
            Y = position.Y,
            Width = width,
            Height = height
        });
    }

    private PixelPoint ClampWindowPosition(PixelPoint desiredPosition, double width, double height)
    {
        var screen = FindBestScreen(desiredPosition);
        if (screen is null)
            return desiredPosition;

        return ClampWindowPositionToScreen(desiredPosition, width, height, screen);
    }

    private PixelPoint GetDefaultWindowPosition(double width, double height)
    {
        var screen = _window?.Screens.Primary ?? _window?.Screens.All.FirstOrDefault();
        if (screen is not null)
        {
            var scaling = GetScreenScaling(screen);
            var pixelWidth = Math.Max(1, (int)Math.Ceiling(width * scaling));
            var pixelHeight = Math.Max(1, (int)Math.Ceiling(height * scaling));
            var area = screen.WorkingArea;
            var x = area.X + area.Width - pixelWidth - 24;
            var y = area.Y + area.Height - pixelHeight - 32;

            return ClampWindowPositionToScreen(new PixelPoint(x, y), width, height, screen);
        }

        if (TryGetWindowsPrimaryWorkArea(out var windowsArea))
        {
            var defaultX = windowsArea.Right - (int)Math.Ceiling(width) - 24;
            var defaultY = windowsArea.Bottom - (int)Math.Ceiling(height) - 32;
            return new PixelPoint(
                Math.Max(windowsArea.Left, defaultX),
                Math.Max(windowsArea.Top, defaultY));
        }

        return _window?.Position ?? new PixelPoint(80, 80);
    }

    private PixelPoint ClampWindowPositionToScreen(PixelPoint desiredPosition, double width, double height, Screen screen)
    {
        var area = screen.WorkingArea;
        var scaling = GetScreenScaling(screen);
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(width * scaling));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(height * scaling));
        var maxX = area.X + Math.Max(0, area.Width - pixelWidth);
        var maxY = area.Y + Math.Max(0, area.Height - pixelHeight);

        return new PixelPoint(
            ClampToRange(desiredPosition.X, area.X, maxX),
            ClampToRange(desiredPosition.Y, area.Y, maxY));
    }

    private Screen? FindBestScreen(PixelPoint point)
    {
        if (_window is null)
            return null;

        var screens = _window.Screens;
        return screens.ScreenFromPoint(point) ??
               screens.All.OrderBy(screen => GetDistanceSquared(point, screen.WorkingArea)).FirstOrDefault();
    }

    private static double GetScreenScaling(Screen screen)
    {
        return screen.Scaling > 0 ? screen.Scaling : 1;
    }

    private static double GetDistanceSquared(PixelPoint point, PixelRect rect)
    {
        var left = rect.X;
        var right = rect.X + rect.Width;
        var top = rect.Y;
        var bottom = rect.Y + rect.Height;
        var dx = point.X < left ? left - point.X : point.X > right ? point.X - right : 0;
        var dy = point.Y < top ? top - point.Y : point.Y > bottom ? point.Y - bottom : 0;

        return (double)dx * dx + (double)dy * dy;
    }

    private static int ClampToRange(int value, int min, int max)
    {
        return max < min ? min : Math.Clamp(value, min, max);
    }

    private static bool TryGetWindowsPrimaryWorkArea(out NativeRect workArea)
    {
        workArea = default;
        return OperatingSystem.IsWindows() &&
               SystemParametersInfo(0x0030, 0, ref workArea, 0);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref NativeRect rect, uint updateIni);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

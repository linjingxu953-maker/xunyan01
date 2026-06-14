using Avalonia.Controls;
using Avalonia.Input;
using Avalonia;
using Avalonia.Platform;
using DesktopMascot.UI.Services;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Views;

public partial class ScreenSelectionOverlayWindow : Window
{
    private readonly TaskCompletionSource<ScreenSelectionResult?> _completion = new();
    private bool _isCompleted;

    public ScreenSelectionOverlayWindow()
    {
        InitializeComponent();
        Opened += (_, _) => Focus();
        Closed += OnClosed;
    }

    public Task<ScreenSelectionResult?> WaitForSelectionAsync() => _completion.Task;

    private ScreenSelectionViewModel? ViewModel => DataContext as ScreenSelectionViewModel;

    public void ConfigureForScreen(Screen screen)
    {
        var scaling = screen.Scaling > 0 ? screen.Scaling : 1;
        WindowState = WindowState.Normal;
        Position = new PixelPoint(screen.Bounds.X, screen.Bounds.Y);
        Width = Math.Max(1, screen.Bounds.Width / scaling);
        Height = Math.Max(1, screen.Bounds.Height / scaling);
        ViewModel?.SetScreenTransform(screen.Bounds, scaling);
    }

    private void SelectionSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(SelectionSurface).Properties.IsLeftButtonPressed)
            return;

        ViewModel?.Begin(GetClampedPosition(e));
        e.Pointer.Capture(SelectionSurface);
        e.Handled = true;
    }

    private void SelectionSurface_PointerMoved(object? sender, PointerEventArgs e)
    {
        ViewModel?.Update(GetClampedPosition(e));
    }

    private void SelectionSurface_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;

        e.Pointer.Capture(null);
        var result = ViewModel?.Complete(GetClampedPosition(e));
        if (result is { HasRegion: true })
        {
            Complete(result);
        }
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Cancel();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.Space)
        {
            var result = ViewModel?.ConfirmCurrent();
            if (result is { HasRegion: true })
            {
                Complete(result);
                e.Handled = true;
            }
        }
    }

    private void Complete(ScreenSelectionResult result)
    {
        if (_isCompleted)
            return;

        _isCompleted = true;
        _completion.TrySetResult(result);
        Close();
    }

    private void Cancel()
    {
        if (_isCompleted)
            return;

        _isCompleted = true;
        _completion.TrySetResult(null);
        Close();
    }

    private Point GetClampedPosition(PointerEventArgs e)
    {
        var point = e.GetPosition(SelectionSurface);
        var bounds = SelectionSurface.Bounds;

        return new Point(
            Math.Clamp(point.X, 0, Math.Max(0, bounds.Width)),
            Math.Clamp(point.Y, 0, Math.Max(0, bounds.Height)));
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_isCompleted)
            return;

        _isCompleted = true;
        _completion.TrySetResult(null);
    }
}

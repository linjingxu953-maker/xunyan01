using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using DesktopMascot.UI.ViewModels;
using DesktopMascot.UI.Views;

namespace DesktopMascot.UI.Services;

public sealed class ScreenSelectionOverlayService
{
    public async Task<ScreenSelectionResult?> SelectRegionAsync(Window? owner = null)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return await Dispatcher.UIThread.InvokeAsync(() => SelectRegionOnUiAsync(owner));
        }

        return await SelectRegionOnUiAsync(owner);
    }

    private static Task<ScreenSelectionResult?> SelectRegionOnUiAsync(Window? owner)
    {
        var viewModel = new ScreenSelectionViewModel();
        var window = new ScreenSelectionOverlayWindow
        {
            DataContext = viewModel
        };
        var screen = ResolveTargetScreen(owner);
        if (screen is not null)
        {
            window.ConfigureForScreen(screen);
        }

        if (owner is not null)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        return window.WaitForSelectionAsync();
    }

    private static Screen? ResolveTargetScreen(Window? owner)
    {
        var screens = owner?.Screens;
        if (screens is null)
            return null;

        if (TryGetCursorPosition(out var cursorPosition))
        {
            var cursorScreen = screens.ScreenFromPoint(cursorPosition);
            if (cursorScreen is not null)
                return cursorScreen;
        }

        var ownerScreen = screens.ScreenFromPoint(owner!.Position);
        return ownerScreen ?? screens.Primary ?? screens.All.FirstOrDefault();
    }

    private static bool TryGetCursorPosition(out PixelPoint position)
    {
        position = default;

        if (!OperatingSystem.IsWindows() || !GetCursorPos(out var point))
            return false;

        position = new PixelPoint(point.X, point.Y);
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);
}

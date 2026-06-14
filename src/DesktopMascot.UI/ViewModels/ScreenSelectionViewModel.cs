using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using DesktopMascot.UI.Services;

namespace DesktopMascot.UI.ViewModels;

public sealed partial class ScreenSelectionViewModel : ObservableObject
{
    private Point _startPoint;
    private Point _currentPoint;
    private PixelRect _screenBounds;
    private double _screenScaling = 1;

    [ObservableProperty] private double _selectionX;
    [ObservableProperty] private double _selectionY;
    [ObservableProperty] private double _selectionWidth;
    [ObservableProperty] private double _selectionHeight;
    [ObservableProperty] private bool _isSelecting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSelection))]
    private bool _hasSelection;

    [ObservableProperty] private string _selectionText = "拖动鼠标圈选要理解的屏幕区域";
    [ObservableProperty] private string _selectionHint = "按 Esc 取消，松开鼠标确认";

    public bool HasNoSelection => !HasSelection;
    public bool HasValidSelection => SelectionWidth >= 12 && SelectionHeight >= 12;

    public void SetScreenTransform(PixelRect screenBounds, double screenScaling)
    {
        _screenBounds = screenBounds;
        _screenScaling = screenScaling > 0 ? screenScaling : 1;
    }

    public void Begin(Point point)
    {
        _startPoint = point;
        _currentPoint = point;
        IsSelecting = true;
        HasSelection = true;
        UpdateSelection();
    }

    public void Update(Point point)
    {
        if (!IsSelecting)
            return;

        _currentPoint = point;
        UpdateSelection();
    }

    public ScreenSelectionResult? Complete(Point point)
    {
        if (!IsSelecting)
            return null;

        _currentPoint = point;
        IsSelecting = false;
        UpdateSelection();

        return HasValidSelection
            ? CreateResult(isConfirmed: true)
            : null;
    }

    public ScreenSelectionResult? ConfirmCurrent()
    {
        return HasValidSelection ? CreateResult(isConfirmed: true) : null;
    }

    public void Reset()
    {
        IsSelecting = false;
        HasSelection = false;
        SelectionX = 0;
        SelectionY = 0;
        SelectionWidth = 0;
        SelectionHeight = 0;
        SelectionText = "拖动鼠标圈选要理解的屏幕区域";
        SelectionHint = "按 Esc 取消，松开鼠标确认";
    }

    private void UpdateSelection()
    {
        var x = Math.Min(_startPoint.X, _currentPoint.X);
        var y = Math.Min(_startPoint.Y, _currentPoint.Y);
        var width = Math.Abs(_currentPoint.X - _startPoint.X);
        var height = Math.Abs(_currentPoint.Y - _startPoint.Y);

        SelectionX = Math.Max(0, x);
        SelectionY = Math.Max(0, y);
        SelectionWidth = Math.Max(0, width);
        SelectionHeight = Math.Max(0, height);
        SelectionText = HasValidSelection
            ? $"已选择 {Math.Round(SelectionWidth * _screenScaling)} x {Math.Round(SelectionHeight * _screenScaling)}"
            : "拖动鼠标圈选要理解的屏幕区域";
        SelectionHint = HasValidSelection
            ? "松开鼠标确认，按 Esc 取消"
            : "区域至少需要 12 x 12";
        OnPropertyChanged(nameof(HasValidSelection));
    }

    private ScreenSelectionResult CreateResult(bool isConfirmed) => new()
    {
        X = _screenBounds.X + (int)Math.Round(SelectionX * _screenScaling),
        Y = _screenBounds.Y + (int)Math.Round(SelectionY * _screenScaling),
        Width = Math.Max(1, (int)Math.Round(SelectionWidth * _screenScaling)),
        Height = Math.Max(1, (int)Math.Round(SelectionHeight * _screenScaling)),
        IsConfirmed = isConfirmed
    };
}

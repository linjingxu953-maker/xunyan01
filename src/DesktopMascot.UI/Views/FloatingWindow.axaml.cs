using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Views;

public partial class FloatingWindow : Window
{
    private const double CollapsedWidth = 180;
    private const double CollapsedHeight = 240;
    private const double ExpandedWidth = 980;
    private const double ExpandedHeight = 660;

    private readonly DispatcherTimer _animationTimer;
    private readonly DateTime _animationStart = DateTime.UtcNow;
    private FloatingWindowViewModel? _viewModel;

    public FloatingWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Opened += (_, _) => ApplyWindowMode(IsExpanded);

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _animationTimer.Tick += (_, _) => UpdateMascotAnimation();
        _animationTimer.Start();
    }

    private bool IsExpanded => _viewModel?.IsChatDialogVisible == true;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.MessageItems.CollectionChanged -= OnMessageItemsCollectionChanged;
            _viewModel.SetInlineSettingsOwner(null);
        }

        _viewModel = DataContext as FloatingWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.SetInlineSettingsOwner(this);
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.MessageItems.CollectionChanged += OnMessageItemsCollectionChanged;
            ApplyWindowMode(_viewModel.IsChatDialogVisible);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FloatingWindowViewModel.IsChatDialogVisible))
        {
            ApplyWindowMode(IsExpanded);

            if (IsExpanded)
            {
                Dispatcher.UIThread.Post(FocusInput, DispatcherPriority.Background);
                QueueScrollMessagesToEnd();
            }
        }
    }

    private void OnMessageItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueScrollMessagesToEnd();
    }

    private void QueueScrollMessagesToEnd()
    {
        Dispatcher.UIThread.Post(() => MessageScrollViewer?.ScrollToEnd(), DispatcherPriority.Background);
    }

    private void ApplyWindowMode(bool expanded)
    {
        var targetWidth = expanded ? ExpandedWidth : CollapsedWidth;
        var targetHeight = expanded ? ExpandedHeight : CollapsedHeight;
        var anchorRight = Position.X + ToPixelWidth(Math.Max(Width, 1));
        var anchorBottom = Position.Y + ToPixelHeight(Math.Max(Height, 1));

        Width = targetWidth;
        Height = targetHeight;
        MinWidth = targetWidth;
        MinHeight = targetHeight;
        MaxWidth = targetWidth;
        MaxHeight = targetHeight;

        if (!IsVisible)
            return;

        Position = ClampToCurrentScreen(
            new PixelPoint(
                anchorRight - ToPixelWidth(targetWidth),
                anchorBottom - ToPixelHeight(targetHeight)),
            targetWidth,
            targetHeight);
    }

    private PixelPoint ClampToCurrentScreen(PixelPoint desiredPosition, double width, double height)
    {
        var screen = Screens.ScreenFromWindow(this) ??
                     Screens.ScreenFromPoint(desiredPosition) ??
                     Screens.Primary ??
                     Screens.All.FirstOrDefault();

        if (screen is null)
            return desiredPosition;

        var area = screen.WorkingArea;
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(width * GetScaling(screen)));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(height * GetScaling(screen)));
        var maxX = area.X + Math.Max(0, area.Width - pixelWidth);
        var maxY = area.Y + Math.Max(0, area.Height - pixelHeight);

        return new PixelPoint(
            Math.Clamp(desiredPosition.X, area.X, maxX),
            Math.Clamp(desiredPosition.Y, area.Y, maxY));
    }

    private int ToPixelWidth(double width) => (int)Math.Ceiling(width * GetCurrentScaling());

    private int ToPixelHeight(double height) => (int)Math.Ceiling(height * GetCurrentScaling());

    private double GetCurrentScaling()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary ?? Screens.All.FirstOrDefault();
        return screen is null ? RenderScaling : GetScaling(screen);
    }

    private static double GetScaling(Screen screen) => screen.Scaling > 0 ? screen.Scaling : 1;

    private void UpdateMascotAnimation()
    {
        if (_viewModel is null || !MascotHost.IsVisible)
            return;

        var seconds = (DateTime.UtcNow - _animationStart).TotalSeconds;
        var speed = _viewModel.IsMascotBusy ? 5.2 : _viewModel.IsMascotWaiting ? 4.0 : 2.4;
        var wave = Math.Sin(seconds * speed);
        var secondary = Math.Sin(seconds * speed * 0.5);
        var lift = _viewModel.IsMascotBusy ? -4.0 : _viewModel.IsMascotWaiting ? -2.4 : -1.8;
        var shake = _viewModel.IsMascotError ? Math.Sin(seconds * 18) * 2.5 : 0;
        var scale = 1 + wave * (_viewModel.IsMascotBusy ? 0.025 : 0.012);

        var transforms = new TransformGroup();
        transforms.Children.Add(new ScaleTransform(scale, 1 + secondary * 0.01));
        transforms.Children.Add(new TranslateTransform(shake, wave * lift));
        MascotHost.RenderTransformOrigin = new RelativePoint(0.5, 0.78, RelativeUnit.Relative);
        MascotHost.RenderTransform = transforms;

        MascotHalo.Opacity = _viewModel.IsMascotBusy
            ? 0.42 + Math.Abs(wave) * 0.28
            : _viewModel.IsMascotWaiting
                ? 0.36 + Math.Abs(wave) * 0.22
                : 0.24 + Math.Abs(wave) * 0.12;
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void MascotIcon_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _viewModel?.ExpandDialogCommand.Execute(null);
    }

    private void HistoryItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: TaskHistoryItem item } || _viewModel is null)
            return;

        _viewModel.OpenTaskHistoryItem(item);
    }

    private void OpenHistoryItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: TaskHistoryItem item })
        {
            _viewModel?.OpenTaskHistoryItem(item);
        }

        e.Handled = true;
    }

    private async void CopyHistoryItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: TaskHistoryItem item } && _viewModel is not null)
        {
            await _viewModel.CopyTaskHistoryItemAsync(item);
        }

        e.Handled = true;
    }

    private async void SaveHistoryItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: TaskHistoryItem item } && _viewModel is not null)
        {
            await _viewModel.SaveTaskHistoryItemAsync(item);
        }

        e.Handled = true;
    }

    private async void DeleteHistoryItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: TaskHistoryItem item } && _viewModel is not null)
        {
            await _viewModel.DeleteTaskHistoryItemAsync(item);
        }

        e.Handled = true;
    }

    private void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        _viewModel?.SendMessageCommand.Execute(null);
        e.Handled = true;
    }

    private void PlayMessageButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: string content })
        {
            _viewModel?.PlayMessageAudio(content);
        }
    }

    private void InlineCharacterAssetDropZone_DragOver(object? sender, DragEventArgs e)
    {
        CharacterAssetDropHelper.SetDragEffect(e);
    }

    private void InlineCharacterAssetDropZone_Drop(object? sender, DragEventArgs e)
    {
        _viewModel?.InlineSettings.ApplyDroppedCharacterImageFiles(CharacterAssetDropHelper.GetImageFilePaths(e));
        e.Handled = true;
    }

    public void FocusInput()
    {
        InputBox?.Focus();
    }
}

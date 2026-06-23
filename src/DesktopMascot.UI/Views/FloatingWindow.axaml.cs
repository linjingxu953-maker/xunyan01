using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using DesktopMascot.UI.Services;
using DesktopMascot.UI.ViewModels;

namespace DesktopMascot.UI.Views;

public partial class FloatingWindow : Window
{
    private const double CollapsedWidth = 180;
    private const double CollapsedHeight = 240;
    private const double ChatWidth = 1120;
    private const double ChatHeight = 760;
    private const double SettingsWidth = 1320;
    private const double SettingsHeight = 820;

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
        if (e.PropertyName == nameof(FloatingWindowViewModel.IsChatDialogVisible) ||
            e.PropertyName == nameof(FloatingWindowViewModel.IsSettingsPageVisible))
        {
            ApplyWindowMode(IsExpanded);

            if (IsExpanded && e.PropertyName == nameof(FloatingWindowViewModel.IsChatDialogVisible))
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
        var settingsMode = expanded && _viewModel?.IsSettingsPageVisible == true;
        var targetWidth = expanded ? (settingsMode ? SettingsWidth : ChatWidth) : CollapsedWidth;
        var targetHeight = expanded ? (settingsMode ? SettingsHeight : ChatHeight) : CollapsedHeight;
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
        var profile = MascotAnimationProfile.ForState(_viewModel.CurrentState, _viewModel.IsBusy);
        var frame = profile.Evaluate(seconds);

        var transforms = new TransformGroup();
        transforms.Children.Add(new ScaleTransform(frame.ScaleX, frame.ScaleY));
        transforms.Children.Add(new RotateTransform(frame.RotationDegrees));
        transforms.Children.Add(new TranslateTransform(frame.OffsetX, frame.OffsetY));
        MascotHost.RenderTransformOrigin = new RelativePoint(0.5, 0.82, RelativeUnit.Relative);
        MascotHost.RenderTransform = transforms;
        MascotHalo.Opacity = frame.HaloOpacity;
        MascotStateChip.RenderTransform = new TranslateTransform(0, frame.ChipOffsetY);
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

    private async void PlayMessageButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: string content })
        {
            if (_viewModel is not null)
            {
                await _viewModel.PlayMessageAudioAsync(content);
            }
        }
    }

    private void UseScreenSuggestedAction_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: ScreenContextActionItem action })
        {
            _viewModel?.UseScreenSuggestedAction(action);
            FocusInput();
        }

        e.Handled = true;
    }

    private void InlineCharacterStatePreview_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { Tag: CharacterStatePreviewItem item })
        {
            _viewModel?.InlineSettings.SelectCharacterStatePreviewCommand.Execute(item);
            e.Handled = true;
        }
    }

    private void UseScreenScreenshotEvidence_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel?.UseScreenScreenshotEvidence();
        FocusInput();
        e.Handled = true;
    }

    private void SelectToolLauncherCategory_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: string category })
        {
            _viewModel?.SelectToolLauncherCategory(category);
        }

        e.Handled = true;
    }

    private void UseToolLauncherItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: ToolLauncherItem item })
        {
            _viewModel?.UseToolLauncherItem(item);
            FocusInput();
        }

        e.Handled = true;
    }

    private void CharacterSwitchItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: CharacterProfileListItem item })
        {
            _viewModel?.SwitchCharacterProfile(item);
        }

        e.Handled = true;
    }

    private void ApplyToolLauncherForm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel?.ApplyToolLauncherForm();
        FocusInput();
        e.Handled = true;
    }

    private void CancelToolLauncherForm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel?.CancelToolLauncherForm();
        e.Handled = true;
    }

    private async void PickToolLauncherFilePath_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.PickToolLauncherFilePathAsync();
        }

        e.Handled = true;
    }

    private async void PickToolLauncherFolderPath_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.PickToolLauncherFolderPathAsync();
        }

        e.Handled = true;
    }

    private void ToggleScreenScreenshotPreview_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel?.ToggleScreenScreenshotPreview();
        e.Handled = true;
    }

    private async void CopyScreenScreenshotPath_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.CopyScreenScreenshotPathAsync();
        }

        e.Handled = true;
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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Storyboard.Models;
using Storyboard.ViewModels;
using System;

namespace Storyboard.Views;

public partial class ShotCardView : UserControl
{
    private const double DragThreshold = 4;

    private bool _isPointerDownOnHandle;
    private bool _isDragging;
    private Point _dragStartPoint;

    public ShotCardView()
    {
        InitializeComponent();
    }

    public void SetPlaceholderState(bool isPlaceholder)
    {
        // Keep layout space but visually empty.
        RootBorder.Opacity = isPlaceholder ? 0 : 1;
        RootBorder.IsHitTestVisible = !isPlaceholder;
    }

    public void SetDraggingState(bool isDragging)
    {
        if (isDragging)
            RootBorder.Classes.Add("dragging");
        else
            RootBorder.Classes.Remove("dragging");

        if (DraggingPill != null)
            DraggingPill.Opacity = isDragging ? 1 : 0;

        // Cursor feedback: keep it obvious that we're dragging.
        try
        {
            RootBorder.Cursor = new Cursor(isDragging ? StandardCursorType.SizeAll : StandardCursorType.Hand);
        }
        catch
        {
            // Some platforms may not support all cursor types; ignore.
        }
    }

    public void FlashDrop()
    {
        RootBorder.Classes.Add("drop-flash");
        var _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await System.Threading.Tasks.Task.Delay(160);
            RootBorder.Classes.Remove("drop-flash");
        });
    }

    private void OnCardClicked(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ShotItem shot)
        {
            // Find the MainWindow and set the selected shot
            var mainWindow = this.FindAncestorOfType<Window>();
            if (mainWindow?.DataContext is MainViewModel viewModel)
            {
                viewModel.SelectedShot = shot;
            }
        }
    }

    private void OnDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPointerDownOnHandle = true;
            _isDragging = false;
            _dragStartPoint = e.GetPosition(this);

            e.Pointer.Capture(sender as IInputElement);
            e.Handled = true;
        }
    }

    private void OnDragHandleMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerDownOnHandle)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPointerDownOnHandle = false;
            return;
        }

        var pos = e.GetPosition(this);
        var dx = pos.X - _dragStartPoint.X;
        var dy = pos.Y - _dragStartPoint.Y;
        var listView = this.FindAncestorOfType<ShotListView>();
        if (listView == null)
            return;

        if (!_isDragging)
        {
            if (Math.Abs(dx) < DragThreshold && Math.Abs(dy) < DragThreshold)
                return;

            if (DataContext is not ShotItem shot)
                return;

            _isDragging = true;
            SetDraggingState(true);
            listView.BeginInternalDrag(this, shot, e);
        }
        // Once dragging begins, ShotListView owns pointer events (via pointer capture).
    }

    private void OnDragHandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        var listView = this.FindAncestorOfType<ShotListView>();
        if (_isDragging && listView != null)
        {
            listView.EndInternalDrag(e);
        }

        _isPointerDownOnHandle = false;
        _isDragging = false;
        e.Pointer.Capture(null);
        SetDraggingState(false);
        e.Handled = true;
    }

    private void OnDragHandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        // Capture will intentionally transfer to ShotListView when dragging starts.
        // Do not cancel here; ShotListView will cancel/end reliably.
        _isPointerDownOnHandle = false;
    }

    private void OnMaterialImagePressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ShotItem shot && !string.IsNullOrEmpty(shot.MaterialFilePath))
        {
            ShowImageDialog(shot.MaterialFilePath, "素材图片");
        }
    }

    private void OnFirstFrameImagePressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ShotItem shot && !string.IsNullOrEmpty(shot.FirstFrameImagePath))
        {
            ShowImageDialog(shot.FirstFrameImagePath, "首帧图片");
        }
    }

    private void ShowImageDialog(string imagePath, string title)
    {
        try
        {
            var bitmap = new Avalonia.Media.Imaging.Bitmap(imagePath);
            var window = new Window
            {
                Title = title,
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new Image
                {
                    Source = bitmap,
                    Stretch = Avalonia.Media.Stretch.Uniform
                }
            };

            var mainWindow = this.FindAncestorOfType<Window>();
            if (mainWindow != null)
            {
                window.ShowDialog(mainWindow);
            }
            else
            {
                window.Show();
            }
        }
        catch (Exception ex)
        {
            // Handle error, perhaps show a message
            Console.WriteLine($"Error loading image: {ex.Message}");
        }
    }
}

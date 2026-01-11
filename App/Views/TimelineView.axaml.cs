using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Storyboard.Models;
using Storyboard.ViewModels;

namespace Storyboard.Views;

public partial class TimelineView : UserControl
{
    public TimelineView()
    {
        InitializeComponent();
    }

    private void OnShotPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && sender is Control { DataContext: ShotItem shot })
        {
            var window = this.FindAncestorOfType<Window>();
            if (window?.DataContext is MainViewModel vm)
                vm.SelectedShot = shot;
        }
    }

    private void OnShotPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: ShotItem shot })
            shot.IsHovered = true;
    }

    private void OnShotPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: ShotItem shot })
            shot.IsHovered = false;
    }
}

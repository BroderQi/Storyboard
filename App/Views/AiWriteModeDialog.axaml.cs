using Avalonia.Controls;
using Avalonia.Interactivity;
using Storyboard.Models;

namespace Storyboard.Views;

public partial class AiWriteModeDialog : Window
{
    public AiWriteModeDialog()
    {
        InitializeComponent();
    }

    private void OnOverwriteClick(object? sender, RoutedEventArgs e)
    {
        Close(AiWriteMode.Overwrite);
    }

    private void OnAppendClick(object? sender, RoutedEventArgs e)
    {
        Close(AiWriteMode.Append);
    }

    private void OnSkipClick(object? sender, RoutedEventArgs e)
    {
        Close(AiWriteMode.Skip);
    }
}

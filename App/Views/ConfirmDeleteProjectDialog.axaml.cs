using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Storyboard.Views;

public partial class ConfirmDeleteProjectDialog : Window
{
    public ConfirmDeleteProjectDialog()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}

public sealed class ConfirmDeleteProjectDialogViewModel
{
    public ConfirmDeleteProjectDialogViewModel(string projectName)
    {
        ProjectName = projectName;
    }

    public string ProjectName { get; }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Storyboard.ViewModels;

namespace Storyboard.Views;

public partial class CreateProjectDialog : Window
{
    public CreateProjectDialog()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.NewProjectName = string.Empty;

        Close();
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            Close();
            return;
        }

        var name = vm.NewProjectName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        vm.CreateNewProjectCommand.Execute(name);
        vm.NewProjectName = string.Empty;
        Close();
    }
}

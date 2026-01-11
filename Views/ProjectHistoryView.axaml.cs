using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Storyboard.Models;
using Storyboard.ViewModels;

namespace Storyboard.Views;

public partial class ProjectHistoryView : UserControl
{
    public ProjectHistoryView()
    {
        InitializeComponent();
    }

    private void OnCreateProjectClick(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ShowCreateProjectDialogCommand.Execute(null);
        }
    }

    private void OnOpenProjectClick(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        // Avoid opening the project when interacting with nested controls (e.g. delete button).
        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<Button>() is not null)
        {
            e.Handled = true;
            return;
        }

        if (sender is Control c && c.DataContext is ProjectInfo project)
        {
            viewModel.OpenProjectCommand.Execute(project);
            e.Handled = true;
        }
    }

    private async void OnDeleteProjectClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (sender is Control c && c.DataContext is ProjectInfo project)
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
            {
                e.Handled = true;
                return;
            }

            var dialog = new ConfirmDeleteProjectDialog
            {
                DataContext = new ConfirmDeleteProjectDialogViewModel($"确定要删除项目\"{project.Name}\"吗？")
            };

            var confirmed = await dialog.ShowDialog<bool>(owner);

            if (confirmed)
                viewModel.DeleteProjectCommand.Execute(project);

            e.Handled = true;
        }
    }
}

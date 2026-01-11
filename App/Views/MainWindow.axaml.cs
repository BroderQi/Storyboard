using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Storyboard;
using Storyboard.ViewModels;
using System.ComponentModel;

namespace Storyboard.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainViewModel viewModel)
            return;

        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsNewProjectDialogOpen):
                if (viewModel.IsNewProjectDialogOpen)
                {
                    var dialog = new CreateProjectDialog { DataContext = viewModel };
                    await dialog.ShowDialog(this);
                    viewModel.IsNewProjectDialogOpen = false;
                }
                break;

            case nameof(MainViewModel.IsBatchOperationsDialogOpen):
                if (viewModel.IsBatchOperationsDialogOpen)
                {
                    var dialog = new BatchOperationsDialog { DataContext = new BatchOperationsViewModel(viewModel.Shots) };
                    await dialog.ShowDialog(this);
                    viewModel.IsBatchOperationsDialogOpen = false;
                }
                break;

            case nameof(MainViewModel.IsExportDialogOpen):
                if (viewModel.IsExportDialogOpen)
                {
                    var dialog = new ExportDialog { DataContext = viewModel };
                    await dialog.ShowDialog(this);
                    viewModel.IsExportDialogOpen = false;
                }
                break;

            case nameof(MainViewModel.IsTaskManagerDialogOpen):
                if (viewModel.IsTaskManagerDialogOpen)
                {
                    var dialog = new TaskManagerDialog { DataContext = viewModel };
                    await dialog.ShowDialog(this);
                    viewModel.IsTaskManagerDialogOpen = false;
                }
                break;

            case nameof(MainViewModel.IsProviderSettingsDialogOpen):
                if (viewModel.IsProviderSettingsDialogOpen)
                {
                    var providerVm = App.Services.GetRequiredService<ApiKeyViewModel>();
                    var dialog = new ProviderSettingsDialog { DataContext = providerVm };
                    await dialog.ShowDialog(this);
                    viewModel.IsProviderSettingsDialogOpen = false;
                }
                break;

            case nameof(MainViewModel.IsTextToShotDialogOpen):
                if (viewModel.IsTextToShotDialogOpen)
                {
                    var dialog = new TextToShotDialog { DataContext = viewModel };
                    await dialog.ShowDialog(this);
                    viewModel.IsTextToShotDialogOpen = false;
                }
                break;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
        base.OnClosing(e);
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Storyboard.ViewModels;

namespace Storyboard.Views;

public partial class TextToShotDialog : Window
{
    public TextToShotDialog()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnGenerateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        await viewModel.GenerateShotsFromTextPromptAsync();
        Close();
    }
}

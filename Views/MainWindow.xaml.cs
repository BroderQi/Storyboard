using System.Windows;
using Storyboard.ViewModels;

namespace Storyboard.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

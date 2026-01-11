using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Storyboard.ViewModels;

namespace Storyboard.Views.Pages;

public partial class ApiKeyPage : UserControl
{
    public ApiKeyPage()
    {
        InitializeComponent();

        if (Application.Current is App app)
        {
            DataContext = app.Services.GetRequiredService<ApiKeyViewModel>();
        }
    }
}

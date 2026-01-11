using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
using Storyboard.AI.Core;
using Storyboard.ViewModels;
using System;

namespace Storyboard.Views;

public partial class ProviderSettingsDialog : Window
{
    public ProviderSettingsDialog()
    {
        InitializeComponent();
    }

    private void OnProviderCardPressed(object? sender, PointerPressedEventArgs e)
    {
        // Clicking the switch should not change selection.
        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<ToggleSwitch>() is not null)
            return;

        if (sender is not Control control)
            return;

        if (control.Tag is not string tag)
            return;

        if (DataContext is not ApiKeyViewModel vm)
            return;

        if (Enum.TryParse<AIProviderType>(tag, out var provider))
        {
            vm.SelectedProvider = provider;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}

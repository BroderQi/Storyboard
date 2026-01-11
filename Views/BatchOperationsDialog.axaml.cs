using Avalonia.Controls;
using Storyboard.ViewModels;
using System;

namespace Storyboard.Views;

public partial class BatchOperationsDialog : Window
{
    public BatchOperationsDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is BatchOperationsViewModel vm)
        {
            vm.RequestClose -= Vm_RequestClose;
            vm.RequestClose += Vm_RequestClose;
        }
    }

    private void Vm_RequestClose(object? sender, EventArgs e)
    {
        Close();
    }
}

using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Storyboard.Models;

namespace Storyboard.Behaviors;

public static class DataGridShotReorderBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(DataGridShotReorderBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty DragStateProperty =
        DependencyProperty.RegisterAttached(
            "DragState",
            typeof(DragState),
            typeof(DataGridShotReorderBehavior),
            new PropertyMetadata(null));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    private static DragState GetOrCreateState(DataGrid dataGrid)
    {
        var state = (DragState?)dataGrid.GetValue(DragStateProperty);
        if (state != null)
            return state;

        state = new DragState();
        dataGrid.SetValue(DragStateProperty, state);
        return state;
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dataGrid)
            return;

        var enabled = e.NewValue is true;

        if (enabled)
        {
            dataGrid.AllowDrop = true;
            dataGrid.PreviewMouseLeftButtonDown += DataGrid_PreviewMouseLeftButtonDown;
            dataGrid.PreviewMouseMove += DataGrid_PreviewMouseMove;
            dataGrid.Drop += DataGrid_Drop;
        }
        else
        {
            dataGrid.PreviewMouseLeftButtonDown -= DataGrid_PreviewMouseLeftButtonDown;
            dataGrid.PreviewMouseMove -= DataGrid_PreviewMouseMove;
            dataGrid.Drop -= DataGrid_Drop;
        }
    }

    private static void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
            return;

        var state = GetOrCreateState(dataGrid);
        state.DragStartPoint = e.GetPosition(null);
        state.DraggedItem = null;

        // Only allow drag when clicking first column cell
        var dep = (DependencyObject)e.OriginalSource;
        while (dep != null && dep is not DataGridCell)
        {
            dep = VisualTreeHelper.GetParent(dep);
        }

        if (dep is DataGridCell cell && cell.Column.DisplayIndex == 0)
        {
            var row = FindAncestor<DataGridRow>(cell);
            state.DraggedItem = row?.Item as ShotItem;
        }
    }

    private static void DataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
            return;

        var state = GetOrCreateState(dataGrid);

        if (e.LeftButton != MouseButtonState.Pressed || state.DraggedItem == null)
            return;

        var currentPosition = e.GetPosition(null);
        var diff = state.DragStartPoint - currentPosition;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            DragDrop.DoDragDrop(dataGrid, state.DraggedItem, DragDropEffects.Move);
            state.DraggedItem = null;
        }
    }

    private static void DataGrid_Drop(object sender, DragEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
            return;

        if (!e.Data.GetDataPresent(typeof(ShotItem)))
            return;

        var droppedData = e.Data.GetData(typeof(ShotItem)) as ShotItem;
        var target = GetDataGridItemAtPosition(dataGrid, e.GetPosition(dataGrid));

        if (droppedData == null || target == null || ReferenceEquals(droppedData, target))
            return;

        if (dataGrid.ItemsSource is not IList list || list.IsReadOnly)
            return;

        var oldIndex = list.IndexOf(droppedData);
        var newIndex = list.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
            return;

        list.RemoveAt(oldIndex);
        list.Insert(newIndex, droppedData);

        // Renumber shot numbers to match new order (1-based)
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] is ShotItem shot)
            {
                shot.ShotNumber = i + 1;
            }
        }

        e.Handled = true;
    }

    private static ShotItem? GetDataGridItemAtPosition(DataGrid dataGrid, Point position)
    {
        var hitTestResult = VisualTreeHelper.HitTest(dataGrid, position);
        if (hitTestResult == null)
            return null;

        var row = FindAncestor<DataGridRow>(hitTestResult.VisualHit);
        return row?.Item as ShotItem;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T ancestor)
                return ancestor;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private sealed class DragState
    {
        public Point DragStartPoint { get; set; }
        public ShotItem? DraggedItem { get; set; }
    }
}

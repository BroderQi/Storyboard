using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using 分镜大师.Models;
using 分镜大师.ViewModels;

namespace 分镜大师;

public partial class MainWindow : Window
{
    private Point _dragStartPoint;
    private ShotItem? _draggedItem;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        
        // 检查是否点击了镜头号列
        var dep = (DependencyObject)e.OriginalSource;
        while (dep != null && dep is not DataGridCell)
        {
            dep = VisualTreeHelper.GetParent(dep);
        }

        if (dep is DataGridCell cell && cell.Column.DisplayIndex == 0)
        {
            var row = FindAncestor<DataGridRow>(cell);
            _draggedItem = row?.Item as ShotItem;
        }
        else
        {
            _draggedItem = null;
        }
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
        {
            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPosition;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                DragDrop.DoDragDrop(ShotsDataGrid, _draggedItem, DragDropEffects.Move);
                _draggedItem = null;
            }
        }
    }

    private void DataGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(ShotItem)))
        {
            var droppedData = e.Data.GetData(typeof(ShotItem)) as ShotItem;
            var target = GetDataGridItemAtPosition(e.GetPosition(ShotsDataGrid));

            if (droppedData != null && target != null && droppedData != target)
            {
                var viewModel = DataContext as MainViewModel;
                var oldIndex = viewModel?.Shots.IndexOf(droppedData) ?? -1;
                var newIndex = viewModel?.Shots.IndexOf(target) ?? -1;

                if (oldIndex >= 0 && newIndex >= 0)
                {
                    viewModel?.MoveShot(oldIndex, newIndex);
                }
            }
        }

        e.Handled = true;
    }

    private ShotItem? GetDataGridItemAtPosition(Point position)
    {
        var hitTestResult = VisualTreeHelper.HitTest(ShotsDataGrid, position);
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
}

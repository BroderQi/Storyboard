using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Storyboard.Models;
using Storyboard.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace Storyboard.Views;

public partial class ShotListView : UserControl
{
    private ShotItem? _dragShot;
    private ShotCardView? _dragSourceCard;
    private bool _isDragging;
    private Point _dragPointerOffsetInItem;
    private readonly Dictionary<Control, long> _flipAnimationTokens = new();

    private int _dragFromIndex = -1;
    private int _pendingDropIndex = -1;
    private Point _lastPointer;
    private Size _dragItemSize;

    private DispatcherTimer? _autoScrollTimer;
    private double _autoScrollVelocityY;

    public ShotListView()
    {
        InitializeComponent();

        // Drag is pointer-driven; make the list view the stable event target.
        // This prevents stuck overlays when the source card is re-templated during live reordering.
        var routes = RoutingStrategies.Tunnel | RoutingStrategies.Bubble;
        AddHandler(InputElement.PointerMovedEvent, OnAnyPointerMoved, routes);
        AddHandler(InputElement.PointerReleasedEvent, OnAnyPointerReleased, routes);
        AddHandler(InputElement.PointerCaptureLostEvent, OnAnyPointerCaptureLost, routes);
    }

    private void OnAnyPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
            UpdateInternalDrag(e);
    }

    private void OnAnyPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
            EndInternalDrag(e);
    }

    private void OnAnyPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isDragging)
            CancelInternalDrag();
    }

    private ItemsControl? GetActiveItemsControl()
    {
        if (DataContext is not MainViewModel vm)
            return null;

        if (vm.IsGridView)
            return GridItemsControl;
        if (vm.IsListView)
            return ListItemsControl;

        return null;
    }

    internal void BeginInternalDrag(ShotCardView sourceCard, ShotItem shot, PointerEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        var itemsControl = GetActiveItemsControl();
        if (itemsControl == null)
            return;

        _dragShot = shot;
        _dragSourceCard = sourceCard;
        _isDragging = true;

        _dragFromIndex = vm.Shots.IndexOf(shot);
        _pendingDropIndex = _dragFromIndex;

        _dragSourceCard.SetPlaceholderState(true);

        // Capture the pointer to a stable element (the list view), so we always get
        // move/release/capture-lost even if the source item is re-created during reordering.
        try
        {
            e.Pointer.Capture(this);
        }
        catch
        {
            // Best-effort; we'll still cancel on window focus loss via capture-lost if available.
        }

        // Compute offset inside the item's visual so ghost aligns naturally.
        var container = FindContainerForItem(itemsControl, shot);
        if (container != null)
        {
            var ptInContainer = e.GetPosition(container);
            _dragPointerOffsetInItem = ptInContainer;
            _dragItemSize = container.Bounds.Size;
        }
        else
        {
            _dragPointerOffsetInItem = new Point(20, 20);
            _dragItemSize = sourceCard.Bounds.Size;
        }

        // Create ghost.
        var ghost = new ShotCardView
        {
            DataContext = shot,
            IsHitTestVisible = false,
            Opacity = 0.96,
            RenderTransformOrigin = Avalonia.RelativePoint.Center,
            RenderTransform = new ScaleTransform(1.02, 1.02)
        };
        DragGhostHost.Content = ghost;
        DragGhostHost.Opacity = 1;

        // Initialize visuals.
        DragTargetHighlight.Opacity = 0;
        DragInsertLine.Opacity = 0;

        UpdateInternalDrag(e);

        StartAutoScrollTimer();
    }

    internal void UpdateInternalDrag(PointerEventArgs e)
    {
        if (!_isDragging || _dragShot == null)
            return;

        if (DataContext is not MainViewModel vm)
            return;

        var itemsControl = GetActiveItemsControl();
        if (itemsControl == null)
            return;

        // Move ghost.
        var pos = e.GetPosition(this);
        _lastPointer = pos;
        var ghostLeft = pos.X - _dragPointerOffsetInItem.X;
        var ghostTop = pos.Y - _dragPointerOffsetInItem.Y;
        Canvas.SetLeft(DragGhostHost, ghostLeft);
        Canvas.SetTop(DragGhostHost, ghostTop);

        // Probe point: use dragged item's center, not the raw pointer.
        // This feels much more natural (like dnd-kit) especially when dragging from a handle.
        var probe = new Point(
            ghostLeft + Math.Max(0, _dragItemSize.Width) / 2,
            ghostTop + Math.Max(0, _dragItemSize.Height) / 2);

        // Find target container.
        var containers = GetVisibleContainers(itemsControl)
            .Where(x => !ReferenceEquals(x.item, _dragShot))
            .ToList();

        if (containers.Count == 0)
        {
            DragTargetHighlight.Opacity = 0;
            DragInsertLine.Opacity = 0;
            return;
        }

        var pointer = pos;

        // LIST VIEW: compute insertion purely by Y midpoints (stable & symmetric).
        if (vm.IsListView)
        {
            var sorted = containers.OrderBy(c => c.rect.Y).ToList();
            if (sorted.Count == 0)
                return;

            var insertPos = sorted.Count; // append by default (post-removal list size = sorted.Count)
            Rect? highlightRect = null;
            bool insertAfter = true;

            for (var i = 0; i < sorted.Count; i++)
            {
                var r = sorted[i].rect;
                var midY = r.Y + (r.Height / 2);
                if (probe.Y < midY)
                {
                    insertPos = i;
                    highlightRect = r;
                    insertAfter = false;
                    break;
                }
            }

            if (highlightRect == null)
            {
                // Appending: highlight last item and show bottom line.
                highlightRect = sorted[^1].rect;
                insertAfter = true;
            }

            ShowTargetHighlight(highlightRect.Value);
            ShowInsertIndicator(highlightRect.Value, insertAfter, vertical: false);

            _pendingDropIndex = Math.Clamp(insertPos, 0, Math.Max(0, vm.Shots.Count - 1));
            UpdateAutoScrollVelocity(pos);
            return;
        }

        // GRID VIEW: prefer item under pointer; otherwise choose nearest by center.
        (ShotItem item, Control container, Rect rect) best;
        var under = containers.FirstOrDefault(c => c.rect.Contains(probe));
        if (under.item != null)
            best = under;
        else
            best = containers.OrderBy(c => DistanceSquared(Center(c.rect), probe)).First();

        var targetIndex = GetIndexInCollection(best.item);
        if (targetIndex < 0)
            return;

        var fromIndex = GetIndexInCollection(_dragShot);
        if (fromIndex < 0)
            return;

        // Grid: left/right when moving more horizontally than vertically.
        var center = Center(best.rect);
        var dxFromCenter = probe.X - center.X;
        var dyFromCenter = probe.Y - center.Y;
        var useHorizontalAxis = Math.Abs(dxFromCenter) >= Math.Abs(dyFromCenter);
        var insertAfterGrid = useHorizontalAxis ? (dxFromCenter >= 0) : (dyFromCenter >= 0);
        ShowTargetHighlight(best.rect);
        ShowInsertIndicator(best.rect, insertAfterGrid, vertical: useHorizontalAxis);

        var overIndexWithoutDrag = targetIndex;
        if (targetIndex > fromIndex)
            overIndexWithoutDrag--;

        var candidateGrid = insertAfterGrid ? overIndexWithoutDrag + 1 : overIndexWithoutDrag;
        candidateGrid = Math.Clamp(candidateGrid, 0, Math.Max(0, vm.Shots.Count - 1));
        _pendingDropIndex = candidateGrid;
        UpdateAutoScrollVelocity(pos);
    }

    internal void EndInternalDrag(PointerReleasedEventArgs e)
    {
        if (!_isDragging)
            return;

        if (DataContext is MainViewModel vm && _dragShot != null)
        {
            var from = vm.Shots.IndexOf(_dragShot);
            var to = Math.Clamp(_pendingDropIndex, 0, Math.Max(0, vm.Shots.Count - 1));

#if DEBUG
            Debug.WriteLine($"[DragDrop] EndInternalDrag from={from} to={to} count={vm.Shots.Count}");
#endif

            if (from >= 0 && from < vm.Shots.Count && to != from)
            {
                vm.Shots.Move(from, to);
                vm.RenumberShotsForDrag();
            }
        }

        // Hide visuals.
        DragGhostHost.Opacity = 0;
        DragGhostHost.Content = null;
        DragTargetHighlight.Opacity = 0;
        DragInsertLine.Opacity = 0;

        // Brief confirmation flash on the dragged card (if still visible).
        _dragSourceCard?.FlashDrop();
        _dragSourceCard?.SetDraggingState(false);
        _dragSourceCard?.SetPlaceholderState(false);
        _dragShot = null;
        _dragSourceCard = null;
        _isDragging = false;

        _dragFromIndex = -1;
        _pendingDropIndex = -1;
        StopAutoScrollTimer();

        try { e.Pointer.Capture(null); } catch { }
    }

    internal void CancelInternalDrag()
    {
        if (!_isDragging)
            return;

        DragGhostHost.Opacity = 0;
        DragGhostHost.Content = null;
        DragTargetHighlight.Opacity = 0;
        DragInsertLine.Opacity = 0;

        _dragSourceCard?.SetDraggingState(false);
        _dragSourceCard?.SetPlaceholderState(false);
        _dragShot = null;
        _dragSourceCard = null;
        _isDragging = false;

        _dragFromIndex = -1;
        _pendingDropIndex = -1;
        StopAutoScrollTimer();

        // Don't assume we still have a PointerEventArgs; just clear capture if any.
        // (Avalonia doesn't expose current pointer here; capture will naturally drop too.)
    }

    private int GetIndexInCollection(ShotItem item)
    {
        if (DataContext is not MainViewModel vm)
            return -1;
        return vm.Shots.IndexOf(item);
    }

    private static Point Center(Rect r) => new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
    private static double DistanceSquared(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private IEnumerable<(ShotItem item, Control container, Rect rect)> GetVisibleContainers(ItemsControl itemsControl)
    {
        if (DataContext is not MainViewModel vm)
            yield break;

        // Important: map each ShotItem to at most ONE realized container.
        // Traversing the visual tree can yield multiple visuals per item (causing duplicate keys).
        foreach (var shot in vm.Shots)
        {
            var container = FindContainerForItem(itemsControl, shot);
            if (container == null)
                continue;

            var topLeft = container.TranslatePoint(new Point(0, 0), this);
            if (topLeft == null)
                continue;

            var rect = new Rect(topLeft.Value, container.Bounds.Size);
            yield return (shot, container, rect);
        }
    }

    private Dictionary<ShotItem, Rect> CaptureItemRects(ItemsControl itemsControl)
        => GetVisibleContainers(itemsControl).ToDictionary(x => x.item, x => x.rect);

    private void AnimateFlip(ItemsControl itemsControl, Dictionary<ShotItem, Rect> before)
    {
        var after = CaptureItemRects(itemsControl);

        foreach (var (item, afterRect) in after)
        {
            if (!before.TryGetValue(item, out var beforeRect))
                continue;

            var dx = beforeRect.X - afterRect.X;
            var dy = beforeRect.Y - afterRect.Y;
            if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5)
                continue;

            var container = FindContainerForItem(itemsControl, item);
            if (container == null)
                continue;

            var tt = container.RenderTransform as TranslateTransform;
            if (tt == null)
            {
                tt = new TranslateTransform();
                container.RenderTransform = tt;
            }

            StartFlipTween(container, tt, dx, dy);
        }
    }

    private void StartFlipTween(Control container, TranslateTransform tt, double startX, double startY)
    {
        // Cancel any in-flight tween for this container.
        var token = DateTime.UtcNow.Ticks;
        _flipAnimationTokens[container] = token;

        tt.X = startX;
        tt.Y = startY;

        const double durationMs = 140;
        var sw = Stopwatch.StartNew();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, __) =>
        {
            if (!_flipAnimationTokens.TryGetValue(container, out var current) || current != token)
            {
                timer.Stop();
                return;
            }

            var t = sw.Elapsed.TotalMilliseconds / durationMs;
            if (t >= 1)
            {
                tt.X = 0;
                tt.Y = 0;
                _flipAnimationTokens.Remove(container);
                timer.Stop();
                return;
            }

            // Cubic ease-out.
            var eased = 1 - Math.Pow(1 - t, 3);
            tt.X = startX * (1 - eased);
            tt.Y = startY * (1 - eased);
        };
        timer.Start();
    }

    private static Control? FindContainerForItem(ItemsControl itemsControl, ShotItem item)
    {
        // Avalonia's ItemsControl doesn't expose WPF-style ContainerFromItem.
        // We locate the realized presenter by matching DataContext.
        return itemsControl.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .FirstOrDefault(p => ReferenceEquals(p.DataContext, item));
    }

    private void ShowTargetHighlight(Rect rect)
    {
        DragTargetHighlight.Width = rect.Width;
        DragTargetHighlight.Height = rect.Height;
        Canvas.SetLeft(DragTargetHighlight, rect.X);
        Canvas.SetTop(DragTargetHighlight, rect.Y);
        DragTargetHighlight.Opacity = 1;
    }

    private void ShowInsertIndicator(Rect rect, bool insertAfter, bool vertical)
    {
        if (vertical)
        {
            DragInsertLine.Width = 2;
            DragInsertLine.Height = rect.Height;
            Canvas.SetLeft(DragInsertLine, insertAfter ? rect.Right - 1 : rect.X);
            Canvas.SetTop(DragInsertLine, rect.Y);
        }
        else
        {
            DragInsertLine.Width = rect.Width;
            DragInsertLine.Height = 2;
            Canvas.SetLeft(DragInsertLine, rect.X);
            Canvas.SetTop(DragInsertLine, insertAfter ? rect.Bottom - 1 : rect.Y);
        }
        DragInsertLine.Opacity = 1;
    }

    private void StartAutoScrollTimer()
    {
        if (_autoScrollTimer != null)
            return;

        _autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _autoScrollTimer.Tick += (_, __) => ApplyAutoScrollTick();
        _autoScrollTimer.Start();
    }

    private void StopAutoScrollTimer()
    {
        if (_autoScrollTimer == null)
            return;

        _autoScrollTimer.Stop();
        _autoScrollTimer = null;
        _autoScrollVelocityY = 0;
    }

    private ScrollViewer? GetActiveScrollViewer()
    {
        if (DataContext is not MainViewModel vm)
            return null;

        if (vm.IsGridView)
            return GridScrollViewer;
        if (vm.IsListView)
            return ListScrollViewer;

        return null;
    }

    private void UpdateAutoScrollVelocity(Point pointer)
    {
        var sv = GetActiveScrollViewer();
        if (sv == null)
        {
            _autoScrollVelocityY = 0;
            return;
        }

        // Pointer is relative to this control; compute against visible bounds.
        const double edge = 48;
        const double maxSpeed = 18;

        if (pointer.Y < edge)
        {
            var t = 1 - (pointer.Y / edge);
            _autoScrollVelocityY = -maxSpeed * t;
        }
        else if (pointer.Y > Bounds.Height - edge)
        {
            var t = 1 - ((Bounds.Height - pointer.Y) / edge);
            _autoScrollVelocityY = maxSpeed * t;
        }
        else
        {
            _autoScrollVelocityY = 0;
        }
    }

    private void ApplyAutoScrollTick()
    {
        if (!_isDragging)
            return;

        var sv = GetActiveScrollViewer();
        if (sv == null)
            return;

        if (Math.Abs(_autoScrollVelocityY) < 0.1)
            return;

        var o = sv.Offset;
        var newY = Math.Max(0, o.Y + _autoScrollVelocityY);
        if (Math.Abs(newY - o.Y) > 0.01)
            sv.Offset = new Vector(o.X, newY);

        // Keep re-evaluating velocity based on latest pointer position.
        UpdateAutoScrollVelocity(_lastPointer);
    }
}

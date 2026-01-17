using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Storyboard.Models;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace Storyboard.Views;

public partial class ShotEditorView : UserControl
{
    private ShotItem? _shot;
    private bool _viewAttached;
    private string? _currentVideoPath;
    private string? _pendingVideoPath;
    private bool _pendingAutoPlay;

    public ShotEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _viewAttached = true;
        EnsurePlayerLoaded();
        ApplyPending();
        UpdatePlayerSource(_shot?.GeneratedVideoPath, false);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_shot != null)
            _shot.PropertyChanged -= OnShotPropertyChanged;

        _viewAttached = false;
        _currentVideoPath = null;
        _pendingVideoPath = null;
        _pendingAutoPlay = false;

        if (VideoWebView != null)
            VideoWebView.Url = null;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_shot != null)
            _shot.PropertyChanged -= OnShotPropertyChanged;

        _shot = DataContext as ShotItem;

        if (_shot != null)
            _shot.PropertyChanged += OnShotPropertyChanged;

        UpdatePlayerSource(_shot?.GeneratedVideoPath, false);
    }

    private void OnShotPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShotItem.GeneratedVideoPath))
        {
            var videoPath = _shot?.GeneratedVideoPath;
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] GeneratedVideoPath changed: {videoPath}");
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] File exists: {(videoPath != null && File.Exists(videoPath))}");
            Dispatcher.UIThread.Post(() => UpdatePlayerSource(videoPath, false));
        }
    }

    private async void OnTogglePlayClicked(object? sender, RoutedEventArgs e)
    {
        var path = NormalizeVideoPath(_shot?.GeneratedVideoPath);
        if (path == null)
            return;

        if (!_viewAttached || VideoWebView == null)
        {
            _pendingVideoPath = path;
            _pendingAutoPlay = true;
            return;
        }

        if (!string.Equals(_currentVideoPath, path, StringComparison.OrdinalIgnoreCase))
        {
            UpdatePlayerSource(path, true);
            return;
        }

        if (await TryTogglePlayAsync())
            return;

        UpdatePlayerSource(path, true);
    }

    private void EnsurePlayerLoaded()
    {
        if (!_viewAttached || VideoWebView == null)
            return;

        var uri = BuildPlayerUri(null, false);
        if (uri == null)
            return;

        if (VideoWebView.Url == null)
            VideoWebView.Url = uri;
    }

    private void ApplyPending()
    {
        if (_pendingVideoPath == null && !_pendingAutoPlay)
            return;

        var path = _pendingVideoPath;
        var autoplay = _pendingAutoPlay;

        _pendingVideoPath = null;
        _pendingAutoPlay = false;

        UpdatePlayerSource(path, autoplay);
    }

    private void UpdatePlayerSource(string? videoPath, bool autoplay)
    {
        System.Diagnostics.Debug.WriteLine($"[ShotEditorView] UpdatePlayerSource called: videoPath={videoPath}, autoplay={autoplay}");
        System.Diagnostics.Debug.WriteLine($"[ShotEditorView] _viewAttached={_viewAttached}, VideoWebView={VideoWebView != null}");

        if (!_viewAttached || VideoWebView == null)
        {
            _pendingVideoPath = videoPath;
            _pendingAutoPlay = autoplay;
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] View not attached, pending video path set");
            return;
        }

        var normalizedPath = NormalizeVideoPath(videoPath);
        System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Normalized path: {normalizedPath}");

        if (!autoplay && string.Equals(_currentVideoPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Path unchanged, skipping update");
            return;
        }

        var uri = BuildPlayerUri(normalizedPath, autoplay);
        System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Built URI: {uri}");

        if (uri == null)
        {
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] URI is null, cannot update player");
            return;
        }

        _currentVideoPath = normalizedPath;
        VideoWebView.Url = uri;
        System.Diagnostics.Debug.WriteLine($"[ShotEditorView] WebView URL updated successfully");
    }

    private async Task<bool> TryTogglePlayAsync()
    {
        if (VideoWebView == null)
            return false;

        try
        {
            await VideoWebView.ExecuteScriptAsync("window.togglePlay && window.togglePlay()");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? NormalizeVideoPath(string? videoPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath))
            return null;

        return File.Exists(videoPath) ? Path.GetFullPath(videoPath) : null;
    }

    private static Uri? BuildPlayerUri(string? videoPath, bool autoplay)
    {
        var playerPath = GetPlayerHtmlPath();
        if (playerPath == null)
            return null;

        var playerUri = new Uri(playerPath);
        if (string.IsNullOrWhiteSpace(videoPath))
            return playerUri;

        var videoUri = new Uri(videoPath).AbsoluteUri;
        var query = $"src={Uri.EscapeDataString(videoUri)}";
        if (autoplay)
            query += "&autoplay=1";

        var builder = new UriBuilder(playerUri)
        {
            Query = query
        };

        return builder.Uri;
    }

    private static string? GetPlayerHtmlPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "App", "Assets", "VideoPlayer", "player.html");
        return File.Exists(path) ? path : null;
    }
}

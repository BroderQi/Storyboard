using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LibVLCSharp.Shared;
using Storyboard.Models;
using System;
using System.ComponentModel;
using System.IO;

namespace Storyboard.Views;

public partial class ShotEditorView : UserControl
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private ShotItem? _shot;
    private bool _videoViewReady;
    private string? _pendingVideoPath;

    public ShotEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        EnsurePlayer();
        UpdateMedia(_shot?.GeneratedVideoPath);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_shot != null)
            _shot.PropertyChanged -= OnShotPropertyChanged;

        _mediaPlayer?.Stop();
        _currentMedia?.Dispose();
        _currentMedia = null;

        if (VideoView != null)
            VideoView.MediaPlayer = null;

        _mediaPlayer?.Dispose();
        _mediaPlayer = null;
        _libVlc?.Dispose();
        _libVlc = null;
        _videoViewReady = false;
        _pendingVideoPath = null;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_shot != null)
            _shot.PropertyChanged -= OnShotPropertyChanged;

        _shot = DataContext as ShotItem;
        if (_shot != null)
            _shot.PropertyChanged += OnShotPropertyChanged;

        UpdateMedia(_shot?.GeneratedVideoPath);
    }

    private void OnShotPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShotItem.GeneratedVideoPath))
            UpdateMedia(_shot?.GeneratedVideoPath);
    }

    private void EnsurePlayer()
    {
        if (_mediaPlayer != null)
            return;

        Core.Initialize();
        _libVlc = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVlc);

        if (VideoView != null)
            VideoView.MediaPlayer = _mediaPlayer;
    }

    private void UpdateMedia(string? videoPath)
    {
        if (_mediaPlayer == null)
            return;

        if (!_videoViewReady)
        {
            _pendingVideoPath = videoPath;
            return;
        }

        _mediaPlayer.Stop();
        _currentMedia?.Dispose();
        _currentMedia = null;

        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            return;

        _currentMedia = new Media(_libVlc!, new Uri(videoPath));
        _mediaPlayer.Media = _currentMedia;
    }

    private void OnVideoViewAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _videoViewReady = true;
        EnsurePlayer();

        if (_pendingVideoPath != null)
        {
            var path = _pendingVideoPath;
            _pendingVideoPath = null;
            UpdateMedia(path);
        }
    }

    private void OnVideoViewDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _videoViewReady = false;
        _pendingVideoPath = null;
    }

    private void OnTogglePlayClicked(object? sender, RoutedEventArgs e)
    {
        EnsurePlayer();

        if (_mediaPlayer == null)
            return;

        if (_mediaPlayer.Media == null)
            UpdateMedia(_shot?.GeneratedVideoPath);

        if (_mediaPlayer.Media == null)
            return;

        if (_mediaPlayer.IsPlaying)
            _mediaPlayer.Pause();
        else
            _mediaPlayer.Play();
    }
}

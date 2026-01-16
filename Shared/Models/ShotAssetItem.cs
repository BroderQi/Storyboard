using CommunityToolkit.Mvvm.ComponentModel;
using Storyboard.Domain.Entities;

namespace Storyboard.Models;

public partial class ShotAssetItem : ObservableObject
{
    [ObservableProperty]
    private ShotAssetType _type;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string? _thumbnailPath;

    [ObservableProperty]
    private string? _videoThumbnailPath;

    partial void OnThumbnailPathChanged(string? value)
    {
        OnPropertyChanged(nameof(DisplayPath));
    }

    partial void OnVideoThumbnailPathChanged(string? value)
    {
        OnPropertyChanged(nameof(DisplayPath));
    }

    [ObservableProperty]
    private string? _prompt;

    [ObservableProperty]
    private string? _model;

    [ObservableProperty]
    private DateTimeOffset _createdAt = DateTimeOffset.Now;

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayName => Type switch
    {
        ShotAssetType.FirstFrameImage => "首帧",
        ShotAssetType.LastFrameImage => "尾帧",
        ShotAssetType.GeneratedVideo => "成品视频",
        _ => "资源"
    };

    public string? DisplayPath => Type switch
    {
        ShotAssetType.GeneratedVideo => !string.IsNullOrWhiteSpace(VideoThumbnailPath) ? VideoThumbnailPath : (!string.IsNullOrWhiteSpace(ThumbnailPath) ? ThumbnailPath : null),
        _ => !string.IsNullOrWhiteSpace(ThumbnailPath) ? ThumbnailPath : FilePath
    };
}

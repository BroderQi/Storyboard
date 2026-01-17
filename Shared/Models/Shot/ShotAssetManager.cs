using CommunityToolkit.Mvvm.ComponentModel;
using Storyboard.Models;
using Storyboard.Domain.Entities;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Storyboard.Models.Shot;

/// <summary>
/// 镜头资产管理器
/// </summary>
public partial class ShotAssetManager : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ShotAssetItem> _firstFrameAssets = new();

    [ObservableProperty]
    private ObservableCollection<ShotAssetItem> _lastFrameAssets = new();

    [ObservableProperty]
    private ObservableCollection<ShotAssetItem> _videoAssets = new();

    [ObservableProperty]
    private string? _firstFrameImagePath;

    [ObservableProperty]
    private string? _lastFrameImagePath;

    [ObservableProperty]
    private string? _generatedVideoPath;

    public string? VideoOutputPath => GeneratedVideoPath;

    partial void OnFirstFrameImagePathChanged(string? value)
    {
        UpdateAssetSelections(ShotAssetType.FirstFrameImage);
    }

    partial void OnLastFrameImagePathChanged(string? value)
    {
        UpdateAssetSelections(ShotAssetType.LastFrameImage);
    }

    partial void OnGeneratedVideoPathChanged(string? value)
    {
        UpdateAssetSelections(ShotAssetType.GeneratedVideo);
    }

    public void SelectAsset(ShotAssetItem? asset)
    {
        if (asset == null || string.IsNullOrWhiteSpace(asset.FilePath))
            return;

        switch (asset.Type)
        {
            case ShotAssetType.FirstFrameImage:
                FirstFrameImagePath = asset.FilePath;
                break;
            case ShotAssetType.LastFrameImage:
                LastFrameImagePath = asset.FilePath;
                break;
            case ShotAssetType.GeneratedVideo:
                GeneratedVideoPath = asset.FilePath;
                break;
        }

        UpdateAssetSelections(asset.Type);
    }

    private void UpdateAssetSelections(ShotAssetType type)
    {
        ObservableCollection<ShotAssetItem>? list = type switch
        {
            ShotAssetType.FirstFrameImage => FirstFrameAssets,
            ShotAssetType.LastFrameImage => LastFrameAssets,
            ShotAssetType.GeneratedVideo => VideoAssets,
            _ => null
        };

        if (list == null)
            return;

        var selectedPath = type switch
        {
            ShotAssetType.FirstFrameImage => FirstFrameImagePath,
            ShotAssetType.LastFrameImage => LastFrameImagePath,
            ShotAssetType.GeneratedVideo => GeneratedVideoPath,
            _ => null
        };

        foreach (var item in list)
            item.IsSelected = !string.IsNullOrWhiteSpace(selectedPath) &&
                              string.Equals(item.FilePath, selectedPath, StringComparison.OrdinalIgnoreCase);
    }
}

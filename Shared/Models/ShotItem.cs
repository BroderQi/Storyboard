using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using System.IO;
using System.Collections.ObjectModel;
using Storyboard.Domain.Entities;

namespace Storyboard.Models;

public partial class ShotItem : ObservableObject
{
    [ObservableProperty]
    private int _shotNumber;

    [ObservableProperty]
    private double _duration;

    [ObservableProperty]
    private double _startTime;

    [ObservableProperty]
    private double _endTime;

    [ObservableProperty]
    private string _firstFramePrompt = string.Empty;

    [ObservableProperty]
    private string _lastFramePrompt = string.Empty;

    [ObservableProperty]
    private string _shotType = string.Empty;

    [ObservableProperty]
    private string _coreContent = string.Empty;

    [ObservableProperty]
    private string _actionCommand = string.Empty;

    [ObservableProperty]
    private string _sceneSettings = string.Empty;

    [ObservableProperty]
    private string _selectedModel = string.Empty;

    [ObservableProperty]
    private string? _firstFrameImagePath;

    [ObservableProperty]
    private string? _lastFrameImagePath;

    [ObservableProperty]
    private bool _isFirstFrameGenerating;

    [ObservableProperty]
    private bool _isLastFrameGenerating;

    [ObservableProperty]
    private string? _generatedVideoPath;

    [ObservableProperty]
    private bool _isVideoGenerating;

    [ObservableProperty]
    private bool _isAiParsing;

    [ObservableProperty]
    private string? _materialThumbnailPath;

    [ObservableProperty]
    private string? _materialFilePath;

    [ObservableProperty]
    private ObservableCollection<ShotAssetItem> _firstFrameAssets = new();

    [ObservableProperty]
    private ObservableCollection<ShotAssetItem> _lastFrameAssets = new();

    [ObservableProperty]
    private ObservableCollection<ShotAssetItem> _videoAssets = new();

    [ObservableProperty]
    private bool _isChecked;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isHovered;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private double _timelineStartPosition;

    [ObservableProperty]
    private double _timelineWidth;

    public string? VideoOutputPath => GeneratedVideoPath;
    // Video generation no longer requires both first and last frame references.
    // Users may provide 0, 1 or 2 reference images. Provider will handle accordingly.
    public bool CanGenerateVideo => true;
    public bool CanGenerateVideoNow => !IsVideoGenerating;

    // Events for communicating with parent ViewModel
    public event EventHandler? DuplicateRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? AiParseRequested;
    public event EventHandler? GenerateFirstFrameRequested;
    public event EventHandler? GenerateLastFrameRequested;
    public event EventHandler? GenerateVideoRequested;

    public ShotItem(int shotNumber)
    {
        ShotNumber = shotNumber;
        Duration = 3.5;
        SelectedModel = string.Empty;
    }

    [RelayCommand]
    private void Duplicate()
    {
        DuplicateRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Delete()
    {
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void AIParse()
    {
        AiParseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ClearModel()
    {
        SelectedModel = string.Empty;
    }

    [RelayCommand]
    private void GenerateFirstFrame()
    {
        GenerateFirstFrameRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RegenerateFirstFrame()
    {
        GenerateFirstFrameRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void GenerateLastFrame()
    {
        GenerateLastFrameRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RegenerateLastFrame()
    {
        GenerateLastFrameRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void GenerateVideo()
    {
        GenerateVideoRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SelectAsset(ShotAssetItem? asset)
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

    partial void OnFirstFrameImagePathChanged(string? value)
    {
        UpdateAssetSelections(ShotAssetType.FirstFrameImage);
        OnPropertyChanged(nameof(CanGenerateVideo));
        OnPropertyChanged(nameof(CanGenerateVideoNow));
    }

    partial void OnLastFrameImagePathChanged(string? value)
    {
        UpdateAssetSelections(ShotAssetType.LastFrameImage);
        OnPropertyChanged(nameof(CanGenerateVideo));
        OnPropertyChanged(nameof(CanGenerateVideoNow));
    }

    partial void OnGeneratedVideoPathChanged(string? value)
    {
        UpdateAssetSelections(ShotAssetType.GeneratedVideo);
        OnPropertyChanged(nameof(VideoOutputPath));
    }

    partial void OnIsVideoGeneratingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGenerateVideoNow));
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
            item.IsSelected = !string.IsNullOrWhiteSpace(selectedPath) && string.Equals(item.FilePath, selectedPath, StringComparison.OrdinalIgnoreCase);
    }
}

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
    private string _coreContent = string.Empty;

    [ObservableProperty]
    private string _actionCommand = string.Empty;

    [ObservableProperty]
    private string _sceneSettings = string.Empty;

    [ObservableProperty]
    private string _selectedModel = string.Empty;

    // Image generation parameters
    [ObservableProperty]
    private string _imageSize = string.Empty;

    // First frame image parameters
    [ObservableProperty]
    private string _firstFrameNegativePrompt = string.Empty;

    [ObservableProperty]
    private string _firstFrameShotType = string.Empty;

    [ObservableProperty]
    private string _firstFrameComposition = string.Empty;

    [ObservableProperty]
    private string _firstFrameLightingType = string.Empty;

    [ObservableProperty]
    private string _firstFrameTimeOfDay = string.Empty;

    [ObservableProperty]
    private string _firstFrameColorStyle = string.Empty;

    // Last frame image parameters
    [ObservableProperty]
    private string _lastFrameNegativePrompt = string.Empty;

    [ObservableProperty]
    private string _lastFrameShotType = string.Empty;

    [ObservableProperty]
    private string _lastFrameComposition = string.Empty;

    [ObservableProperty]
    private string _lastFrameLightingType = string.Empty;

    [ObservableProperty]
    private string _lastFrameTimeOfDay = string.Empty;

    [ObservableProperty]
    private string _lastFrameColorStyle = string.Empty;

    // Legacy image professional parameters (kept for backward compatibility, but deprecated)
    [ObservableProperty]
    private string _negativePrompt = string.Empty;

    [ObservableProperty]
    private string _aspectRatio = string.Empty;

    [ObservableProperty]
    private string _lightingType = string.Empty;

    [ObservableProperty]
    private string _timeOfDay = string.Empty;

    [ObservableProperty]
    private string _composition = string.Empty;

    [ObservableProperty]
    private string _colorStyle = string.Empty;

    [ObservableProperty]
    private string _shotType = string.Empty;

    [ObservableProperty]
    private string _lensType = string.Empty;

    // Video generation parameters
    [ObservableProperty]
    private string _videoPrompt = string.Empty;

    [ObservableProperty]
    private string _sceneDescription = string.Empty;

    [ObservableProperty]
    private string _actionDescription = string.Empty;

    [ObservableProperty]
    private string _styleDescription = string.Empty;

    [ObservableProperty]
    private string _videoNegativePrompt = string.Empty;

    // Video professional parameters
    [ObservableProperty]
    private string _cameraMovement = string.Empty;

    [ObservableProperty]
    private string _shootingStyle = string.Empty;

    [ObservableProperty]
    private string _videoEffect = string.Empty;

    [ObservableProperty]
    private string _videoResolution = string.Empty;

    [ObservableProperty]
    private string _videoRatio = string.Empty;

    [ObservableProperty]
    private int _videoFrames;

    [ObservableProperty]
    private bool _useFirstFrameReference = true;

    [ObservableProperty]
    private bool _useLastFrameReference;

    [ObservableProperty]
    private int? _seed;

    [ObservableProperty]
    private bool _cameraFixed;

    [ObservableProperty]
    private bool _watermark;

    // Material info fields
    [ObservableProperty]
    private string _materialResolution = string.Empty;

    [ObservableProperty]
    private string _materialFileSize = string.Empty;

    [ObservableProperty]
    private string _materialFormat = string.Empty;

    [ObservableProperty]
    private string _materialColorTone = string.Empty;

    [ObservableProperty]
    private string _materialBrightness = string.Empty;

    // Collapsible section states
    // First frame collapsible states
    [ObservableProperty]
    private bool _isFirstFrameProfessionalParamsExpanded;

    [ObservableProperty]
    private bool _isFirstFrameNegativePromptExpanded;

    // Last frame collapsible states
    [ObservableProperty]
    private bool _isLastFrameProfessionalParamsExpanded;

    [ObservableProperty]
    private bool _isLastFrameNegativePromptExpanded;

    // Legacy collapsible states (kept for backward compatibility, but deprecated)
    [ObservableProperty]
    private bool _isImageProfessionalParamsExpanded;

    [ObservableProperty]
    private bool _isImageNegativePromptExpanded;

    // Video collapsible states
    [ObservableProperty]
    private bool _isVideoSceneActionExpanded;

    [ObservableProperty]
    private bool _isVideoProfessionalParamsExpanded;

    [ObservableProperty]
    private bool _isVideoNegativePromptExpanded;

    [ObservableProperty]
    private bool _isVideoAdvancedOptionsExpanded;

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
    private void ToggleImageProfessionalParams()
    {
        IsImageProfessionalParamsExpanded = !IsImageProfessionalParamsExpanded;
    }

    [RelayCommand]
    private void ToggleImageNegativePrompt()
    {
        IsImageNegativePromptExpanded = !IsImageNegativePromptExpanded;
    }

    [RelayCommand]
    private void ToggleFirstFrameProfessionalParams()
    {
        IsFirstFrameProfessionalParamsExpanded = !IsFirstFrameProfessionalParamsExpanded;
    }

    [RelayCommand]
    private void ToggleFirstFrameNegativePrompt()
    {
        IsFirstFrameNegativePromptExpanded = !IsFirstFrameNegativePromptExpanded;
    }

    [RelayCommand]
    private void ToggleLastFrameProfessionalParams()
    {
        IsLastFrameProfessionalParamsExpanded = !IsLastFrameProfessionalParamsExpanded;
    }

    [RelayCommand]
    private void ToggleLastFrameNegativePrompt()
    {
        IsLastFrameNegativePromptExpanded = !IsLastFrameNegativePromptExpanded;
    }

    [RelayCommand]
    private void ToggleVideoSceneAction()
    {
        IsVideoSceneActionExpanded = !IsVideoSceneActionExpanded;
    }

    [RelayCommand]
    private void ToggleVideoProfessionalParams()
    {
        IsVideoProfessionalParamsExpanded = !IsVideoProfessionalParamsExpanded;
    }

    [RelayCommand]
    private void ToggleVideoNegativePrompt()
    {
        IsVideoNegativePromptExpanded = !IsVideoNegativePromptExpanded;
    }

    [RelayCommand]
    private void ToggleVideoAdvancedOptions()
    {
        IsVideoAdvancedOptionsExpanded = !IsVideoAdvancedOptionsExpanded;
    }

    [RelayCommand]
    private void CombineToMainPrompt()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(SceneDescription))
            parts.Add(SceneDescription);
        if (!string.IsNullOrWhiteSpace(ActionDescription))
            parts.Add(ActionDescription);
        if (!string.IsNullOrWhiteSpace(StyleDescription))
            parts.Add(StyleDescription);

        if (parts.Count > 0)
            VideoPrompt = string.Join(", ", parts);
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

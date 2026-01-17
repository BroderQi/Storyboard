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

    [ObservableProperty]
    private string _imageQuality = "2K";

    [ObservableProperty]
    private bool _imageWatermark;

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

    [ObservableProperty]
    private string _firstFrameLensType = string.Empty;

    [ObservableProperty]
    private string _firstFrameImageSize = string.Empty;

    [ObservableProperty]
    private string _firstFrameAspectRatio = string.Empty;

    [ObservableProperty]
    private string _firstFrameSelectedModel = string.Empty;

    [ObservableProperty]
    private int? _firstFrameSeed;

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

    [ObservableProperty]
    private string _lastFrameLensType = string.Empty;

    [ObservableProperty]
    private string _lastFrameImageSize = string.Empty;

    [ObservableProperty]
    private string _lastFrameAspectRatio = string.Empty;

    [ObservableProperty]
    private string _lastFrameSelectedModel = string.Empty;

    [ObservableProperty]
    private int? _lastFrameSeed;

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
    private string _videoResolution = "720p";

    [ObservableProperty]
    private string _videoRatio = "16:9";

    [ObservableProperty]
    private int _videoFrames;

    [ObservableProperty]
    private bool _useFirstFrameReference = true;

    [ObservableProperty]
    private bool _useLastFrameReference;

    [ObservableProperty]
    private bool _useReferenceImages;

    [ObservableProperty]
    private bool _generateAudio;

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

    // Video generation mode selection
    [ObservableProperty]
    private bool _isTextToVideoMode;

    [ObservableProperty]
    private bool _useDurationMode = true;

    [ObservableProperty]
    private bool _useCustomSeed;

    // Video generation presets
    [ObservableProperty]
    private string _selectedPreset = string.Empty;

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

    // Computed properties for UI
    public bool IsNotReferenceMode => !UseReferenceImages;

    public bool SupportsResolution => !UseReferenceImages && !SelectedModel.Contains("t2v", StringComparison.OrdinalIgnoreCase);

    public bool SupportsAudio => SelectedModel.Contains("seedance-1-5-pro", StringComparison.OrdinalIgnoreCase);

    public int MinDuration => SelectedModel.Contains("1-5-pro") ? 4 : 2;

    public int EstimatedFrames => (int)(Duration * 24);

    public string ResolutionWarning
    {
        get
        {
            if (UseReferenceImages)
                return "参考图模式不支持分辨率设置";
            if (SelectedModel.Contains("t2v", StringComparison.OrdinalIgnoreCase))
                return "T2V 模型不支持分辨率设置";
            return string.Empty;
        }
    }

    public string DurationHint
    {
        get
        {
            if (SelectedModel.Contains("1-5-pro"))
                return "Seedance 1.5 Pro 支持 4-12 秒";
            return "支持 2-12 秒";
        }
    }

    public string ParameterSummary
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"模式: {GetModeName()}");
            sb.AppendLine($"宽高比: {VideoRatio}");
            if (SupportsResolution && !string.IsNullOrWhiteSpace(VideoResolution))
                sb.AppendLine($"分辨率: {VideoResolution}");
            if (UseDurationMode)
                sb.AppendLine($"时长: {Duration:F1} 秒");
            else if (VideoFrames > 0)
                sb.AppendLine($"帧数: {VideoFrames} 帧");
            if (GenerateAudio)
                sb.AppendLine("音频: 是");
            if (Watermark)
                sb.AppendLine("水印: 是");
            if (CameraFixed)
                sb.AppendLine("固定镜头: 是");
            if (UseCustomSeed && Seed.HasValue)
                sb.AppendLine($"种子: {Seed}");
            return sb.ToString();
        }
    }

    private string GetModeName()
    {
        if (UseReferenceImages) return "参考图模式";
        if (UseFirstFrameReference && UseLastFrameReference) return "首尾帧模式";
        if (UseFirstFrameReference) return "首帧模式";
        return "纯文本模式";
    }

    // Events for communicating with parent ViewModel
    public event EventHandler? DuplicateRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? AiParseRequested;
    public event EventHandler? GenerateFirstFrameRequested;
    public event EventHandler? GenerateLastFrameRequested;
    public event EventHandler? GenerateVideoRequested;

    // Image size options for ComboBox
    // Note: Volcengine API requires minimum 3,686,400 pixels (e.g., 2560x1440 for 16:9)
    public ObservableCollection<string> ImageSizeOptions { get; } = new()
    {
        "",
        "2048x2048",  // 4,194,304 pixels ✓
        "2560x1440",  // 3,686,400 pixels ✓ (16:9)
        "1440x2560",  // 3,686,400 pixels ✓ (9:16)
        "2304x1728",  // 3,981,312 pixels ✓ (4:3)
        "1728x2304",  // 3,981,312 pixels ✓ (3:4)
        "2688x1512",  // 4,064,256 pixels ✓ (16:9)
        "1512x2688"   // 4,064,256 pixels ✓ (9:16)
    };

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
        System.Diagnostics.Debug.WriteLine($"[ShotItem] GenerateVideo called for Shot {ShotNumber}");
        System.Diagnostics.Debug.WriteLine($"[ShotItem] VideoPrompt: '{VideoPrompt}'");
        System.Diagnostics.Debug.WriteLine($"[ShotItem] GenerateVideoRequested subscribers: {GenerateVideoRequested?.GetInvocationList().Length ?? 0}");
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
    private void GenerateRandomSeed()
    {
        Seed = Random.Shared.Next(0, int.MaxValue);
    }

    [RelayCommand]
    private void ApplyPreset(string? presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName))
            return;

        switch (presetName)
        {
            case "抖音竖屏":
                VideoRatio = "9:16";
                VideoResolution = "1080p";
                Duration = 5;
                UseDurationMode = true;
                break;
            case "横屏高清":
                VideoRatio = "16:9";
                VideoResolution = "1080p";
                Duration = 8;
                UseDurationMode = true;
                break;
            case "快速预览":
                VideoRatio = "16:9";
                VideoResolution = "480p";
                Duration = 3;
                UseDurationMode = true;
                break;
            case "方形社交":
                VideoRatio = "1:1";
                VideoResolution = "1080p";
                Duration = 5;
                UseDurationMode = true;
                break;
            case "电影宽屏":
                VideoRatio = "21:9";
                VideoResolution = "1080p";
                Duration = 10;
                UseDurationMode = true;
                break;
        }

        SelectedPreset = presetName;
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

    partial void OnUseReferenceImagesChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotReferenceMode));
        OnPropertyChanged(nameof(SupportsResolution));
        OnPropertyChanged(nameof(ResolutionWarning));
    }

    partial void OnSelectedModelChanged(string value)
    {
        OnPropertyChanged(nameof(SupportsResolution));
        OnPropertyChanged(nameof(SupportsAudio));
        OnPropertyChanged(nameof(MinDuration));
        OnPropertyChanged(nameof(ResolutionWarning));
        OnPropertyChanged(nameof(DurationHint));
    }

    partial void OnDurationChanged(double value)
    {
        OnPropertyChanged(nameof(EstimatedFrames));
        OnPropertyChanged(nameof(ParameterSummary));
    }

    partial void OnVideoRatioChanged(string value)
    {
        OnPropertyChanged(nameof(ParameterSummary));
    }

    partial void OnVideoResolutionChanged(string value)
    {
        OnPropertyChanged(nameof(ParameterSummary));
    }

    partial void OnVideoFramesChanged(int value)
    {
        OnPropertyChanged(nameof(ParameterSummary));
    }

    partial void OnGenerateAudioChanged(bool value)
    {
        OnPropertyChanged(nameof(ParameterSummary));
    }

    partial void OnWatermarkChanged(bool value)
    {
        OnPropertyChanged(nameof(ParameterSummary));
    }

    partial void OnCameraFixedChanged(bool value)
    {
        OnPropertyChanged(nameof(ParameterSummary));
    }

    partial void OnSeedChanged(int? value)
    {
        OnPropertyChanged(nameof(ParameterSummary));
    }

    partial void OnUseCustomSeedChanged(bool value)
    {
        OnPropertyChanged(nameof(ParameterSummary));
    }

    partial void OnUseDurationModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ParameterSummary));
    }

    partial void OnUseFirstFrameReferenceChanged(bool value)
    {
        OnPropertyChanged(nameof(ParameterSummary));
    }

    partial void OnUseLastFrameReferenceChanged(bool value)
    {
        OnPropertyChanged(nameof(ParameterSummary));
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

    /// <summary>
    /// 批量更新 AI 解析结果，避免多次触发 PropertyChanged 事件
    /// </summary>
    public void ApplyAiAnalysisResult(AiShotDescription result)
    {
        try
        {
            // 注意：CommunityToolkit.Mvvm 的 ObservableObject 没有内置的暂停通知机制
            // 所以我们直接更新字段，然后手动触发一次通知

            // 基本信息
            if (!string.IsNullOrWhiteSpace(result.ShotType))
                ShotType = result.ShotType;

            if (!string.IsNullOrWhiteSpace(result.CoreContent))
                CoreContent = result.CoreContent;

            if (!string.IsNullOrWhiteSpace(result.ActionCommand))
                ActionCommand = result.ActionCommand;

            if (!string.IsNullOrWhiteSpace(result.SceneSettings))
                SceneSettings = result.SceneSettings;

            // 首帧和尾帧提示词
            if (!string.IsNullOrWhiteSpace(result.FirstFramePrompt))
                FirstFramePrompt = result.FirstFramePrompt;

            if (!string.IsNullOrWhiteSpace(result.LastFramePrompt))
                LastFramePrompt = result.LastFramePrompt;

            // 图片专业参数 - 应用到首帧和尾帧（AI 返回的是通用参数）
            if (!string.IsNullOrWhiteSpace(result.Composition))
            {
                FirstFrameComposition = result.Composition;
                LastFrameComposition = result.Composition;
                Composition = result.Composition; // 兼容旧版
            }

            if (!string.IsNullOrWhiteSpace(result.LightingType))
            {
                FirstFrameLightingType = result.LightingType;
                LastFrameLightingType = result.LightingType;
                LightingType = result.LightingType; // 兼容旧版
            }

            if (!string.IsNullOrWhiteSpace(result.TimeOfDay))
            {
                FirstFrameTimeOfDay = result.TimeOfDay;
                LastFrameTimeOfDay = result.TimeOfDay;
                TimeOfDay = result.TimeOfDay; // 兼容旧版
            }

            if (!string.IsNullOrWhiteSpace(result.ColorStyle))
            {
                FirstFrameColorStyle = result.ColorStyle;
                LastFrameColorStyle = result.ColorStyle;
                ColorStyle = result.ColorStyle; // 兼容旧版
            }

            if (!string.IsNullOrWhiteSpace(result.NegativePrompt))
            {
                FirstFrameNegativePrompt = result.NegativePrompt;
                LastFrameNegativePrompt = result.NegativePrompt;
                NegativePrompt = result.NegativePrompt; // 兼容旧版
            }

            // 图片尺寸
            if (!string.IsNullOrWhiteSpace(result.ImageSize))
                ImageSize = result.ImageSize;

            // 视频参数
            if (!string.IsNullOrWhiteSpace(result.VideoPrompt))
                VideoPrompt = result.VideoPrompt;

            if (!string.IsNullOrWhiteSpace(result.SceneDescription))
                SceneDescription = result.SceneDescription;

            if (!string.IsNullOrWhiteSpace(result.ActionDescription))
                ActionDescription = result.ActionDescription;

            if (!string.IsNullOrWhiteSpace(result.StyleDescription))
                StyleDescription = result.StyleDescription;

            if (!string.IsNullOrWhiteSpace(result.CameraMovement))
                CameraMovement = result.CameraMovement;

            if (!string.IsNullOrWhiteSpace(result.ShootingStyle))
                ShootingStyle = result.ShootingStyle;

            if (!string.IsNullOrWhiteSpace(result.VideoEffect))
                VideoEffect = result.VideoEffect;

            if (!string.IsNullOrWhiteSpace(result.VideoNegativePrompt))
                VideoNegativePrompt = result.VideoNegativePrompt;

            if (!string.IsNullOrWhiteSpace(result.VideoResolution))
                VideoResolution = result.VideoResolution;

            if (!string.IsNullOrWhiteSpace(result.VideoRatio))
                VideoRatio = result.VideoRatio;
        }
        catch
        {
            // 确保不会因为异常导致状态不一致
        }
    }
}

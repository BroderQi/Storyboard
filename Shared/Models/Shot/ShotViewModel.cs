using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Storyboard.Messages;
using System;
using System.Collections.Generic;

namespace Storyboard.Models.Shot;

/// <summary>
/// 镜头视图模型 - 聚合所有镜头相关的子模型
/// </summary>
public partial class ShotViewModel : ObservableObject
{
    private readonly IMessenger _messenger;

    // 子模型
    public ShotEntity Entity { get; }
    public ImageGenerationParams ImageParams { get; }
    public VideoGenerationParams VideoParams { get; }
    public MaterialInfo Material { get; }
    public ShotUIState UIState { get; }
    public ShotAssetManager Assets { get; }

    // 生成状态
    [ObservableProperty]
    private bool _isFirstFrameGenerating;

    [ObservableProperty]
    private bool _isLastFrameGenerating;

    [ObservableProperty]
    private bool _isVideoGenerating;

    [ObservableProperty]
    private bool _isAiParsing;

    // 便捷属性 - 委托到子模型
    public int ShotNumber
    {
        get => Entity.ShotNumber;
        set => Entity.ShotNumber = value;
    }

    public double Duration
    {
        get => Entity.Duration;
        set => Entity.Duration = value;
    }

    public double StartTime
    {
        get => Entity.StartTime;
        set => Entity.StartTime = value;
    }

    public double EndTime
    {
        get => Entity.EndTime;
        set => Entity.EndTime = value;
    }

    public string? FirstFrameImagePath
    {
        get => Assets.FirstFrameImagePath;
        set => Assets.FirstFrameImagePath = value;
    }

    public string? LastFrameImagePath
    {
        get => Assets.LastFrameImagePath;
        set => Assets.LastFrameImagePath = value;
    }

    public string? GeneratedVideoPath
    {
        get => Assets.GeneratedVideoPath;
        set => Assets.GeneratedVideoPath = value;
    }

    public string? VideoOutputPath => Assets.VideoOutputPath;

    // 计算属性
    public bool CanGenerateVideo => true;
    public bool CanGenerateVideoNow => !IsVideoGenerating;

    partial void OnIsVideoGeneratingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGenerateVideoNow));
    }

    public ShotViewModel(int shotNumber, IMessenger? messenger = null)
    {
        _messenger = messenger ?? WeakReferenceMessenger.Default;

        Entity = new ShotEntity(shotNumber);
        ImageParams = new ImageGenerationParams();
        VideoParams = new VideoGenerationParams();
        Material = new MaterialInfo();
        UIState = new ShotUIState();
        Assets = new ShotAssetManager();

        // 订阅子模型的属性变更以触发计算属性更新
        Assets.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Assets.FirstFrameImagePath))
                OnPropertyChanged(nameof(FirstFrameImagePath));
            if (e.PropertyName == nameof(Assets.LastFrameImagePath))
                OnPropertyChanged(nameof(LastFrameImagePath));
            if (e.PropertyName == nameof(Assets.GeneratedVideoPath))
            {
                OnPropertyChanged(nameof(GeneratedVideoPath));
                OnPropertyChanged(nameof(VideoOutputPath));
            }
        };
    }

    // 命令 - 通过消息总线发送请求
    [RelayCommand]
    private void Duplicate()
    {
        _messenger.Send(new ShotDuplicateRequestedMessage(this.ToShotItem()));
    }

    [RelayCommand]
    private void Delete()
    {
        _messenger.Send(new ShotDeleteRequestedMessage(this.ToShotItem()));
    }

    [RelayCommand]
    private void AIParse()
    {
        _messenger.Send(new AiParseRequestedMessage(this.ToShotItem()));
    }

    [RelayCommand]
    private void ClearModel()
    {
        Entity.SelectedModel = string.Empty;
    }

    [RelayCommand]
    private void GenerateFirstFrame()
    {
        _messenger.Send(new ImageGenerationRequestedMessage(this.ToShotItem(), true));
    }

    [RelayCommand]
    private void RegenerateFirstFrame()
    {
        _messenger.Send(new ImageGenerationRequestedMessage(this.ToShotItem(), true));
    }

    [RelayCommand]
    private void GenerateLastFrame()
    {
        _messenger.Send(new ImageGenerationRequestedMessage(this.ToShotItem(), false));
    }

    [RelayCommand]
    private void RegenerateLastFrame()
    {
        _messenger.Send(new ImageGenerationRequestedMessage(this.ToShotItem(), false));
    }

    [RelayCommand]
    private void GenerateVideo()
    {
        _messenger.Send(new VideoGenerationRequestedMessage(this.ToShotItem()));
    }

    [RelayCommand]
    private void ToggleImageProfessionalParams()
    {
        ImageParams.IsImageProfessionalParamsExpanded = !ImageParams.IsImageProfessionalParamsExpanded;
    }

    [RelayCommand]
    private void ToggleImageNegativePrompt()
    {
        ImageParams.IsImageNegativePromptExpanded = !ImageParams.IsImageNegativePromptExpanded;
    }

    [RelayCommand]
    private void ToggleVideoSceneAction()
    {
        VideoParams.IsVideoSceneActionExpanded = !VideoParams.IsVideoSceneActionExpanded;
    }

    [RelayCommand]
    private void ToggleVideoProfessionalParams()
    {
        VideoParams.IsVideoProfessionalParamsExpanded = !VideoParams.IsVideoProfessionalParamsExpanded;
    }

    [RelayCommand]
    private void ToggleVideoNegativePrompt()
    {
        VideoParams.IsVideoNegativePromptExpanded = !VideoParams.IsVideoNegativePromptExpanded;
    }

    [RelayCommand]
    private void ToggleVideoAdvancedOptions()
    {
        VideoParams.IsVideoAdvancedOptionsExpanded = !VideoParams.IsVideoAdvancedOptionsExpanded;
    }

    [RelayCommand]
    private void CombineToMainPrompt()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(VideoParams.SceneDescription))
            parts.Add(VideoParams.SceneDescription);
        if (!string.IsNullOrWhiteSpace(VideoParams.ActionDescription))
            parts.Add(VideoParams.ActionDescription);
        if (!string.IsNullOrWhiteSpace(VideoParams.StyleDescription))
            parts.Add(VideoParams.StyleDescription);

        if (parts.Count > 0)
            VideoParams.VideoPrompt = string.Join(", ", parts);
    }

    [RelayCommand]
    private void SelectAsset(ShotAssetItem? asset)
    {
        Assets.SelectAsset(asset);
    }

    // 转换方法 - 用于向后兼容，将 ShotViewModel 转换为 ShotItem
    // 这是临时方法，在完全迁移后可以移除
    public ShotItem ToShotItem()
    {
        var item = new ShotItem(Entity.ShotNumber)
        {
            Duration = Entity.Duration,
            StartTime = Entity.StartTime,
            EndTime = Entity.EndTime,
            FirstFramePrompt = ImageParams.FirstFramePrompt,
            LastFramePrompt = ImageParams.LastFramePrompt,
            ShotType = Entity.ShotType,
            CoreContent = Entity.CoreContent,
            ActionCommand = Entity.ActionCommand,
            SceneSettings = Entity.SceneSettings,
            SelectedModel = Entity.SelectedModel,
            ImageSize = ImageParams.ImageSize,
            NegativePrompt = ImageParams.NegativePrompt,
            AspectRatio = ImageParams.AspectRatio,
            LightingType = ImageParams.LightingType,
            TimeOfDay = ImageParams.TimeOfDay,
            Composition = ImageParams.Composition,
            ColorStyle = ImageParams.ColorStyle,
            LensType = ImageParams.LensType,
            VideoPrompt = VideoParams.VideoPrompt,
            SceneDescription = VideoParams.SceneDescription,
            ActionDescription = VideoParams.ActionDescription,
            StyleDescription = VideoParams.StyleDescription,
            VideoNegativePrompt = VideoParams.VideoNegativePrompt,
            CameraMovement = VideoParams.CameraMovement,
            ShootingStyle = VideoParams.ShootingStyle,
            VideoEffect = VideoParams.VideoEffect,
            VideoResolution = VideoParams.VideoResolution,
            VideoRatio = VideoParams.VideoRatio,
            VideoFrames = VideoParams.VideoFrames,
            UseFirstFrameReference = VideoParams.UseFirstFrameReference,
            UseLastFrameReference = VideoParams.UseLastFrameReference,
            Seed = ImageParams.Seed,
            CameraFixed = VideoParams.CameraFixed,
            Watermark = VideoParams.Watermark,
            MaterialResolution = Material.MaterialResolution,
            MaterialFileSize = Material.MaterialFileSize,
            MaterialFormat = Material.MaterialFormat,
            MaterialColorTone = Material.MaterialColorTone,
            MaterialBrightness = Material.MaterialBrightness,
            MaterialThumbnailPath = Material.MaterialThumbnailPath,
            MaterialFilePath = Material.MaterialFilePath,
            FirstFrameImagePath = Assets.FirstFrameImagePath,
            LastFrameImagePath = Assets.LastFrameImagePath,
            GeneratedVideoPath = Assets.GeneratedVideoPath,
            IsFirstFrameGenerating = IsFirstFrameGenerating,
            IsLastFrameGenerating = IsLastFrameGenerating,
            IsVideoGenerating = IsVideoGenerating,
            IsAiParsing = IsAiParsing,
            IsChecked = UIState.IsChecked,
            IsSelected = UIState.IsSelected,
            IsHovered = UIState.IsHovered,
            SelectedTabIndex = UIState.SelectedTabIndex,
            TimelineStartPosition = UIState.TimelineStartPosition,
            TimelineWidth = UIState.TimelineWidth
        };

        // 复制资产集合
        foreach (var asset in Assets.FirstFrameAssets)
            item.FirstFrameAssets.Add(asset);
        foreach (var asset in Assets.LastFrameAssets)
            item.LastFrameAssets.Add(asset);
        foreach (var asset in Assets.VideoAssets)
            item.VideoAssets.Add(asset);

        return item;
    }

    // 从 ShotItem 创建 ShotViewModel
    public static ShotViewModel FromShotItem(ShotItem item, IMessenger? messenger = null)
    {
        var vm = new ShotViewModel(item.ShotNumber, messenger);

        vm.Entity.Duration = item.Duration;
        vm.Entity.StartTime = item.StartTime;
        vm.Entity.EndTime = item.EndTime;
        vm.Entity.ShotType = item.ShotType;
        vm.Entity.CoreContent = item.CoreContent;
        vm.Entity.ActionCommand = item.ActionCommand;
        vm.Entity.SceneSettings = item.SceneSettings;
        vm.Entity.SelectedModel = item.SelectedModel;

        vm.ImageParams.FirstFramePrompt = item.FirstFramePrompt;
        vm.ImageParams.LastFramePrompt = item.LastFramePrompt;
        vm.ImageParams.ImageSize = item.ImageSize;
        vm.ImageParams.NegativePrompt = item.NegativePrompt;
        vm.ImageParams.AspectRatio = item.AspectRatio;
        vm.ImageParams.LightingType = item.LightingType;
        vm.ImageParams.TimeOfDay = item.TimeOfDay;
        vm.ImageParams.Composition = item.Composition;
        vm.ImageParams.ColorStyle = item.ColorStyle;
        vm.ImageParams.LensType = item.LensType;
        vm.ImageParams.Seed = item.Seed;

        vm.VideoParams.VideoPrompt = item.VideoPrompt;
        vm.VideoParams.SceneDescription = item.SceneDescription;
        vm.VideoParams.ActionDescription = item.ActionDescription;
        vm.VideoParams.StyleDescription = item.StyleDescription;
        vm.VideoParams.VideoNegativePrompt = item.VideoNegativePrompt;
        vm.VideoParams.CameraMovement = item.CameraMovement;
        vm.VideoParams.ShootingStyle = item.ShootingStyle;
        vm.VideoParams.VideoEffect = item.VideoEffect;
        vm.VideoParams.VideoResolution = item.VideoResolution;
        vm.VideoParams.VideoRatio = item.VideoRatio;
        vm.VideoParams.VideoFrames = item.VideoFrames;
        vm.VideoParams.UseFirstFrameReference = item.UseFirstFrameReference;
        vm.VideoParams.UseLastFrameReference = item.UseLastFrameReference;
        vm.VideoParams.CameraFixed = item.CameraFixed;
        vm.VideoParams.Watermark = item.Watermark;

        vm.Material.MaterialResolution = item.MaterialResolution;
        vm.Material.MaterialFileSize = item.MaterialFileSize;
        vm.Material.MaterialFormat = item.MaterialFormat;
        vm.Material.MaterialColorTone = item.MaterialColorTone;
        vm.Material.MaterialBrightness = item.MaterialBrightness;
        vm.Material.MaterialThumbnailPath = item.MaterialThumbnailPath;
        vm.Material.MaterialFilePath = item.MaterialFilePath;

        vm.Assets.FirstFrameImagePath = item.FirstFrameImagePath;
        vm.Assets.LastFrameImagePath = item.LastFrameImagePath;
        vm.Assets.GeneratedVideoPath = item.GeneratedVideoPath;

        vm.IsFirstFrameGenerating = item.IsFirstFrameGenerating;
        vm.IsLastFrameGenerating = item.IsLastFrameGenerating;
        vm.IsVideoGenerating = item.IsVideoGenerating;
        vm.IsAiParsing = item.IsAiParsing;

        vm.UIState.IsChecked = item.IsChecked;
        vm.UIState.IsSelected = item.IsSelected;
        vm.UIState.IsHovered = item.IsHovered;
        vm.UIState.SelectedTabIndex = item.SelectedTabIndex;
        vm.UIState.TimelineStartPosition = item.TimelineStartPosition;
        vm.UIState.TimelineWidth = item.TimelineWidth;

        // 复制资产集合
        foreach (var asset in item.FirstFrameAssets)
            vm.Assets.FirstFrameAssets.Add(asset);
        foreach (var asset in item.LastFrameAssets)
            vm.Assets.LastFrameAssets.Add(asset);
        foreach (var asset in item.VideoAssets)
            vm.Assets.VideoAssets.Add(asset);

        return vm;
    }
}

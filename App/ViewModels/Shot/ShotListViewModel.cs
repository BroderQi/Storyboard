using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace Storyboard.ViewModels.Shot;

/// <summary>
/// 镜头列表 ViewModel - 负责镜头集合管理和 CRUD 操作
/// </summary>
public partial class ShotListViewModel : ObservableObject
{
    private readonly IMessenger _messenger;
    private readonly ILogger<ShotListViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<ShotItem> _shots = new();

    [ObservableProperty]
    private ShotItem? _selectedShot;

    [ObservableProperty]
    private double _totalDuration;

    [ObservableProperty]
    private int _completedShotsCount;

    [ObservableProperty]
    private int _completedVideoShotsCount;

    public bool HasShots => Shots.Count > 0;
    public bool HasSelectedShots => Shots.Any(s => s.IsChecked);
    public string SelectedShotsCountText => $"{Shots.Count(s => s.IsChecked)} 已选择";

    public ShotListViewModel(
        IMessenger messenger,
        ILogger<ShotListViewModel> logger)
    {
        _messenger = messenger;
        _logger = logger;

        // 监听镜头集合变化
        Shots.CollectionChanged += Shots_CollectionChanged;

        // 订阅消息
        _messenger.Register<ProjectCreatedMessage>(this, OnProjectCreated);
        _messenger.Register<ProjectOpenedMessage>(this, OnProjectOpened);
        _messenger.Register<ProjectDataLoadedMessage>(this, OnProjectDataLoaded);
        _messenger.Register<ProjectClosedMessage>(this, OnProjectClosed);
        _messenger.Register<ShotDuplicateRequestedMessage>(this, OnShotDuplicateRequested);
        _messenger.Register<ShotDeleteRequestedMessage>(this, OnShotDeleteRequested);
        _messenger.Register<FramesExtractedMessage>(this, OnFramesExtracted);
        _messenger.Register<RestoreSnapshotMessage>(this, OnRestoreSnapshot);

        // 订阅查询消息 - 允许其他ViewModel查询镜头数据
        _messenger.Register<GetAllShotsQuery>(this, (r, m) => m.Shots = Shots.ToList());
    }

    [RelayCommand]
    private void AddShot()
    {
        var startTime = Shots.Count == 0 ? 0 : Shots.Max(s => s.EndTime);
        var newShot = new ShotItem(Shots.Count + 1)
        {
            StartTime = startTime,
            EndTime = startTime + 3.5
        };

        AttachShotEventHandlers(newShot);
        Shots.Add(newShot);

        _messenger.Send(new ShotAddedMessage(newShot));
        _messenger.Send(new MarkUndoableChangeMessage());

        _logger.LogInformation("添加镜头: Shot {ShotNumber}", newShot.ShotNumber);
    }

    /// <summary>
    /// 添加一个已创建的分镜到列表
    /// </summary>
    public void AddShot(ShotItem shot)
    {
        AttachShotEventHandlers(shot);
        Shots.Add(shot);

        _messenger.Send(new ShotAddedMessage(shot));
        _messenger.Send(new MarkUndoableChangeMessage());

        _logger.LogInformation("添加镜头: Shot {ShotNumber}", shot.ShotNumber);
    }

    public void RenumberShots()
    {
        for (int i = 0; i < Shots.Count; i++)
        {
            Shots[i].ShotNumber = i + 1;
        }
    }

    public void RenumberShotsForDrag()
    {
        RenumberShots();
        UpdateSummaryCounts();
    }

    public void MoveShot(ShotItem source, ShotItem target)
    {
        MoveShot(source, target, false);
    }

    public void MoveShot(ShotItem source, ShotItem target, bool insertAfter)
    {
        var fromIndex = Shots.IndexOf(source);
        var toIndex = Shots.IndexOf(target);

        if (fromIndex < 0 || toIndex < 0)
            return;

        if (insertAfter)
            toIndex++;

        if (fromIndex < toIndex)
            toIndex--;

        if (fromIndex == toIndex)
            return;

        Shots.Move(fromIndex, toIndex);
        RenumberShots();

        _messenger.Send(new ShotMovedMessage(source, fromIndex, toIndex));
        _messenger.Send(new MarkUndoableChangeMessage());

        _logger.LogInformation("移动镜头: Shot {ShotNumber} from {FromIndex} to {ToIndex}",
            source.ShotNumber, fromIndex, toIndex);
    }

    private void OnShotDuplicateRequested(object recipient, ShotDuplicateRequestedMessage message)
    {
        var original = message.Shot;
        var index = Shots.IndexOf(original);
        if (index < 0)
            return;

        var duplicate = new ShotItem(original.ShotNumber + 1)
        {
            Duration = original.Duration,
            StartTime = original.EndTime,
            EndTime = original.EndTime + original.Duration,
            FirstFramePrompt = original.FirstFramePrompt,
            LastFramePrompt = original.LastFramePrompt,
            ShotType = original.ShotType,
            CoreContent = original.CoreContent,
            ActionCommand = original.ActionCommand,
            SceneSettings = original.SceneSettings,
            SelectedModel = original.SelectedModel,
            ImageSize = original.ImageSize,
            NegativePrompt = original.NegativePrompt,
            AspectRatio = original.AspectRatio,
            LightingType = original.LightingType,
            TimeOfDay = original.TimeOfDay,
            Composition = original.Composition,
            ColorStyle = original.ColorStyle,
            LensType = original.LensType,
            VideoPrompt = original.VideoPrompt,
            SceneDescription = original.SceneDescription,
            ActionDescription = original.ActionDescription,
            StyleDescription = original.StyleDescription,
            VideoNegativePrompt = original.VideoNegativePrompt,
            CameraMovement = original.CameraMovement,
            ShootingStyle = original.ShootingStyle,
            VideoEffect = original.VideoEffect,
            VideoResolution = original.VideoResolution,
            VideoRatio = original.VideoRatio,
            VideoFrames = original.VideoFrames,
            UseFirstFrameReference = original.UseFirstFrameReference,
            UseLastFrameReference = original.UseLastFrameReference,
            Seed = original.Seed,
            CameraFixed = original.CameraFixed,
            Watermark = original.Watermark
        };

        AttachShotEventHandlers(duplicate);
        Shots.Insert(index + 1, duplicate);
        RenumberShots();

        _messenger.Send(new ShotAddedMessage(duplicate));
        _messenger.Send(new MarkUndoableChangeMessage());

        _logger.LogInformation("复制镜头: Shot {ShotNumber}", original.ShotNumber);
    }

    private void OnShotDeleteRequested(object recipient, ShotDeleteRequestedMessage message)
    {
        var shot = message.Shot;
        if (!Shots.Contains(shot))
            return;

        DetachShotEventHandlers(shot);
        Shots.Remove(shot);
        RenumberShots();

        _messenger.Send(new ShotDeletedMessage(shot));
        _messenger.Send(new MarkUndoableChangeMessage());

        _logger.LogInformation("删除镜头: Shot {ShotNumber}", shot.ShotNumber);
    }

    private void AttachShotEventHandlers(ShotItem shot)
    {
        shot.DuplicateRequested += OnShotDuplicateRequestedEvent;
        shot.DeleteRequested += OnShotDeleteRequestedEvent;
        shot.AiParseRequested += OnShotAiParseRequestedEvent;
        shot.GenerateFirstFrameRequested += OnShotGenerateFirstFrameRequestedEvent;
        shot.GenerateLastFrameRequested += OnShotGenerateLastFrameRequestedEvent;
        shot.GenerateVideoRequested += OnShotGenerateVideoRequestedEvent;
        shot.PropertyChanged += Shot_PropertyChanged;
    }

    private void DetachShotEventHandlers(ShotItem shot)
    {
        shot.DuplicateRequested -= OnShotDuplicateRequestedEvent;
        shot.DeleteRequested -= OnShotDeleteRequestedEvent;
        shot.AiParseRequested -= OnShotAiParseRequestedEvent;
        shot.GenerateFirstFrameRequested -= OnShotGenerateFirstFrameRequestedEvent;
        shot.GenerateLastFrameRequested -= OnShotGenerateLastFrameRequestedEvent;
        shot.GenerateVideoRequested -= OnShotGenerateVideoRequestedEvent;
        shot.PropertyChanged -= Shot_PropertyChanged;
    }

    private void OnShotDuplicateRequestedEvent(object? sender, EventArgs e)
    {
        if (sender is ShotItem shot)
            _messenger.Send(new ShotDuplicateRequestedMessage(shot));
    }

    private void OnShotDeleteRequestedEvent(object? sender, EventArgs e)
    {
        if (sender is ShotItem shot)
            _messenger.Send(new ShotDeleteRequestedMessage(shot));
    }

    private void OnShotAiParseRequestedEvent(object? sender, EventArgs e)
    {
        if (sender is ShotItem shot)
            _messenger.Send(new AiParseRequestedMessage(shot));
    }

    private void OnShotGenerateFirstFrameRequestedEvent(object? sender, EventArgs e)
    {
        if (sender is ShotItem shot)
            _messenger.Send(new ImageGenerationRequestedMessage(shot, true));
    }

    private void OnShotGenerateLastFrameRequestedEvent(object? sender, EventArgs e)
    {
        if (sender is ShotItem shot)
            _messenger.Send(new ImageGenerationRequestedMessage(shot, false));
    }

    private void OnShotGenerateVideoRequestedEvent(object? sender, EventArgs e)
    {
        if (sender is ShotItem shot)
        {
            _logger.LogInformation("镜头请求生成视频: Shot {ShotNumber}", shot.ShotNumber);
            _messenger.Send(new VideoGenerationRequestedMessage(shot));
        }
    }

    private void Shot_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is ShotItem shot)
        {
            _logger.LogInformation("镜头属性变更: ShotNumber={ShotNumber}, PropertyName={PropertyName}", shot.ShotNumber, e.PropertyName);

            // 忽略 UI 状态属性的变更，这些不需要保存到数据库
            if (e.PropertyName is nameof(ShotItem.IsSelected) or
                nameof(ShotItem.IsChecked) or
                nameof(ShotItem.IsHovered) or
                nameof(ShotItem.SelectedTabIndex) or
                nameof(ShotItem.TimelineStartPosition) or
                nameof(ShotItem.TimelineWidth))
            {
                return;
            }

            _messenger.Send(new ShotUpdatedMessage(shot));

            // 某些属性变更需要标记为可撤销
            if (e.PropertyName is nameof(ShotItem.Duration) or nameof(ShotItem.FirstFramePrompt) or nameof(ShotItem.LastFramePrompt))
            {
                _messenger.Send(new MarkUndoableChangeMessage());
            }
        }
    }

    private void Shots_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasShots));
        OnPropertyChanged(nameof(HasSelectedShots));
        OnPropertyChanged(nameof(SelectedShotsCountText));

        UpdateSummaryCounts();
    }

    private void UpdateSummaryCounts()
    {
        TotalDuration = Shots.Sum(s => s.Duration);
        CompletedShotsCount = Shots.Count(s =>
            !string.IsNullOrWhiteSpace(s.FirstFrameImagePath) &&
            !string.IsNullOrWhiteSpace(s.LastFrameImagePath));
        CompletedVideoShotsCount = Shots.Count(s =>
            !string.IsNullOrWhiteSpace(s.GeneratedVideoPath) &&
            System.IO.File.Exists(s.GeneratedVideoPath));

        OnPropertyChanged(nameof(TotalDuration));
        OnPropertyChanged(nameof(CompletedShotsCount));
        OnPropertyChanged(nameof(CompletedVideoShotsCount));
    }

    private void OnProjectCreated(object recipient, ProjectCreatedMessage message)
    {
        // 项目创建时，清空镜头列表，准备新项目
        foreach (var shot in Shots.ToList())
        {
            DetachShotEventHandlers(shot);
        }
        Shots.Clear();
        SelectedShot = null;

        _logger.LogInformation("项目创建完成，准备新项目: {ProjectId}", message.ProjectId);
    }

    private void OnProjectOpened(object recipient, ProjectOpenedMessage message)
    {
        // 项目打开时，镜头数据会由项目加载逻辑填充
        _logger.LogInformation("项目打开，准备加载镜头数据");
    }

    private void OnProjectDataLoaded(object recipient, ProjectDataLoadedMessage message)
    {
        // 清空现有镜头
        foreach (var shot in Shots.ToList())
        {
            DetachShotEventHandlers(shot);
        }
        Shots.Clear();

        // 加载镜头数据
        foreach (var shotState in message.ProjectState.Shots)
        {
            var shot = new ShotItem(shotState.ShotNumber)
            {
                Duration = shotState.Duration,
                StartTime = shotState.StartTime,
                EndTime = shotState.EndTime,
                FirstFramePrompt = shotState.FirstFramePrompt,
                LastFramePrompt = shotState.LastFramePrompt,
                ShotType = shotState.ShotType,
                CoreContent = shotState.CoreContent,
                ActionCommand = shotState.ActionCommand,
                SceneSettings = shotState.SceneSettings,
                SelectedModel = shotState.SelectedModel,
                FirstFrameImagePath = shotState.FirstFrameImagePath,
                LastFrameImagePath = shotState.LastFrameImagePath,
                GeneratedVideoPath = shotState.GeneratedVideoPath,
                MaterialThumbnailPath = shotState.MaterialThumbnailPath,
                MaterialFilePath = shotState.MaterialFilePath,
                // Material info
                MaterialResolution = shotState.MaterialResolution,
                MaterialFileSize = shotState.MaterialFileSize,
                MaterialFormat = shotState.MaterialFormat,
                MaterialColorTone = shotState.MaterialColorTone,
                MaterialBrightness = shotState.MaterialBrightness,
                // Image generation parameters
                ImageSize = shotState.ImageSize,
                NegativePrompt = shotState.NegativePrompt,
                // Image professional parameters
                AspectRatio = shotState.AspectRatio,
                LightingType = shotState.LightingType,
                TimeOfDay = shotState.TimeOfDay,
                Composition = shotState.Composition,
                ColorStyle = shotState.ColorStyle,
                LensType = shotState.LensType,
                // First frame professional parameters
                FirstFrameComposition = shotState.FirstFrameComposition,
                FirstFrameLightingType = shotState.FirstFrameLightingType,
                FirstFrameTimeOfDay = shotState.FirstFrameTimeOfDay,
                FirstFrameColorStyle = shotState.FirstFrameColorStyle,
                FirstFrameLensType = shotState.FirstFrameLensType,
                FirstFrameNegativePrompt = shotState.FirstFrameNegativePrompt,
                FirstFrameImageSize = shotState.FirstFrameImageSize,
                FirstFrameAspectRatio = shotState.FirstFrameAspectRatio,
                FirstFrameSelectedModel = shotState.FirstFrameSelectedModel,
                FirstFrameSeed = shotState.FirstFrameSeed,
                // Last frame professional parameters
                LastFrameComposition = shotState.LastFrameComposition,
                LastFrameLightingType = shotState.LastFrameLightingType,
                LastFrameTimeOfDay = shotState.LastFrameTimeOfDay,
                LastFrameColorStyle = shotState.LastFrameColorStyle,
                LastFrameLensType = shotState.LastFrameLensType,
                LastFrameNegativePrompt = shotState.LastFrameNegativePrompt,
                LastFrameImageSize = shotState.LastFrameImageSize,
                LastFrameAspectRatio = shotState.LastFrameAspectRatio,
                LastFrameSelectedModel = shotState.LastFrameSelectedModel,
                LastFrameSeed = shotState.LastFrameSeed,
                // Video generation parameters
                VideoPrompt = shotState.VideoPrompt,
                SceneDescription = shotState.SceneDescription,
                ActionDescription = shotState.ActionDescription,
                StyleDescription = shotState.StyleDescription,
                VideoNegativePrompt = shotState.VideoNegativePrompt,
                // Video professional parameters
                CameraMovement = shotState.CameraMovement,
                ShootingStyle = shotState.ShootingStyle,
                VideoEffect = shotState.VideoEffect,
                VideoResolution = shotState.VideoResolution,
                VideoRatio = shotState.VideoRatio,
                VideoFrames = shotState.VideoFrames,
                UseFirstFrameReference = shotState.UseFirstFrameReference,
                UseLastFrameReference = shotState.UseLastFrameReference,
                Seed = shotState.Seed,
                CameraFixed = shotState.CameraFixed,
                Watermark = shotState.Watermark
            };

            // 加载资产到对应的集合
            foreach (var assetState in shotState.Assets)
            {
                var assetItem = new ShotAssetItem
                {
                    Type = assetState.Type,
                    FilePath = assetState.FilePath,
                    ThumbnailPath = assetState.ThumbnailPath,
                    VideoThumbnailPath = assetState.VideoThumbnailPath,
                    Prompt = assetState.Prompt,
                    Model = assetState.Model,
                    CreatedAt = assetState.CreatedAt
                };

                switch (assetState.Type)
                {
                    case Domain.Entities.ShotAssetType.FirstFrameImage:
                        shot.FirstFrameAssets.Add(assetItem);
                        break;
                    case Domain.Entities.ShotAssetType.LastFrameImage:
                        shot.LastFrameAssets.Add(assetItem);
                        break;
                    case Domain.Entities.ShotAssetType.GeneratedVideo:
                        shot.VideoAssets.Add(assetItem);
                        break;
                }
            }

            Shots.Add(shot);
        }

        UpdateSummaryCounts();

        // 默认选中第一个镜头
        if (Shots.Count > 0)
        {
            SelectedShot = Shots[0];
        }

        // 在所有镜头加载完成后，再绑定事件处理器，避免加载过程中触发自动保存
        foreach (var shot in Shots)
        {
            AttachShotEventHandlers(shot);
        }

        _logger.LogInformation("项目数据加载完成: {ShotCount} 个镜头", Shots.Count);
    }

    private void OnProjectClosed(object recipient, ProjectClosedMessage message)
    {
        // 清空镜头列表
        foreach (var shot in Shots.ToList())
        {
            DetachShotEventHandlers(shot);
        }

        Shots.Clear();
        SelectedShot = null;

        _logger.LogInformation("项目关闭，清空镜头列表");
    }

    private void OnFramesExtracted(object recipient, FramesExtractedMessage message)
    {
        // 清空现有镜头
        foreach (var shot in Shots.ToList())
        {
            DetachShotEventHandlers(shot);
        }
        Shots.Clear();

        // 添加新的镜头
        foreach (var shot in message.Shots)
        {
            AttachShotEventHandlers(shot);
            Shots.Add(shot);
        }

        // 默认选中第一个镜头
        if (Shots.Count > 0)
        {
            SelectedShot = Shots[0];
        }

        _logger.LogInformation("抽帧完成，加载 {Count} 个镜头到列表", Shots.Count);
    }

    private void OnRestoreSnapshot(object recipient, RestoreSnapshotMessage message)
    {
        // 清空现有镜头（不触发事件）
        foreach (var shot in Shots.ToList())
        {
            DetachShotEventHandlers(shot);
        }
        Shots.Clear();

        // 从快照数据恢复镜头
        foreach (var shotData in message.Shots)
        {
            var shot = new ShotItem(shotData.ShotNumber)
            {
                Duration = shotData.Duration,
                StartTime = shotData.StartTime,
                EndTime = shotData.EndTime,
                FirstFramePrompt = shotData.FirstFramePrompt,
                LastFramePrompt = shotData.LastFramePrompt,
                ShotType = shotData.ShotType,
                CoreContent = shotData.CoreContent,
                ActionCommand = shotData.ActionCommand,
                SceneSettings = shotData.SceneSettings,
                SelectedModel = shotData.SelectedModel
            };

            AttachShotEventHandlers(shot);
            Shots.Add(shot);
        }

        UpdateSummaryCounts();

        // 默认选中第一个镜头
        if (Shots.Count > 0)
        {
            SelectedShot = Shots[0];
        }

        _logger.LogInformation("从快照恢复 {Count} 个镜头", message.Shots.Count);
    }
}


using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
// using Microsoft.Win32; // WPF specific - remove for Avalonia
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
// using System.Windows; // WPF specific - remove for Avalonia
using Storyboard.Models;
using Storyboard.Application.Abstractions;
using Storyboard.Domain.Entities;
using Storyboard.Views;
// using Storyboard.Views.Windows; // Old WPF views - remove for Avalonia
using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Text;
using Storyboard.Application.Services;
using Storyboard.Infrastructure.Media;
using Avalonia.Threading;


namespace Storyboard.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IVideoAnalysisService _videoAnalysisService;
    private readonly IVideoMetadataService _videoMetadataService;
    private readonly IFrameExtractionService _frameExtractionService;
    private readonly IAiShotService _aiShotService;
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IVideoGenerationService _videoGenerationService;
    private readonly IFinalRenderService _finalRenderService;
    private readonly IJobQueueService _jobQueue;
    private readonly IProjectStore _projectStore;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private string? _selectedVideoPath;

    [ObservableProperty]
    private string _projectName = "未命名项目";

    [ObservableProperty]
    private string? _currentProjectId;

    [ObservableProperty]
    private string _newProjectName = string.Empty;

    // 用于 Footer 显示的项目信息
    public ProjectInfo CurrentProject => new ProjectInfo 
    { 
        Name = ProjectName 
    };

    [ObservableProperty]
    private ObservableCollection<ShotItem> _shots = new();

    [ObservableProperty]
    private ShotItem? _selectedShot;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private double _totalDuration;

    [ObservableProperty]
    private int _completedShotsCount;

    [ObservableProperty]
    private int _completedVideoShotsCount;

    [ObservableProperty]
    private int _shotsRenderingCount;

    [ObservableProperty]
    private bool _hasCurrentProject;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    private sealed record ShotSnapshot(
        int ShotNumber,
        double Duration,
        double StartTime,
        double EndTime,
        string FirstFramePrompt,
        string LastFramePrompt,
        string ShotType,
        string CoreContent,
        string ActionCommand,
        string SceneSettings,
        string SelectedModel,
        string? FirstFrameImagePath,
        string? LastFrameImagePath,
        string? GeneratedVideoPath,
        string? MaterialThumbnailPath,
        string? MaterialFilePath,
        IReadOnlyList<ShotAssetSnapshot> Assets,
        bool IsChecked,
        int SelectedTabIndex,
        double TimelineStartPosition,
        double TimelineWidth);

    private sealed record ShotAssetSnapshot(
        Storyboard.Domain.Entities.ShotAssetType Type,
        string FilePath,
        string? ThumbnailPath,
        string? VideoThumbnailPath,
        string? Prompt,
        string? Model,
        DateTimeOffset CreatedAt);

    private sealed record ProjectSnapshot(
        string ProjectName,
        string? CurrentProjectId,
        bool HasCurrentProject,
        string? SelectedVideoPath,
        bool HasVideoFile,
        string VideoFileDuration,
        string VideoFileResolution,
        string VideoFileFps,
        int ExtractModeIndex,
        int FrameCount,
        double TimeInterval,
        double DetectionSensitivity,
        int? SelectedShotIndex,
        IReadOnlyList<ShotSnapshot> Shots);

    private readonly Stack<ProjectSnapshot> _undoStack = new();
    private readonly Stack<ProjectSnapshot> _redoStack = new();
    private ProjectSnapshot? _lastSnapshot;
    private ProjectSnapshot? _pendingUndoSnapshot;
    private Timer? _historyCommitTimer;
    private bool _isHistorySuspended;
    private bool _isRestoringSnapshot;
    private static readonly TimeSpan HistoryCommitDelay = TimeSpan.FromMilliseconds(300);

    [ObservableProperty]
    private bool _isGridView = true;

    [ObservableProperty]
    private bool _isListView;

    [ObservableProperty]
    private bool _isTimelineView;

    [ObservableProperty]
    private ObservableCollection<ProjectInfo> _projects = new();

    [ObservableProperty]
    private bool _hasVideoFile;

    [ObservableProperty]
    private string _videoFileDuration = "--:--";

    [ObservableProperty]
    private string _videoFileResolution = "-- x --";

    [ObservableProperty]
    private string _videoFileFps = "--";

    [ObservableProperty]
    private int _extractModeIndex = 0; // 0=Fixed, 1=Dynamic, 2=Interval, 3=Keyframe

    [ObservableProperty]
    private int _frameCount = 10;

    [ObservableProperty]
    private double _timeInterval = 1000;

    [ObservableProperty]
    private double _detectionSensitivity = 0.5;

    [ObservableProperty]
    private double _timelinePixelsPerSecond = 100;

    [ObservableProperty]
    private double _timelineWidth = 1000;

    [ObservableProperty]
    private ObservableCollection<TimeMarker> _timeMarkers = new();

    private const double TimelineRightPadding = 100;
    private const double TimelineMarkerIntervalSeconds = 5;

    // Dialog visibility properties
    [ObservableProperty]
    private bool _isBatchOperationsDialogOpen;

    [ObservableProperty]
    private bool _isExportDialogOpen;

    [ObservableProperty]
    private bool _isTaskManagerDialogOpen;

    [ObservableProperty]
    private bool _isProviderSettingsDialogOpen;

    [ObservableProperty]
    private bool _isTextToShotDialogOpen;

    [ObservableProperty]
    private string _textToShotPrompt = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private bool _isNewProjectDialogOpen;

    public bool HasProjects => Projects.Count > 0;
    public bool HasShots => Shots.Count > 0;
    public bool CanExportVideo => HasShots && CompletedVideoShotsCount == Shots.Count;
    public bool HasSelectedShots => Shots.Any(s => s.IsChecked);
    public string SelectedShotsCountText => $"{Shots.Count(s => s.IsChecked)} 已选择";
    public bool IsFixedOrDynamicMode => ExtractModeIndex == 0 || ExtractModeIndex == 1;
    public bool IsIntervalMode => ExtractModeIndex == 2;
    public bool IsDynamicMode => ExtractModeIndex == 1;
    public bool IsKeyframeMode => ExtractModeIndex == 3;

    partial void OnExtractModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsFixedOrDynamicMode));
        OnPropertyChanged(nameof(IsIntervalMode));
        OnPropertyChanged(nameof(IsDynamicMode));
        OnPropertyChanged(nameof(IsKeyframeMode));
    }

    private readonly HashSet<ShotItem> _trackedShots = new();



    [RelayCommand]
    private void ShowMaterialImage(ShotItem? shot)
    {
        var previewPath = shot?.MaterialThumbnailPath;
        if (string.IsNullOrWhiteSpace(previewPath) || !System.IO.File.Exists(previewPath))
            previewPath = shot?.MaterialFilePath;

        if (string.IsNullOrWhiteSpace(previewPath) || !System.IO.File.Exists(previewPath))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = previewPath,
                UseShellExecute = true
            });
        }
        catch
        {
            // If preview fails (e.g. invalid image), fall back to opening the file.
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = previewPath,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    [RelayCommand]
    private void OpenMaterialFolder(ShotItem? shot)
    {
        if (shot?.MaterialFilePath == null || !System.IO.File.Exists(shot.MaterialFilePath))
            return;

        try
        {
            // Locate file in Explorer
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{shot.MaterialFilePath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void ViewFirstFrameImage(ShotItem? shot)
    {
        if (string.IsNullOrWhiteSpace(shot?.FirstFrameImagePath) || !System.IO.File.Exists(shot.FirstFrameImagePath))
            return;

        // For now, just open in default image viewer
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = shot.FirstFrameImagePath,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void OpenFirstFrameFolder(ShotItem? shot)
    {
        if (string.IsNullOrWhiteSpace(shot?.FirstFrameImagePath) || !System.IO.File.Exists(shot.FirstFrameImagePath))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{shot.FirstFrameImagePath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void ViewLastFrameImage(ShotItem? shot)
    {
        if (string.IsNullOrWhiteSpace(shot?.LastFrameImagePath) || !System.IO.File.Exists(shot.LastFrameImagePath))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = shot.LastFrameImagePath,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void OpenLastFrameFolder(ShotItem? shot)
    {
        if (string.IsNullOrWhiteSpace(shot?.LastFrameImagePath) || !System.IO.File.Exists(shot.LastFrameImagePath))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{shot.LastFrameImagePath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void PlayVideo(ShotItem? shot)
    {
        if (string.IsNullOrWhiteSpace(shot?.GeneratedVideoPath) || !System.IO.File.Exists(shot.GeneratedVideoPath))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = shot.GeneratedVideoPath,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void OpenVideoFolder(ShotItem? shot)
    {
        if (string.IsNullOrWhiteSpace(shot?.GeneratedVideoPath) || !System.IO.File.Exists(shot.GeneratedVideoPath))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{shot.GeneratedVideoPath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [ObservableProperty]
    private int _generatedImagesCount;

    [ObservableProperty]
    private int _generatedVideosCount;

    [ObservableProperty]
    private int _generatingCount;

    partial void OnProjectNameChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentProject));
        MarkUndoableChange();
    }
    
    public MainViewModel(
        IVideoAnalysisService videoAnalysisService,
        IVideoMetadataService videoMetadataService,
        IFrameExtractionService frameExtractionService,
        IAiShotService aiShotService,
        IImageGenerationService imageGenerationService,
        IVideoGenerationService videoGenerationService,
        IFinalRenderService finalRenderService,
        IJobQueueService jobQueue,
        IProjectStore projectStore,
        ILogger<MainViewModel> logger)
    {
        _videoAnalysisService = videoAnalysisService;
        _videoMetadataService = videoMetadataService;
        _frameExtractionService = frameExtractionService;
        _aiShotService = aiShotService;
        _imageGenerationService = imageGenerationService;
        _videoGenerationService = videoGenerationService;
        _finalRenderService = finalRenderService;
        _jobQueue = jobQueue;
        _projectStore = projectStore;
        _logger = logger;

        Shots.CollectionChanged += Shots_CollectionChanged;
        Projects.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasProjects));
        UpdateSummaryCounts();

        RecalculateTimelineLayout();

        // Start with no project open (Project History screen)
        HasCurrentProject = false;

        _ = ReloadProjectsAsync();

        InitializeHistory();
    }

    partial void OnSelectedShotChanged(ShotItem? value)
    {
        RunWithoutHistory(() =>
        {
            foreach (var shot in Shots)
                shot.IsSelected = ReferenceEquals(shot, value);
        });
    }

    partial void OnTimelinePixelsPerSecondChanged(double value)
    {
        RecalculateTimelineLayout();
    }

    private void RecalculateTimelineLayout()
    {
        RunWithoutHistory(() =>
        {
            var pixelsPerSecond = Math.Max(1, TimelinePixelsPerSecond);
            var totalDuration = Math.Max(0, Shots.Sum(s => Math.Max(0, s.Duration)));

            TotalDuration = totalDuration;
            TimelineWidth = totalDuration * pixelsPerSecond + TimelineRightPadding;

            var currentTime = 0.0;
            foreach (var shot in Shots)
            {
                var duration = Math.Max(0, shot.Duration);
                shot.TimelineStartPosition = currentTime * pixelsPerSecond;
                shot.TimelineWidth = duration * pixelsPerSecond;
                currentTime += duration;
            }

            TimeMarkers.Clear();
            for (var t = 0.0; t <= Math.Ceiling(totalDuration); t += TimelineMarkerIntervalSeconds)
            {
                TimeMarkers.Add(new TimeMarker
                {
                    Position = t * pixelsPerSecond,
                    Label = $"{t:0}s"
                });
            }

            foreach (var shot in Shots)
                shot.IsSelected = ReferenceEquals(shot, SelectedShot);
        });
    }

    private void InitializeHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _pendingUndoSnapshot = null;
        _lastSnapshot = TakeSnapshot();
        UpdateUndoRedoState();
    }

    private void UpdateUndoRedoState()
    {
        CanUndo = HasCurrentProject && _undoStack.Count > 0;
        CanRedo = HasCurrentProject && _redoStack.Count > 0;
    }

    private void RunWithoutHistory(Action action)
    {
        var prev = _isHistorySuspended;
        _isHistorySuspended = true;
        try
        {
            action();
        }
        finally
        {
            _isHistorySuspended = prev;
        }
    }

    private ProjectSnapshot TakeSnapshot()
    {
        var selectedIndex = SelectedShot == null ? (int?)null : Shots.IndexOf(SelectedShot);

        var shotSnapshots = Shots.Select(s => new ShotSnapshot(
            s.ShotNumber,
            s.Duration,
            s.StartTime,
            s.EndTime,
            s.FirstFramePrompt,
            s.LastFramePrompt,
            s.ShotType,
            s.CoreContent,
            s.ActionCommand,
            s.SceneSettings,
            s.SelectedModel,
            s.FirstFrameImagePath,
            s.LastFrameImagePath,
            s.GeneratedVideoPath,
            s.MaterialThumbnailPath,
            s.MaterialFilePath,
            BuildAssetSnapshots(s),
            s.IsChecked,
            s.SelectedTabIndex,
            s.TimelineStartPosition,
            s.TimelineWidth)).ToList();

        return new ProjectSnapshot(
            ProjectName,
            CurrentProjectId,
            HasCurrentProject,
            SelectedVideoPath,
            HasVideoFile,
            VideoFileDuration,
            VideoFileResolution,
            VideoFileFps,
            ExtractModeIndex,
            FrameCount,
            TimeInterval,
            DetectionSensitivity,
            selectedIndex,
            shotSnapshots);
    }

    private static IReadOnlyList<ShotAssetSnapshot> BuildAssetSnapshots(ShotItem shot)
    {
        var list = new List<ShotAssetSnapshot>();

        AddAssetSnapshots(list, shot.FirstFrameAssets);
        AddAssetSnapshots(list, shot.LastFrameAssets);
        AddAssetSnapshots(list, shot.VideoAssets);

        return list;
    }

    private static void AddAssetSnapshots(List<ShotAssetSnapshot> list, IEnumerable<ShotAssetItem> assets)
    {
        foreach (var asset in assets)
        {
            if (string.IsNullOrWhiteSpace(asset.FilePath))
                continue;

            list.Add(new ShotAssetSnapshot(
                asset.Type,
                asset.FilePath,
                asset.ThumbnailPath,
                asset.VideoThumbnailPath,
                asset.Prompt,
                asset.Model,
                asset.CreatedAt));
        }
    }

    private void RestoreSnapshot(ProjectSnapshot snapshot)
    {
        _isRestoringSnapshot = true;
        RunWithoutHistory(() =>
        {
            ProjectName = snapshot.ProjectName;
            CurrentProjectId = snapshot.CurrentProjectId;
            HasCurrentProject = snapshot.HasCurrentProject;
            SelectedVideoPath = snapshot.SelectedVideoPath;
            HasVideoFile = snapshot.HasVideoFile;
            VideoFileDuration = snapshot.VideoFileDuration;
            VideoFileResolution = snapshot.VideoFileResolution;
            VideoFileFps = snapshot.VideoFileFps;
            ExtractModeIndex = snapshot.ExtractModeIndex;
            FrameCount = snapshot.FrameCount;
            TimeInterval = snapshot.TimeInterval;
            DetectionSensitivity = snapshot.DetectionSensitivity;

            Shots.Clear();
            foreach (var s in snapshot.Shots)
            {
                var shot = new ShotItem(s.ShotNumber)
                {
                    Duration = s.Duration,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    FirstFramePrompt = s.FirstFramePrompt,
                    LastFramePrompt = s.LastFramePrompt,
                    ShotType = s.ShotType,
                    CoreContent = s.CoreContent,
                    ActionCommand = s.ActionCommand,
                    SceneSettings = s.SceneSettings,
                    SelectedModel = s.SelectedModel,
                    FirstFrameImagePath = s.FirstFrameImagePath,
                    LastFrameImagePath = s.LastFrameImagePath,
                    GeneratedVideoPath = s.GeneratedVideoPath,
                    MaterialThumbnailPath = s.MaterialThumbnailPath,
                    MaterialFilePath = s.MaterialFilePath,
                    IsChecked = s.IsChecked,
                    SelectedTabIndex = s.SelectedTabIndex,
                    TimelineStartPosition = s.TimelineStartPosition,
                    TimelineWidth = s.TimelineWidth
                };
                LoadAssetsIntoShot(shot, s.Assets);
                AttachShotEventHandlers(shot);
                Shots.Add(shot);
            }

            if (snapshot.SelectedShotIndex is int idx && idx >= 0 && idx < Shots.Count)
                SelectedShot = Shots[idx];
            else
                SelectedShot = null;

            UpdateSummaryCounts();
            OnPropertyChanged(nameof(HasShots));
            OnPropertyChanged(nameof(HasSelectedShots));
            OnPropertyChanged(nameof(SelectedShotsCountText));

            foreach (var shot in Shots)
            {
                foreach (var asset in shot.VideoAssets)
                    _ = EnsureVideoAssetThumbnailAsync(asset);
            }
        });

        _pendingUndoSnapshot = null;
        _lastSnapshot = TakeSnapshot();
        UpdateUndoRedoState();
        _isRestoringSnapshot = false;
    }

    private static void LoadAssetsIntoShot(ShotItem shot, IReadOnlyList<ShotAssetSnapshot> assets)
    {
        ClearShotAssets(shot);
        foreach (var asset in assets)
        {
            if (string.IsNullOrWhiteSpace(asset.FilePath))
                continue;

            var item = new ShotAssetItem
            {
                Type = asset.Type,
                FilePath = asset.FilePath,
                ThumbnailPath = asset.ThumbnailPath,
                VideoThumbnailPath = asset.VideoThumbnailPath,
                Prompt = asset.Prompt,
                Model = asset.Model,
                CreatedAt = asset.CreatedAt
            };

            AddAssetItemToShot(shot, item);
        }
    }

    private static void LoadAssetsIntoShot(ShotItem shot, IReadOnlyList<ShotAssetState> assets)
    {
        ClearShotAssets(shot);
        foreach (var asset in assets)
        {
            if (string.IsNullOrWhiteSpace(asset.FilePath))
                continue;

            var item = new ShotAssetItem
            {
                Type = asset.Type,
                FilePath = asset.FilePath,
                ThumbnailPath = asset.ThumbnailPath,
                VideoThumbnailPath = asset.VideoThumbnailPath,
                Prompt = asset.Prompt,
                Model = asset.Model,
                CreatedAt = asset.CreatedAt
            };

            AddAssetItemToShot(shot, item);
        }
    }

    private static void ClearShotAssets(ShotItem shot)
    {
        shot.FirstFrameAssets.Clear();
        shot.LastFrameAssets.Clear();
        shot.VideoAssets.Clear();
    }

    private static void AddAssetItemToShot(ShotItem shot, ShotAssetItem item)
    {
        switch (item.Type)
        {
            case Storyboard.Domain.Entities.ShotAssetType.FirstFrameImage:
                item.IsSelected = string.Equals(item.FilePath, shot.FirstFrameImagePath, StringComparison.OrdinalIgnoreCase);
                shot.FirstFrameAssets.Add(item);
                break;
            case Storyboard.Domain.Entities.ShotAssetType.LastFrameImage:
                item.IsSelected = string.Equals(item.FilePath, shot.LastFrameImagePath, StringComparison.OrdinalIgnoreCase);
                shot.LastFrameAssets.Add(item);
                break;
            case Storyboard.Domain.Entities.ShotAssetType.GeneratedVideo:
                item.IsSelected = string.Equals(item.FilePath, shot.GeneratedVideoPath, StringComparison.OrdinalIgnoreCase);
                shot.VideoAssets.Add(item);
                break;
        }
    }

    private void MarkUndoableChange()
    {
        if (_isHistorySuspended || _isRestoringSnapshot)
            return;

        if (!HasCurrentProject)
            return;

        // Lazily initialize if needed
        _lastSnapshot ??= TakeSnapshot();

        _pendingUndoSnapshot ??= _lastSnapshot;

        // Debounce: commit one undo record after the user stops changing things.
        _historyCommitTimer?.Dispose();
        _historyCommitTimer = new Timer(_ =>
        {
            try
            {
                OnUi(CommitPendingUndoSnapshot);
            }
            catch
            {
                // ignore timer failures
            }
        }, null, HistoryCommitDelay, Timeout.InfiniteTimeSpan);
    }

    private void CommitPendingUndoSnapshot()
    {
        if (_isHistorySuspended || _isRestoringSnapshot)
            return;

        if (!HasCurrentProject)
            return;

        if (_pendingUndoSnapshot == null)
            return;

        var current = TakeSnapshot();

        _undoStack.Push(_pendingUndoSnapshot);
        _redoStack.Clear();

        _pendingUndoSnapshot = null;
        _lastSnapshot = current;
        UpdateUndoRedoState();

        // Persist project (debounced via history commit timer).
        UpsertFromCurrentProject();
    }

    private static ProjectInfo ToProjectInfo(ProjectSummary dto)
    {
        var completionRate = dto.TotalShots > 0 ? (int)Math.Round((double)dto.CompletedShots / dto.TotalShots * 100) : 0;
        return new ProjectInfo
        {
            Id = dto.Id,
            Name = dto.Name,
            UpdatedAt = dto.UpdatedAt,
            UpdatedTimeAgo = FormatTimeAgo(dto.UpdatedAt),
            CompletionText = dto.TotalShots > 0 ? $"{dto.CompletedShots} / {dto.TotalShots}" : "0%",
            CompletionWidth = dto.TotalShots > 0 ? Math.Clamp(2.2 * completionRate, 0, 220) : 0,
            ShotCountText = $"{dto.TotalShots} 分镜",
            ImageCountText = $"{dto.HasImages} 图片"
        };
    }

    private async Task ReloadProjectsAsync()
    {
        try
        {
            var list = await _projectStore.GetRecentAsync().ConfigureAwait(false);
            OnUi(() =>
            {
                Projects.Clear();
                foreach (var dto in list)
                    Projects.Add(ToProjectInfo(dto));
            });
        }
        catch
        {
            // ignore load failures
        }
    }

    private static string FormatTimeAgo(DateTimeOffset timestamp)
    {
        var now = DateTimeOffset.Now;
        var diff = now - timestamp;

        if (diff.TotalHours < 1)
            return "刚刚";
        if (diff.TotalHours < 24)
            return $"{(int)Math.Floor(diff.TotalHours)} 小时前";
        if (diff.TotalDays < 7)
            return $"{(int)Math.Floor(diff.TotalDays)} 天前";

        return timestamp.LocalDateTime.ToString("yyyy/M/d");
    }

    private void UpsertFromCurrentProject()
    {
        if (!HasCurrentProject)
            return;

        if (string.IsNullOrWhiteSpace(CurrentProjectId))
            CurrentProjectId = Guid.NewGuid().ToString("N");

        var totalShots = Shots.Count;
        var completedShots = Shots.Count(s => !string.IsNullOrWhiteSpace(s.GeneratedVideoPath) && File.Exists(s.GeneratedVideoPath));
        var hasImages = Shots.Sum(s => s.FirstFrameAssets.Count + s.LastFrameAssets.Count);
        var updatedAt = DateTimeOffset.Now;
        var dto = new ProjectSummary(
            CurrentProjectId!,
            ProjectName,
            updatedAt,
            totalShots,
            completedShots,
            hasImages);

        var existing = Projects.FirstOrDefault(p => p.Id == dto.Id);
        if (existing == null)
        {
            Projects.Insert(0, ToProjectInfo(dto));
        }
        else
        {
            existing.Name = dto.Name;
            existing.UpdatedAt = dto.UpdatedAt;
            existing.UpdatedTimeAgo = FormatTimeAgo(dto.UpdatedAt);
            existing.CompletionText = dto.TotalShots > 0 ? $"{dto.CompletedShots} / {dto.TotalShots}" : "0%";
            var completionRate = dto.TotalShots > 0 ? (int)Math.Round((double)dto.CompletedShots / dto.TotalShots * 100) : 0;
            existing.CompletionWidth = dto.TotalShots > 0 ? Math.Clamp(2.2 * completionRate, 0, 220) : 0;
            existing.ShotCountText = $"{dto.TotalShots} 分镜";
            existing.ImageCountText = $"{dto.HasImages} 图片";

            // move to top
            var idx = Projects.IndexOf(existing);
            if (idx > 0)
                Projects.Move(idx, 0);
        }

        PersistCurrentProjectFireAndForget();
    }

        private ProjectState BuildCurrentProjectState()
        {
            var id = CurrentProjectId ?? Guid.NewGuid().ToString("N");
            var shots = Shots
                .OrderBy(s => s.ShotNumber)
                .Select(s => new ShotState(
                    s.ShotNumber,
                    s.Duration,
                    s.StartTime,
                    s.EndTime,
                    s.FirstFramePrompt,
                    s.LastFramePrompt,
                    s.ShotType,
                    s.CoreContent,
                    s.ActionCommand,
                    s.SceneSettings,
                    s.SelectedModel,
                    s.FirstFrameImagePath,
                    s.LastFrameImagePath,
                    s.GeneratedVideoPath,
                    s.MaterialThumbnailPath,
                    s.MaterialFilePath,
                    BuildAssetStates(s),
                    // Material info
                    s.MaterialResolution,
                    s.MaterialFileSize,
                    s.MaterialFormat,
                    s.MaterialColorTone,
                    s.MaterialBrightness,
                    // Image generation parameters
                    s.ImageSize,
                    s.NegativePrompt,
                    // Image professional parameters
                    s.AspectRatio,
                    s.LightingType,
                    s.TimeOfDay,
                    s.Composition,
                    s.ColorStyle,
                    s.LensType,
                    // Video generation parameters
                    s.VideoPrompt,
                    s.SceneDescription,
                    s.ActionDescription,
                    s.StyleDescription,
                    s.VideoNegativePrompt,
                    // Video professional parameters
                    s.CameraMovement,
                    s.ShootingStyle,
                    s.VideoEffect,
                    s.VideoResolution,
                    s.VideoRatio,
                    s.VideoFrames,
                    s.UseFirstFrameReference,
                    s.UseLastFrameReference,
                    s.Seed,
                    s.CameraFixed,
                    s.Watermark))
                .ToList();

            return new ProjectState(
                id,
                ProjectName,
                SelectedVideoPath,
                HasVideoFile,
                VideoFileDuration,
                VideoFileResolution,
                VideoFileFps,
                ExtractModeIndex,
                FrameCount,
                TimeInterval,
                DetectionSensitivity,
                shots);
        }

    private static IReadOnlyList<ShotAssetState> BuildAssetStates(ShotItem shot)
    {
        var list = new List<ShotAssetState>();
        AddAssetStates(list, shot.FirstFrameAssets);
        AddAssetStates(list, shot.LastFrameAssets);
        AddAssetStates(list, shot.VideoAssets);
        return list;
    }

    private static void AddAssetStates(List<ShotAssetState> list, IEnumerable<ShotAssetItem> assets)
    {
        foreach (var asset in assets)
        {
            if (string.IsNullOrWhiteSpace(asset.FilePath))
                continue;

            list.Add(new ShotAssetState(
                asset.Type,
                asset.FilePath,
                asset.ThumbnailPath,
                asset.VideoThumbnailPath,
                asset.Prompt,
                asset.Model,
                asset.CreatedAt));
        }
    }

    private void PersistCurrentProjectFireAndForget()
    {
        if (!HasCurrentProject)
            return;

        var state = BuildCurrentProjectState();
        CurrentProjectId = state.Id;

        _ = Task.Run(async () =>
        {
            try
            {
                await _projectStore.SaveAsync(state).ConfigureAwait(false);
            }
            catch
            {
                // ignore save failures
            }
        });
    }

    private string GetProjectOutputDirectory(string category)
    {
        var projectId = string.IsNullOrWhiteSpace(CurrentProjectId) ? "temp" : CurrentProjectId;
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "projects", projectId, category);
    }

    public ObservableCollection<GenerationJob> JobHistory => _jobQueue.Jobs;

    private void AttachShotEventHandlers(ShotItem shot)
    {
        shot.DuplicateRequested += OnShotDuplicateRequested;
        shot.DeleteRequested += OnShotDeleteRequested;
        shot.AiParseRequested += OnShotAiParseRequested;
        shot.GenerateFirstFrameRequested += OnShotGenerateFirstFrameRequested;
        shot.GenerateLastFrameRequested += OnShotGenerateLastFrameRequested;
        shot.GenerateVideoRequested += OnShotGenerateVideoRequested;
    }

    private void DetachShotEventHandlers(ShotItem shot)
    {
        shot.DuplicateRequested -= OnShotDuplicateRequested;
        shot.DeleteRequested -= OnShotDeleteRequested;
        shot.AiParseRequested -= OnShotAiParseRequested;
        shot.GenerateFirstFrameRequested -= OnShotGenerateFirstFrameRequested;
        shot.GenerateLastFrameRequested -= OnShotGenerateLastFrameRequested;
        shot.GenerateVideoRequested -= OnShotGenerateVideoRequested;
    }

    private void OnShotDuplicateRequested(object? sender, EventArgs e)
    {
        if (sender is ShotItem shot)
        {
            var index = Shots.IndexOf(shot);
            if (index >= 0)
            {
                var duplicate = new ShotItem(shot.ShotNumber + 1)
                {
                    Duration = shot.Duration,
                    StartTime = shot.StartTime,
                    EndTime = shot.EndTime,
                    FirstFramePrompt = shot.FirstFramePrompt,
                    LastFramePrompt = shot.LastFramePrompt,
                    ShotType = shot.ShotType,
                    CoreContent = shot.CoreContent,
                    ActionCommand = shot.ActionCommand,
                    SceneSettings = shot.SceneSettings,
                    SelectedModel = shot.SelectedModel
                };
                Shots.Insert(index + 1, duplicate);
                RenumberShots();
            }
        }
    }

    private void OnShotDeleteRequested(object? sender, EventArgs e)
    {
        if (sender is ShotItem shot)
        {
            Shots.Remove(shot);
            RenumberShots();
        }
    }

    private void OnShotAiParseRequested(object? sender, EventArgs e)
    {
        if (sender is ShotItem shot)
            EnqueueAiParseJob(shot);
    }

    private void OnShotGenerateFirstFrameRequested(object? sender, EventArgs e)
    {
        if (sender is ShotItem shot)
            EnqueueFirstFrameJob(shot);
    }

    private void OnShotGenerateLastFrameRequested(object? sender, EventArgs e)
    {
        if (sender is ShotItem shot)
            EnqueueLastFrameJob(shot);
    }

    private void OnShotGenerateVideoRequested(object? sender, EventArgs e)
    {
        if (sender is ShotItem shot)
            EnqueueVideoJob(shot);
    }

    // UI Toggle Commands (for toolbar buttons)
    [RelayCommand]
    private void ShowCreateProjectDialog()
    {
        IsNewProjectDialogOpen = true;
    }

    [RelayCommand]
    private void CreateNewProject(string? projectName = null)
    {
        RunWithoutHistory(() =>
        {
            // Create new project
            if (string.IsNullOrWhiteSpace(projectName))
            {
                projectName = $"新项目 {DateTime.Now:MMdd-HHmm}";
            }

            CurrentProjectId = Guid.NewGuid().ToString("N");
            ProjectName = projectName;
            HasCurrentProject = true;
            Shots.Clear();
            SelectedShot = null;
            IsNewProjectDialogOpen = false;

            // update history after creating
            UpsertFromCurrentProject();
        });

        InitializeHistory();
    }

    [RelayCommand]
    private void OpenProject(ProjectInfo? project)
    {
        if (project == null)
            return;

        RunWithoutHistory(() =>
        {
            CurrentProjectId = project.Id;
            ProjectName = project.Name;
            HasCurrentProject = true;
            Shots.Clear();
            SelectedShot = null;
        });

        _ = Task.Run(async () =>
        {
            try
            {
                var state = await _projectStore.LoadAsync(project.Id).ConfigureAwait(false);
                if (state == null)
                    return;

                OnUi(() =>
                {
                    RunWithoutHistory(() =>
                    {
                        CurrentProjectId = state.Id;
                        ProjectName = state.Name;
                        HasCurrentProject = true;
                        SelectedVideoPath = state.SelectedVideoPath;
                        HasVideoFile = state.HasVideoFile;
                        VideoFileDuration = state.VideoFileDuration;
                        VideoFileResolution = state.VideoFileResolution;
                        VideoFileFps = state.VideoFileFps;
                        ExtractModeIndex = state.ExtractModeIndex;
                        FrameCount = state.FrameCount;
                        TimeInterval = state.TimeInterval;
                        DetectionSensitivity = state.DetectionSensitivity;

                        Shots.Clear();
                        foreach (var s in state.Shots.OrderBy(s => s.ShotNumber))
                        {
                            var shot = new ShotItem(s.ShotNumber)
                            {
                                Duration = s.Duration,
                                StartTime = s.StartTime,
                                EndTime = s.EndTime,
                                FirstFramePrompt = s.FirstFramePrompt,
                                LastFramePrompt = s.LastFramePrompt,
                                ShotType = s.ShotType,
                                CoreContent = s.CoreContent,
                                ActionCommand = s.ActionCommand,
                                SceneSettings = s.SceneSettings,
                                SelectedModel = s.SelectedModel,
                                FirstFrameImagePath = s.FirstFrameImagePath,
                                LastFrameImagePath = s.LastFrameImagePath,
                                GeneratedVideoPath = s.GeneratedVideoPath,
                                MaterialThumbnailPath = s.MaterialThumbnailPath,
                                MaterialFilePath = s.MaterialFilePath,
                                // Material info
                                MaterialResolution = s.MaterialResolution,
                                MaterialFileSize = s.MaterialFileSize,
                                MaterialFormat = s.MaterialFormat,
                                MaterialColorTone = s.MaterialColorTone,
                                MaterialBrightness = s.MaterialBrightness,
                                // Image generation parameters
                                ImageSize = s.ImageSize,
                                NegativePrompt = s.NegativePrompt,
                                // Image professional parameters
                                AspectRatio = s.AspectRatio,
                                LightingType = s.LightingType,
                                TimeOfDay = s.TimeOfDay,
                                Composition = s.Composition,
                                ColorStyle = s.ColorStyle,
                                LensType = s.LensType,
                                // Video generation parameters
                                VideoPrompt = s.VideoPrompt,
                                SceneDescription = s.SceneDescription,
                                ActionDescription = s.ActionDescription,
                                StyleDescription = s.StyleDescription,
                                VideoNegativePrompt = s.VideoNegativePrompt,
                                // Video professional parameters
                                CameraMovement = s.CameraMovement,
                                ShootingStyle = s.ShootingStyle,
                                VideoEffect = s.VideoEffect,
                                VideoResolution = s.VideoResolution,
                                VideoRatio = s.VideoRatio,
                                VideoFrames = s.VideoFrames,
                                UseFirstFrameReference = s.UseFirstFrameReference,
                                UseLastFrameReference = s.UseLastFrameReference,
                                Seed = s.Seed,
                                CameraFixed = s.CameraFixed,
                                Watermark = s.Watermark
                            };
                            LoadAssetsIntoShot(shot, s.Assets);
                            AttachShotEventHandlers(shot);
                            Shots.Add(shot);
                        }
                        RenumberShots();

                        foreach (var shot in Shots)
                        {
                            foreach (var asset in shot.VideoAssets)
                                _ = EnsureVideoAssetThumbnailAsync(asset);
                        }
                    });

                    InitializeHistory();
                });
            }
            catch
            {
                // ignore load failures
            }
        });

        InitializeHistory();
    }

    [RelayCommand]
    private void DeleteProject(ProjectInfo? project)
    {
        if (project == null)
            return;

        Projects.Remove(project);
        _ = Task.Run(async () =>
        {
            try
            {
                await _projectStore.DeleteAsync(project.Id).ConfigureAwait(false);
            }
            catch
            {
                // ignore delete failures
            }
        });
    }

    [RelayCommand]
    private void ToggleViewMode()
    {
        if (IsTimelineView)
        {
            SetGridView();
        }
        else
        {
            SetTimelineView();
        }
    }

    [RelayCommand]
    private void ToggleTaskManager()
    {
        IsTaskManagerDialogOpen = !IsTaskManagerDialogOpen;
    }

    [RelayCommand]
    private void ToggleProviderSettings()
    {
        IsProviderSettingsDialogOpen = !IsProviderSettingsDialogOpen;
    }

    [RelayCommand]
    private void CloseProject()
    {
        RunWithoutHistory(() =>
        {
            // persist the latest stats before closing
            UpsertFromCurrentProject();

            HasCurrentProject = false;
            ProjectName = "未命名项目";
            Shots.Clear();
            SelectedShot = null;

            SelectedVideoPath = null;
            HasVideoFile = false;
            VideoFileDuration = "--:--";
            VideoFileResolution = "-- x --";
            VideoFileFps = "--";

            CurrentProjectId = null;
        });

        InitializeHistory();
    }

    [RelayCommand]
    private void Undo()
    {
        CommitPendingUndoSnapshot();
        if (_undoStack.Count == 0)
            return;

        var current = TakeSnapshot();
        var previous = _undoStack.Pop();
        _redoStack.Push(current);
        RestoreSnapshot(previous);
    }

    [RelayCommand]
    private void Redo()
    {
        CommitPendingUndoSnapshot();
        if (_redoStack.Count == 0)
            return;

        var current = TakeSnapshot();
        var next = _redoStack.Pop();
        _undoStack.Push(current);
        RestoreSnapshot(next);
    }

    [RelayCommand]
    private void ShowBatchOperations()
    {
        IsBatchOperationsDialogOpen = true;
    }

    [RelayCommand]
    private void ShowExportDialog()
    {
        IsExportDialogOpen = true;
    }

    [RelayCommand]
    private void ShowTaskManager()
    {
        IsTaskManagerDialogOpen = true;
    }

    [RelayCommand]
    private void ShowProviderSettings()
    {
        IsProviderSettingsDialogOpen = true;
    }

    [RelayCommand]
    private void SetGridView()
    {
        IsGridView = true;
        IsListView = false;
        IsTimelineView = false;
        OnPropertyChanged(nameof(IsGridView));
        OnPropertyChanged(nameof(IsListView));
        OnPropertyChanged(nameof(IsTimelineView));
    }

    [RelayCommand]
    private void SetListView()
    {
        IsGridView = false;
        IsListView = true;
        IsTimelineView = false;
        OnPropertyChanged(nameof(IsGridView));
        OnPropertyChanged(nameof(IsListView));
        OnPropertyChanged(nameof(IsTimelineView));
    }

    [RelayCommand]
    private void SetTimelineView()
    {
        IsGridView = false;
        IsListView = false;
        IsTimelineView = true;
        OnPropertyChanged(nameof(IsGridView));
        OnPropertyChanged(nameof(IsListView));
        OnPropertyChanged(nameof(IsTimelineView));
    }

    [RelayCommand]
    private void ShowTextToShotDialog()
    {
        IsTextToShotDialogOpen = true;
    }

    public async Task GenerateShotsFromTextPromptAsync()
    {
        if (string.IsNullOrWhiteSpace(TextToShotPrompt))
            return;

        try
        {
            StatusMessage = "正在生成分镜...";
            var items = await _aiShotService.GenerateShotsFromTextAsync(TextToShotPrompt).ConfigureAwait(false);

            if (items.Count == 0)
            {
                StatusMessage = "未生成任何分镜，请调整描述后重试。";
                return;
            }

            var baseIndex = Shots.Count;
            var startTime = Shots.Count == 0 ? 0 : Shots.Max(s => s.EndTime);

            OnUi(() =>
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var desc = items[i];
                    var duration = desc.DurationSeconds.GetValueOrDefault(3.5);
                    if (duration <= 0)
                        duration = 3.5;

                    var shot = new ShotItem(baseIndex + i + 1)
                    {
                        Duration = duration,
                        StartTime = startTime,
                        EndTime = startTime + duration,
                        ShotType = desc.ShotType,
                        CoreContent = desc.CoreContent,
                        ActionCommand = desc.ActionCommand,
                        SceneSettings = desc.SceneSettings,
                        FirstFramePrompt = desc.FirstFramePrompt,
                        LastFramePrompt = desc.LastFramePrompt,
                        SelectedModel = string.Empty
                    };

                    startTime += duration;
                    AttachShotEventHandlers(shot);
                    Shots.Add(shot);
                }

                RenumberShots();
                TextToShotPrompt = string.Empty;
                StatusMessage = $"已生成 {items.Count} 个分镜。";
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generate shots failed. Prompt: {Prompt}", TextToShotPrompt);
            StatusMessage = $"生成分镜失败: {ex.Message}";
        }
    }

    public void MoveShot(ShotItem source, ShotItem target)
        => MoveShot(source, target, insertAfter: false);

    public void MoveShot(ShotItem source, ShotItem target, bool insertAfter)
    {
        var fromIndex = Shots.IndexOf(source);
        var targetIndex = Shots.IndexOf(target);
        if (fromIndex < 0 || targetIndex < 0)
            return;

        if (ReferenceEquals(source, target))
            return;

        var insertIndex = insertAfter ? targetIndex + 1 : targetIndex;

        // Remove then insert to support before/after semantics reliably.
        Shots.RemoveAt(fromIndex);
        if (fromIndex < insertIndex)
            insertIndex--;

        insertIndex = Math.Clamp(insertIndex, 0, Shots.Count);
        Shots.Insert(insertIndex, source);
        RenumberShots();
    }

    [RelayCommand]
    private async Task AIAnalyzeAll()
    {
        if (Shots.Count == 0)
            return;

        var needMode = Shots.Any(NeedsAiWriteMode);
        AiWriteMode? mode = null;
        if (needMode)
        {
            mode = await RequestAiWriteModeAsync();
            if (mode == null)
                return;
        }

        var skipped = 0;
        foreach (var shot in Shots)
        {
            if (string.IsNullOrWhiteSpace(shot.MaterialFilePath) || !File.Exists(shot.MaterialFilePath))
            {
                skipped++;
                continue;
            }
            EnqueueAiParseJob(shot, mode);
        }

        if (skipped > 0)
            StatusMessage = $"已跳过 {skipped} 个缺少素材图片的分镜。";
    }

    [RelayCommand]
    private async Task ImportVideo()
    {
        var videoPath = await PickVideoPathAsync();
        if (string.IsNullOrWhiteSpace(videoPath))
            return;

        try
        {
            StatusMessage = "正在解析视频...";
            var metadata = await _videoMetadataService.GetMetadataAsync(videoPath);

            SelectedVideoPath = metadata.VideoPath;
            HasVideoFile = true;
            VideoFileDuration = FormatDuration(metadata.DurationSeconds);
            VideoFileResolution = $"{metadata.Width}x{metadata.Height}";
            VideoFileFps = metadata.Fps <= 0 ? "--" : metadata.Fps.ToString("0.##");

            StatusMessage = "视频导入完成";
            UpsertFromCurrentProject();
        }
        catch (Exception ex)
        {
            StatusMessage = $"视频导入失败: {ex.Message}";
        }
    }

    private async Task<string?> PickVideoPathAsync()
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var window = lifetime?.MainWindow;
        var storageProvider = window?.StorageProvider;
        if (storageProvider == null)
            return null;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择视频文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("视频文件")
                {
                    Patterns = new[] { "*.mp4", "*.mov", "*.mkv", "*.avi", "*.webm" }
                }
            }
        });

        return files?.FirstOrDefault()?.Path.LocalPath;
    }

    private static string FormatDuration(double seconds)
    {
        if (seconds <= 0)
            return "--:--";

        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"hh\:mm\:ss")
            : ts.ToString(@"mm\:ss");
    }

    private void BuildShotsFromFrames(IReadOnlyList<ExtractedFrame> frames, double totalDuration)
    {
        if (frames.Count == 0)
            return;

        var ordered = frames.OrderBy(f => f.TimestampSeconds).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var current = ordered[i];
            var prevTime = i == 0 ? 0 : ordered[i - 1].TimestampSeconds;
            var nextTime = i == ordered.Count - 1 ? totalDuration : ordered[i + 1].TimestampSeconds;
            var start = i == 0 ? 0 : (prevTime + current.TimestampSeconds) / 2.0;
            var end = i == ordered.Count - 1 ? totalDuration : (current.TimestampSeconds + nextTime) / 2.0;
            if (end < start)
                end = start + 0.5;

            var duration = Math.Max(0.5, end - start);

            // Extract material info from the frame file
            var materialInfo = ExtractMaterialInfo(current.FilePath);

            var shot = new ShotItem(i + 1)
            {
                Duration = duration,
                StartTime = start,
                EndTime = end,
                ShotType = duration > 4 ? "远景" : "中景",
                CoreContent = "抽帧生成镜头",
                ActionCommand = "待补充",
                SceneSettings = "待补充",
                FirstFramePrompt = string.Empty,
                LastFramePrompt = string.Empty,
                SelectedModel = string.Empty,
                MaterialFilePath = current.FilePath,
                MaterialThumbnailPath = current.FilePath,
                MaterialResolution = materialInfo.Resolution,
                MaterialFileSize = materialInfo.FileSize,
                MaterialFormat = materialInfo.Format,
                MaterialColorTone = materialInfo.ColorTone,
                MaterialBrightness = materialInfo.Brightness
            };

            AttachShotEventHandlers(shot);
            Shots.Add(shot);
        }
    }

    private (string Resolution, string FileSize, string Format, string ColorTone, string Brightness) ExtractMaterialInfo(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return ("未知", "未知", "未知", "未知", "未知");

            var fileInfo = new FileInfo(filePath);
            var format = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
            var fileSize = FormatFileSize(fileInfo.Length);

            // Use SkiaSharp to extract image info
            using var stream = File.OpenRead(filePath);
            using var bitmap = SkiaSharp.SKBitmap.Decode(stream);

            if (bitmap == null)
                return ($"{0}x{0}", fileSize, format, "未知", "未知");

            var resolution = $"{bitmap.Width}x{bitmap.Height}";
            var (colorTone, brightness) = AnalyzeImageColor(bitmap);

            return (resolution, fileSize, format, colorTone, brightness);
        }
        catch
        {
            return ("未知", "未知", "未知", "未知", "未知");
        }
    }

    private (string ColorTone, string Brightness) AnalyzeImageColor(SkiaSharp.SKBitmap bitmap)
    {
        long totalR = 0, totalG = 0, totalB = 0;
        int sampleCount = 0;
        int step = Math.Max(1, bitmap.Width / 100); // Sample every N pixels

        for (int y = 0; y < bitmap.Height; y += step)
        {
            for (int x = 0; x < bitmap.Width; x += step)
            {
                var pixel = bitmap.GetPixel(x, y);
                totalR += pixel.Red;
                totalG += pixel.Green;
                totalB += pixel.Blue;
                sampleCount++;
            }
        }

        if (sampleCount == 0)
            return ("中性", "中等");

        var avgR = totalR / sampleCount;
        var avgG = totalG / sampleCount;
        var avgB = totalB / sampleCount;

        // Calculate brightness (0-1)
        var brightness = (avgR + avgG + avgB) / (3.0 * 255.0);

        // Determine color tone
        string colorTone;
        if (avgR > avgG && avgR > avgB)
            colorTone = avgR - Math.Max(avgG, avgB) > 30 ? "暖色调" : "中性";
        else if (avgB > avgR && avgB > avgG)
            colorTone = avgB - Math.Max(avgR, avgG) > 30 ? "冷色调" : "中性";
        else
            colorTone = "中性";

        // Determine brightness level
        string brightnessLevel = brightness switch
        {
            < 0.3 => "暗",
            < 0.7 => "中等",
            _ => "亮"
        };

        return (colorTone, brightnessLevel);
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    [RelayCommand]
    private async Task ExtractFrames()
    {
        if (string.IsNullOrWhiteSpace(SelectedVideoPath) || !File.Exists(SelectedVideoPath))
        {
            StatusMessage = "请先导入视频文件。";
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentProjectId))
            CurrentProjectId = Guid.NewGuid().ToString("N");

        try
        {
            IsAnalyzing = true;
            StatusMessage = "正在抽帧...";

            var mode = (FrameExtractionMode)ExtractModeIndex;
            var request = new FrameExtractionRequest(
                SelectedVideoPath,
                CurrentProjectId!,
                mode,
                FrameCount,
                TimeInterval,
                DetectionSensitivity);

            var progress = new Progress<double>(p =>
            {
                StatusMessage = $"抽帧进度: {Math.Round(p * 100)}%";
            });

            var result = await _frameExtractionService.ExtractAsync(request, progress).ConfigureAwait(false);
            var metadata = await _videoMetadataService.GetMetadataAsync(SelectedVideoPath).ConfigureAwait(false);

            OnUi(() =>
            {
                Shots.Clear();
                BuildShotsFromFrames(result.Frames, metadata.DurationSeconds);
                RenumberShots();
                UpdateSummaryCounts();
                RecalculateTimelineLayout();
                StatusMessage = $"抽帧完成，生成 {Shots.Count} 个分镜。";
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"抽帧失败: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    private void CancelJob(GenerationJob? job)
    {
        if (job == null)
            return;

        _jobQueue.Cancel(job);
    }

    [RelayCommand]
    private void RetryJob(GenerationJob? job)
    {
        if (job == null)
            return;

        _jobQueue.Retry(job);
    }
    
    [RelayCommand]
    private void DeleteJob(GenerationJob? job)
    {
        if (job == null)
            return;

        _jobQueue.Remove(job);
    }
    
    [RelayCommand]
    private async Task UploadVideoAsync()
    {
        await ImportVideo();
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
        RenumberShots();
    }

    private void RenumberShots()
    {
        for (int i = 0; i < Shots.Count; i++)
        {
            Shots[i].ShotNumber = i + 1;
        }
    }

    public void RenumberShotsForDrag()
    {
        RenumberShots();
    }

    private async Task AnalyzeVideoAsync()
    {
        if (string.IsNullOrEmpty(SelectedVideoPath))
            return;

        try
        {
            IsAnalyzing = true;
            var result = await _videoAnalysisService.AnalyzeVideoAsync(SelectedVideoPath);

            Shots.Clear();
            foreach (var shot in result.Shots)
            {
                Shots.Add(shot);
            }

            // 镜头号自动编号（以当前顺序为准）
            RenumberShots();

            TotalDuration = result.TotalDuration;
            GeneratedImagesCount = 0;
            GeneratedVideosCount = 0;
        }
        catch (Exception ex)
        {
            StatusMessage = $"视频分析失败: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    private Task GenerateFirstFrameAsync(ShotItem shot)
    {
        EnqueueFirstFrameJob(shot);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task GenerateLastFrameAsync(ShotItem shot)
    {
        EnqueueLastFrameJob(shot);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task GenerateVideoAsync(ShotItem shot)
    {
        EnqueueVideoJob(shot);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void BatchGenerateImages()
    {
        foreach (var shot in Shots)
        {
            if (string.IsNullOrWhiteSpace(shot.FirstFrameImagePath) && !shot.IsFirstFrameGenerating)
                EnqueueFirstFrameJob(shot);

            if (string.IsNullOrWhiteSpace(shot.LastFrameImagePath) && !shot.IsLastFrameGenerating)
                EnqueueLastFrameJob(shot);
        }
    }

    [RelayCommand]
    private void BatchGenerateVideos()
    {
        foreach (var shot in Shots)
        {
            if (shot.CanGenerateVideo && string.IsNullOrWhiteSpace(shot.GeneratedVideoPath) && !shot.IsVideoGenerating)
                EnqueueVideoJob(shot);
        }

        var canRenderAll = Shots.All(s => s.CanGenerateVideo || !string.IsNullOrWhiteSpace(s.GeneratedVideoPath));
        if (canRenderAll)
        {
            // 整片合成（Full Render）也进入队列：一键完成最终视频输出
            EnqueueFullRenderJob();
        }
        else
        {
            StatusMessage = "存在未绑定首尾帧的分镜，已跳过整片合成。";
        }
    }

    private void EnqueueFullRenderJob()
    {
        _jobQueue.Enqueue(
            GenerationJobType.FullRender,
            shotNumber: null,
            async (ct, progress) =>
            {
                ct.ThrowIfCancellationRequested();
                progress.Report(0);

                // 等待所有分镜视频可用（若缺失则阻止合成）
                var startAt = DateTimeOffset.Now;
                var timeout = TimeSpan.FromMinutes(30);

                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    var snapshot = GetShotVideoSnapshot();
                    if (snapshot.Count == 0)
                        throw new InvalidOperationException("没有分镜可合成");

                    var missing = snapshot.Where(s => string.IsNullOrWhiteSpace(s.VideoPath) || !System.IO.File.Exists(s.VideoPath)).ToList();
                    if (missing.Count == 0)
                    {
                        var ordered = snapshot.OrderBy(s => s.ShotNumber).Select(s => s.VideoPath!).ToList();
                        var outputPath = await _finalRenderService.RenderAsync(ordered, ct, progress).ConfigureAwait(false);

                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = $"/select,\"{outputPath}\"",
                                UseShellExecute = true
                            });
                        }
                        catch { }

                        progress.Report(1);
                        return;
                    }

                    // 若队列里不存在任何“生成分镜视频”的任务，说明用户没有生成或已失败
                    var hasAnyVideoJob = false;
                    OnUi(() =>
                    {
                        hasAnyVideoJob = JobHistory.Any(j => j.Type == GenerationJobType.Video && j.Status is GenerationJobStatus.Queued or GenerationJobStatus.Running or GenerationJobStatus.Retrying);
                    });

                    if (!hasAnyVideoJob)
                    {
                        var msg = "存在缺失的分镜视频，无法合成：\n" + string.Join("\n", missing.Select(m => $"#{m.ShotNumber}"));
                        throw new InvalidOperationException(msg);
                    }

                    if (DateTimeOffset.Now - startAt > timeout)
                        throw new TimeoutException("等待分镜视频生成超时，无法继续合成。" );

                    await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
                }
            },
            maxAttempts: 1);
    }

    public async Task ExportVideoAsync(string targetPath)
    {
        if (!CanExportVideo)
        {
            StatusMessage = "当前分镜未全部生成成片。";
            return;
        }

        var clipPaths = Shots
            .OrderBy(s => s.ShotNumber)
            .Select(s => s.GeneratedVideoPath)
            .ToList();

        var missing = clipPaths.Where(p => string.IsNullOrWhiteSpace(p) || !File.Exists(p)).ToList();
        if (missing.Count > 0)
        {
            StatusMessage = "存在缺失的视频片段，无法导出。";
            return;
        }

        try
        {
            StatusMessage = "正在合成导出视频...";
            var ordered = clipPaths.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!).ToList();
            var output = await _finalRenderService.RenderAsync(ordered, CancellationToken.None).ConfigureAwait(false);

            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            File.Copy(output, targetPath, overwrite: true);
            StatusMessage = $"视频导出完成: {targetPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"视频导出失败: {ex.Message}";
        }
    }

    private sealed record ShotVideoSnapshot(int ShotNumber, string? VideoPath);

    private List<ShotVideoSnapshot> GetShotVideoSnapshot()
    {
        var list = new List<ShotVideoSnapshot>();
        OnUi(() =>
        {
            foreach (var s in Shots)
                list.Add(new ShotVideoSnapshot(s.ShotNumber, s.GeneratedVideoPath));
        });
        return list;
    }

    private GenerationJob EnqueueFirstFrameJob(ShotItem shot)
    {
        return _jobQueue.Enqueue(
            GenerationJobType.ImageFirst,
            shot.ShotNumber,
            async (ct, progress) =>
            {
                ct.ThrowIfCancellationRequested();
                progress.Report(0);

                if (string.IsNullOrWhiteSpace(shot.FirstFramePrompt))
                    throw new InvalidOperationException("缺少首帧提示词");

                OnUi(() => shot.IsFirstFrameGenerating = true);
                try
                {
                    var outputDir = GetProjectOutputDirectory("images");
                    var prefix = $"shot_{shot.ShotNumber:000}_first";

                    // Parse image size (e.g., "1024x1024" or "1024x768")
                    var (width, height) = ParseImageSize(shot.ImageSize);

                    // Build full prompt with professional parameters
                    var fullPrompt = BuildImagePrompt(shot.FirstFramePrompt, shot);

                    var request = new Storyboard.Infrastructure.Media.ImageGenerationRequest(
                        Prompt: fullPrompt,
                        Model: string.IsNullOrWhiteSpace(shot.SelectedModel) ? "default" : shot.SelectedModel,
                        Width: width,
                        Height: height,
                        Style: "default",
                        ShotType: shot.ShotType,
                        Composition: shot.Composition,
                        LightingType: shot.LightingType,
                        TimeOfDay: shot.TimeOfDay,
                        ColorStyle: shot.ColorStyle,
                        NegativePrompt: shot.NegativePrompt,
                        AspectRatio: shot.AspectRatio);

                    var imagePath = await _imageGenerationService.GenerateImageAsync(request, outputDir, prefix, ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();

                    OnUi(() =>
                    {
                        AddAssetToShot(shot, ShotAssetType.FirstFrameImage, imagePath, imagePath, shot.FirstFramePrompt, shot.SelectedModel);
                    });

                    progress.Report(1);
                }
                finally
                {
                    OnUi(() => shot.IsFirstFrameGenerating = false);
                }
            },
            maxAttempts: 2);
    }

    private GenerationJob EnqueueLastFrameJob(ShotItem shot)
    {
        return _jobQueue.Enqueue(
            GenerationJobType.ImageLast,
            shot.ShotNumber,
            async (ct, progress) =>
            {
                ct.ThrowIfCancellationRequested();
                progress.Report(0);

                if (string.IsNullOrWhiteSpace(shot.LastFramePrompt))
                    throw new InvalidOperationException("缺少尾帧提示词");

                OnUi(() => shot.IsLastFrameGenerating = true);
                try
                {
                    var outputDir = GetProjectOutputDirectory("images");
                    var prefix = $"shot_{shot.ShotNumber:000}_last";

                    // Parse image size (e.g., "1024x1024" or "1024x768")
                    var (width, height) = ParseImageSize(shot.ImageSize);

                    // Build full prompt with professional parameters
                    var fullPrompt = BuildImagePrompt(shot.LastFramePrompt, shot);

                    var request = new Storyboard.Infrastructure.Media.ImageGenerationRequest(
                        Prompt: fullPrompt,
                        Model: string.IsNullOrWhiteSpace(shot.SelectedModel) ? "default" : shot.SelectedModel,
                        Width: width,
                        Height: height,
                        Style: "default",
                        ShotType: shot.ShotType,
                        Composition: shot.Composition,
                        LightingType: shot.LightingType,
                        TimeOfDay: shot.TimeOfDay,
                        ColorStyle: shot.ColorStyle,
                        NegativePrompt: shot.NegativePrompt,
                        AspectRatio: shot.AspectRatio);

                    var imagePath = await _imageGenerationService.GenerateImageAsync(request, outputDir, prefix, ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();

                    OnUi(() =>
                    {
                        AddAssetToShot(shot, ShotAssetType.LastFrameImage, imagePath, imagePath, shot.LastFramePrompt, shot.SelectedModel);
                    });

                    progress.Report(1);
                }
                finally
                {
                    OnUi(() => shot.IsLastFrameGenerating = false);
                }
            },
            maxAttempts: 2);
    }

    private (int Width, int Height) ParseImageSize(string? imageSize)
    {
        if (string.IsNullOrWhiteSpace(imageSize))
            return (1024, 1024); // Default size

        var parts = imageSize.Split('x', 'X', '×');
        if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var w) && int.TryParse(parts[1].Trim(), out var h))
            return (w, h);

        return (1024, 1024); // Fallback to default
    }

    private string BuildImagePrompt(string basePrompt, ShotItem shot)
    {
        var parts = new List<string> { basePrompt };

        // Add professional parameters to enhance the prompt
        if (!string.IsNullOrWhiteSpace(shot.ShotType))
            parts.Add($"shot type: {shot.ShotType}");
        if (!string.IsNullOrWhiteSpace(shot.Composition))
            parts.Add($"composition: {shot.Composition}");
        if (!string.IsNullOrWhiteSpace(shot.LightingType))
            parts.Add($"lighting: {shot.LightingType}");
        if (!string.IsNullOrWhiteSpace(shot.TimeOfDay))
            parts.Add($"time: {shot.TimeOfDay}");
        if (!string.IsNullOrWhiteSpace(shot.ColorStyle))
            parts.Add($"color style: {shot.ColorStyle}");

        return string.Join(", ", parts);
    }

    private GenerationJob EnqueueAiParseJob(ShotItem shot, AiWriteMode? mode = null)
    {
        return _jobQueue.Enqueue(
            GenerationJobType.AiParse,
            shot.ShotNumber,
            async (ct, progress) =>
            {
                ct.ThrowIfCancellationRequested();
                progress.Report(0);

                OnUi(() => shot.IsAiParsing = true);
                try
                {
                    var request = await CaptureAiRequestAsync(shot).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(request.FirstFramePath) || !File.Exists(request.FirstFramePath))
                    {
                        _logger.LogWarning("AI 解析失败: 分镜 #{ShotNumber} 缺少素材图片", shot.ShotNumber);
                        throw new InvalidOperationException("缺少素材图片，请先导入或生成素材图片。");
                    }

                    _logger.LogInformation("开始 AI 解析分镜 #{ShotNumber}，素材图片: {MaterialPath}", shot.ShotNumber, request.FirstFramePath);
                    var description = await _aiShotService.AnalyzeShotAsync(request, ct).ConfigureAwait(false);
                    progress.Report(0.7);

                    AiWriteMode? writeMode = mode;
                    if (writeMode == null && NeedsAiWriteMode(shot))
                    {
                        writeMode = await RequestAiWriteModeAsync().ConfigureAwait(false);
                    }

                    if (writeMode == null)
                    {
                        _logger.LogInformation("AI 解析已取消: 分镜 #{ShotNumber}", shot.ShotNumber);
                        return;
                    }

                    OnUi(() => ApplyAiShotDescription(shot, description, writeMode.Value));
                    _logger.LogInformation("AI 解析完成: 分镜 #{ShotNumber}", shot.ShotNumber);
                    progress.Report(1);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AI 解析失败: 分镜 #{ShotNumber}", shot.ShotNumber);
                    throw;
                }
                finally
                {
                    OnUi(() => shot.IsAiParsing = false);
                }
            },
            maxAttempts: 2);
    }

    public GenerationJob QueueAiParse(ShotItem shot, AiWriteMode? mode = null)
        => EnqueueAiParseJob(shot, mode);

    public GenerationJob QueueFirstFrame(ShotItem shot)
        => EnqueueFirstFrameJob(shot);

    public GenerationJob QueueLastFrame(ShotItem shot)
        => EnqueueLastFrameJob(shot);

    public GenerationJob QueueVideo(ShotItem shot)
        => EnqueueVideoJob(shot);

    public bool NeedsAiWriteModeForBatch(ShotItem shot)
        => NeedsAiWriteMode(shot);

    public Task<AiWriteMode?> PromptAiWriteModeAsync()
        => RequestAiWriteModeAsync();

    private GenerationJob EnqueueVideoJob(ShotItem shot)
    {
        return _jobQueue.Enqueue(
            GenerationJobType.Video,
            shot.ShotNumber,
            async (ct, progress) =>
            {
                ct.ThrowIfCancellationRequested();
                progress.Report(0);

                // Allow video generation even when reference images are missing (0/1/2 allowed).
                OnUi(() => shot.IsVideoGenerating = true);
                try
                {
                    var outputDir = GetProjectOutputDirectory("videos");
                    var prefix = $"shot_{shot.ShotNumber:000}_video";
                    var videoPath = await _videoGenerationService.GenerateVideoAsync(
                        shot,
                        outputDir,
                        prefix,
                        ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();

                    var thumbnailPath = await TryCreateVideoThumbnailAsync(videoPath, ct).ConfigureAwait(false);

                    OnUi(() =>
                    {
                        AddAssetToShot(shot, ShotAssetType.GeneratedVideo, videoPath, thumbnailPath, null, shot.SelectedModel);
                    });

                    progress.Report(1);
                }
                finally
                {
                    OnUi(() => shot.IsVideoGenerating = false);
                }
            },
            maxAttempts: 2);
    }

    private static bool NeedsAiWriteMode(ShotItem shot)
    {
        return !string.IsNullOrWhiteSpace(shot.ShotType)
            || !string.IsNullOrWhiteSpace(shot.CoreContent)
            || !string.IsNullOrWhiteSpace(shot.ActionCommand)
            || !string.IsNullOrWhiteSpace(shot.SceneSettings)
            || !string.IsNullOrWhiteSpace(shot.FirstFramePrompt)
            || !string.IsNullOrWhiteSpace(shot.LastFramePrompt);
    }

    private Task<AiShotAnalysisRequest> CaptureAiRequestAsync(ShotItem shot)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(new AiShotAnalysisRequest(
                shot.MaterialFilePath,
                shot.LastFrameImagePath,
                shot.ShotType,
                shot.CoreContent,
                shot.ActionCommand,
                shot.SceneSettings,
                shot.FirstFramePrompt,
                shot.LastFramePrompt));
        }

        return Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => new AiShotAnalysisRequest(
            shot.MaterialFilePath,
            shot.LastFrameImagePath,
            shot.ShotType,
            shot.CoreContent,
            shot.ActionCommand,
            shot.SceneSettings,
            shot.FirstFramePrompt,
            shot.LastFramePrompt)).GetTask();
    }

    private Task<AiWriteMode?> RequestAiWriteModeAsync()
    {
        // Ensure we're on the UI thread
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            return Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(RequestAiWriteModeOnUiThreadAsync);
        }

        return RequestAiWriteModeOnUiThreadAsync();
    }

    private async Task<AiWriteMode?> RequestAiWriteModeOnUiThreadAsync()
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;
        var dialog = new AiWriteModeDialog();

        if (owner == null)
            return AiWriteMode.Overwrite;

        return await dialog.ShowDialog<AiWriteMode?>(owner);
    }

    private static void ApplyAiShotDescription(ShotItem shot, AiShotDescription description, AiWriteMode mode)
    {
        shot.ShotType = MergeField(shot.ShotType, description.ShotType, mode);
        shot.CoreContent = MergeField(shot.CoreContent, description.CoreContent, mode);
        shot.ActionCommand = MergeField(shot.ActionCommand, description.ActionCommand, mode);
        shot.SceneSettings = MergeField(shot.SceneSettings, description.SceneSettings, mode);
        shot.FirstFramePrompt = MergeField(shot.FirstFramePrompt, description.FirstFramePrompt, mode);
        shot.LastFramePrompt = MergeField(shot.LastFramePrompt, description.LastFramePrompt, mode);

        // Apply image professional parameters
        if (!string.IsNullOrWhiteSpace(description.Composition))
            shot.Composition = MergeField(shot.Composition, description.Composition, mode);
        if (!string.IsNullOrWhiteSpace(description.LightingType))
            shot.LightingType = MergeField(shot.LightingType, description.LightingType, mode);
        if (!string.IsNullOrWhiteSpace(description.TimeOfDay))
            shot.TimeOfDay = MergeField(shot.TimeOfDay, description.TimeOfDay, mode);
        if (!string.IsNullOrWhiteSpace(description.ColorStyle))
            shot.ColorStyle = MergeField(shot.ColorStyle, description.ColorStyle, mode);
        if (!string.IsNullOrWhiteSpace(description.NegativePrompt))
            shot.NegativePrompt = MergeField(shot.NegativePrompt, description.NegativePrompt, mode);
        if (!string.IsNullOrWhiteSpace(description.ImageSize))
            shot.ImageSize = MergeField(shot.ImageSize, description.ImageSize, mode);

        // Apply video parameters
        if (!string.IsNullOrWhiteSpace(description.VideoPrompt))
            shot.VideoPrompt = MergeField(shot.VideoPrompt, description.VideoPrompt, mode);
        if (!string.IsNullOrWhiteSpace(description.SceneDescription))
            shot.SceneDescription = MergeField(shot.SceneDescription, description.SceneDescription, mode);
        if (!string.IsNullOrWhiteSpace(description.ActionDescription))
            shot.ActionDescription = MergeField(shot.ActionDescription, description.ActionDescription, mode);
        if (!string.IsNullOrWhiteSpace(description.StyleDescription))
            shot.StyleDescription = MergeField(shot.StyleDescription, description.StyleDescription, mode);
        if (!string.IsNullOrWhiteSpace(description.CameraMovement))
            shot.CameraMovement = MergeField(shot.CameraMovement, description.CameraMovement, mode);
        if (!string.IsNullOrWhiteSpace(description.ShootingStyle))
            shot.ShootingStyle = MergeField(shot.ShootingStyle, description.ShootingStyle, mode);
        if (!string.IsNullOrWhiteSpace(description.VideoEffect))
            shot.VideoEffect = MergeField(shot.VideoEffect, description.VideoEffect, mode);
        if (!string.IsNullOrWhiteSpace(description.VideoNegativePrompt))
            shot.VideoNegativePrompt = MergeField(shot.VideoNegativePrompt, description.VideoNegativePrompt, mode);
        if (!string.IsNullOrWhiteSpace(description.VideoResolution))
            shot.VideoResolution = MergeField(shot.VideoResolution, description.VideoResolution, mode);
        if (!string.IsNullOrWhiteSpace(description.VideoRatio))
            shot.VideoRatio = MergeField(shot.VideoRatio, description.VideoRatio, mode);
    }

    private static string MergeField(string current, string incoming, AiWriteMode mode)
    {
        if (string.IsNullOrWhiteSpace(incoming))
            return current;

        if (string.IsNullOrWhiteSpace(current))
            return incoming;

        return mode switch
        {
            AiWriteMode.Overwrite => incoming,
            AiWriteMode.Append => $"{current}\n{incoming}",
            AiWriteMode.Skip => current,
            _ => current
        };
    }

    private void AddAssetToShot(ShotItem shot, ShotAssetType type, string filePath, string? thumbnailPath, string? prompt, string? model)
    {
        var list = type switch
        {
            ShotAssetType.FirstFrameImage => shot.FirstFrameAssets,
            ShotAssetType.LastFrameImage => shot.LastFrameAssets,
            ShotAssetType.GeneratedVideo => shot.VideoAssets,
            _ => null
        };

        if (list == null)
            return;

        if (list.Any(a => string.Equals(a.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
            return;

        // If we're adding a generated video, make it the shot's current generated video
        if (type == ShotAssetType.GeneratedVideo)
            shot.GeneratedVideoPath = filePath;

        list.Insert(0, new ShotAssetItem
        {
            Type = type,
            FilePath = filePath,
            ThumbnailPath = type == ShotAssetType.GeneratedVideo ? null : thumbnailPath,
            VideoThumbnailPath = type == ShotAssetType.GeneratedVideo ? thumbnailPath : null,
            Prompt = prompt,
            Model = model,
            CreatedAt = DateTimeOffset.Now,
            IsSelected = type switch
            {
                ShotAssetType.FirstFrameImage => string.Equals(shot.FirstFrameImagePath, filePath, StringComparison.OrdinalIgnoreCase),
                ShotAssetType.LastFrameImage => string.Equals(shot.LastFrameImagePath, filePath, StringComparison.OrdinalIgnoreCase),
                ShotAssetType.GeneratedVideo => string.Equals(shot.GeneratedVideoPath, filePath, StringComparison.OrdinalIgnoreCase),
                _ => false
            }
        });

        MarkUndoableChange();
        UpdateSummaryCounts();
    }

    private async Task<string?> TryCreateVideoThumbnailAsync(string videoPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            return null;

        var outputDir = GetProjectOutputDirectory("video-thumbnails");
        Directory.CreateDirectory(outputDir);

        var baseName = Path.GetFileNameWithoutExtension(videoPath);
        var thumbPath = Path.Combine(outputDir, $"{baseName}_thumb.jpg");

        var args = $"-y -hide_banner -loglevel error -ss 0.2 -i \"{videoPath}\" -frames:v 1 -q:v 2 \"{thumbPath}\"";
        var (exitCode, _stdout, stderr) = await RunProcessCaptureAsync(
            FfmpegLocator.GetFfmpegPath(),
            args,
            cancellationToken).ConfigureAwait(false);

        if (exitCode != 0 || !File.Exists(thumbPath))
        {
            _logger.LogWarning("视频缩略图生成失败: {Error}", stderr);
            return null;
        }

        return thumbPath;
    }

    private async Task EnsureVideoAssetThumbnailAsync(ShotAssetItem asset)
    {
        var (filePath, existingThumb) = await OnUiAsync(() => (asset.FilePath, asset.VideoThumbnailPath)).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(filePath))
            return;

        if (!string.IsNullOrWhiteSpace(existingThumb) && File.Exists(existingThumb))
            return;

        var thumbnailPath = await TryCreateVideoThumbnailAsync(filePath, CancellationToken.None).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(thumbnailPath))
            return;

        OnUi(() => asset.VideoThumbnailPath = thumbnailPath);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessCaptureAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stdout.AppendLine(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stderr.AppendLine(e.Data);
        };

        if (!proc.Start())
            throw new InvalidOperationException($"无法启动进程: {fileName}");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (proc.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static void OnUi(Action action)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(action);
        }
    }

    private static Task<T> OnUiAsync<T>(Func<T> func)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return Task.FromResult(func());

        return Dispatcher.UIThread.InvokeAsync(func).GetTask();
    }

    public void MoveShot(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Shots.Count || newIndex < 0 || newIndex >= Shots.Count)
            return;

        var shot = Shots[oldIndex];
        Shots.RemoveAt(oldIndex);
        Shots.Insert(newIndex, shot);

        // 更新镜头号
        RenumberShots();
    }

    private void Shots_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var tracked in new List<ShotItem>(_trackedShots))
            {
                UnregisterShotEvents(tracked);
            }

            foreach (var shot in Shots)
            {
                RegisterShotEvents(shot);
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is ShotItem shot)
                {
                    RegisterShotEvents(shot);
                }
            }
        }

        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is ShotItem shot)
                {
                    UnregisterShotEvents(shot);
                }
            }
        }

        OnPropertyChanged(nameof(HasShots));
        OnPropertyChanged(nameof(HasSelectedShots));
        OnPropertyChanged(nameof(SelectedShotsCountText));
        UpdateSummaryCounts();

        RecalculateTimelineLayout();

        // Track add/remove/reorder operations as undoable changes.
        if (e.Action is NotifyCollectionChangedAction.Add
            or NotifyCollectionChangedAction.Remove
            or NotifyCollectionChangedAction.Move
            or NotifyCollectionChangedAction.Replace
            or NotifyCollectionChangedAction.Reset)
        {
            MarkUndoableChange();
        }
    }

    private void RegisterShotEvents(ShotItem shot)
    {
        if (_trackedShots.Add(shot))
        {
            shot.PropertyChanged += Shot_PropertyChanged;
        }
    }

    private void UnregisterShotEvents(ShotItem shot)
    {
        if (_trackedShots.Remove(shot))
        {
            shot.PropertyChanged -= Shot_PropertyChanged;
        }
    }

    private void Shot_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShotItem.IsChecked))
        {
            OnPropertyChanged(nameof(HasSelectedShots));
            OnPropertyChanged(nameof(SelectedShotsCountText));
        }

        if (e.PropertyName is nameof(ShotItem.FirstFrameImagePath)
            or nameof(ShotItem.IsFirstFrameGenerating)
            or nameof(ShotItem.IsLastFrameGenerating)
            or nameof(ShotItem.IsVideoGenerating)
            or nameof(ShotItem.GeneratedVideoPath))
        {
            UpdateSummaryCounts();
        }

        // Snapshot-based undo/redo for user-editable shot properties.
        if (e.PropertyName is nameof(ShotItem.Duration)
            or nameof(ShotItem.StartTime)
            or nameof(ShotItem.EndTime)
            or nameof(ShotItem.FirstFramePrompt)
            or nameof(ShotItem.LastFramePrompt)
            or nameof(ShotItem.ShotType)
            or nameof(ShotItem.CoreContent)
            or nameof(ShotItem.ActionCommand)
            or nameof(ShotItem.SceneSettings)
            or nameof(ShotItem.SelectedModel)
            or nameof(ShotItem.FirstFrameImagePath)
            or nameof(ShotItem.LastFrameImagePath)
            or nameof(ShotItem.GeneratedVideoPath)
            or nameof(ShotItem.MaterialThumbnailPath)
            or nameof(ShotItem.MaterialFilePath)
            or nameof(ShotItem.SelectedTabIndex)
            or nameof(ShotItem.TimelineStartPosition)
            or nameof(ShotItem.TimelineWidth))
        {
            MarkUndoableChange();
        }

        if (e.PropertyName is nameof(ShotItem.Duration))
            RecalculateTimelineLayout();
    }

    private void UpdateSummaryCounts()
    {
        CompletedShotsCount = Shots.Count(shot => !string.IsNullOrWhiteSpace(shot.FirstFrameImagePath));
        CompletedVideoShotsCount = Shots.Count(shot => !string.IsNullOrWhiteSpace(shot.GeneratedVideoPath));
        ShotsRenderingCount = Shots.Count(shot => shot.IsFirstFrameGenerating || shot.IsLastFrameGenerating || shot.IsVideoGenerating);
        GeneratedImagesCount = Shots.Sum(shot => shot.FirstFrameAssets.Count + shot.LastFrameAssets.Count);
        GeneratedVideosCount = Shots.Sum(shot => shot.VideoAssets.Count);
        GeneratingCount = ShotsRenderingCount;

        OnPropertyChanged(nameof(HasShots));
        OnPropertyChanged(nameof(CanExportVideo));
    }
}


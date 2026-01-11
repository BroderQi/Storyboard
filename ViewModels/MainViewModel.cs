
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
// using Microsoft.Win32; // WPF specific - remove for Avalonia
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
// using System.Windows; // WPF specific - remove for Avalonia
using Storyboard.Models;
using Storyboard.Application.Abstractions;
// using Storyboard.Views.Windows; // Old WPF views - remove for Avalonia
using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Storyboard.Application.Services;


namespace Storyboard.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IVideoAnalysisService _videoAnalysisService;
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IVideoGenerationService _videoGenerationService;
    private readonly IFinalRenderService _finalRenderService;
    private readonly IJobQueueService _jobQueue;
    private readonly IProjectStore _projectStore;

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
        bool IsChecked,
        int SelectedTabIndex,
        double TimelineStartPosition,
        double TimelineWidth);

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

    partial void OnExtractModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsFixedOrDynamicMode));
        OnPropertyChanged(nameof(IsIntervalMode));
        OnPropertyChanged(nameof(IsDynamicMode));
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

        // TODO: Implement Avalonia preview dialog
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

        // TODO: Implement Avalonia image preview dialog
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

        // TODO: Implement Avalonia image preview dialog
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
        IImageGenerationService imageGenerationService,
        IVideoGenerationService videoGenerationService,
        IFinalRenderService finalRenderService,
        IJobQueueService jobQueue,
        IProjectStore projectStore)
    {
        _videoAnalysisService = videoAnalysisService;
        _imageGenerationService = imageGenerationService;
        _videoGenerationService = videoGenerationService;
        _finalRenderService = finalRenderService;
        _jobQueue = jobQueue;
        _projectStore = projectStore;

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
        });

        _pendingUndoSnapshot = null;
        _lastSnapshot = TakeSnapshot();
        UpdateUndoRedoState();
        _isRestoringSnapshot = false;
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
        var hasImages = Shots.Count(s => (!string.IsNullOrWhiteSpace(s.FirstFrameImagePath) && File.Exists(s.FirstFrameImagePath))
                                       || (!string.IsNullOrWhiteSpace(s.LastFrameImagePath) && File.Exists(s.LastFrameImagePath)));
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
                s.MaterialFilePath))
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

    public ObservableCollection<GenerationJob> JobHistory => _jobQueue.Jobs;

    private void AttachShotEventHandlers(ShotItem shot)
    {
        shot.DuplicateRequested += OnShotDuplicateRequested;
        shot.DeleteRequested += OnShotDeleteRequested;
    }

    private void DetachShotEventHandlers(ShotItem shot)
    {
        shot.DuplicateRequested -= OnShotDuplicateRequested;
        shot.DeleteRequested -= OnShotDeleteRequested;
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
            LoadTestData();
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
                                MaterialFilePath = s.MaterialFilePath
                            };
                            AttachShotEventHandlers(shot);
                            Shots.Add(shot);
                        }
                        RenumberShots();
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
        // TODO: Switch between Grid and Timeline view
    }

    [RelayCommand]
    private void ToggleTaskManager()
    {
        // TODO: Show/hide task manager panel
    }

    [RelayCommand]
    private void ToggleProviderSettings()
    {
        // TODO: Show/hide provider settings dialog
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

    public void GenerateShotsFromTextPrompt()
    {
        // Keep behavior aligned with React demo: generate a couple of example shots.
        // This is a placeholder until real AI text->shot parsing is integrated.
        var baseIndex = Shots.Count;

        var shot1 = new ShotItem(baseIndex + 1)
        {
            Duration = 5,
            ShotType = "远景",
            CoreContent = "城市天际线",
            ActionCommand = "缓慢推进",
            SceneSettings = "清晨，阳光照射在高楼大厦上",
            FirstFramePrompt = "城市远景，清晨金色阳光",
            LastFramePrompt = "城市近景，建筑细节清晰",
            SelectedModel = "RunwayGen3"
        };
        AttachShotEventHandlers(shot1);

        var shot2 = new ShotItem(baseIndex + 2)
        {
            Duration = 4,
            ShotType = "中景",
            CoreContent = "街道人群",
            ActionCommand = "跟随主角行走",
            SceneSettings = "繁忙的街道，人来人往",
            FirstFramePrompt = "主角从远处走来",
            LastFramePrompt = "主角面部特写",
            SelectedModel = "Pika"
        };
        AttachShotEventHandlers(shot2);

        Shots.Add(shot1);
        Shots.Add(shot2);
        RenumberShots();

        TextToShotPrompt = string.Empty;
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
    private void AIAnalyzeAll()
    {
        // TODO: Implement AI analyze all shots
    }

    [RelayCommand]
    private async Task ImportVideo()
    {
        // TODO: Implement Avalonia file picker
        // For now, load test data and provide demo video metadata to match sidebar UX.
        LoadTestData();

        HasVideoFile = true;
        VideoFileDuration = "120";
        VideoFileResolution = "1920x1080";
        VideoFileFps = "30";
    }

    [RelayCommand]
    private async Task ExtractFrames()
    {
        // TODO: Implement frame extraction
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void CancelJob(GenerationJob? job)
    {
        if (job == null)
            return;

        _jobQueue.Cancel(job);
    }
    
    private void LoadTestData()
    {
        var shot1 = new ShotItem(1)
        {
            Duration = 3.5,
            FirstFramePrompt = "清晨的城市街道，阳光透过高楼洒下",
            LastFramePrompt = "镜头缓缓推进到咖啡店门口",
            ShotType = "推镜",
            CoreContent = "城市街景",
            ActionCommand = "缓慢推进",
            SceneSettings = "早晨，暖色调，自然光",
            SelectedModel = "RunwayGen3"
        };
        AttachShotEventHandlers(shot1);
        Shots.Add(shot1);
        
        var shot2 = new ShotItem(2)
        {
            Duration = 2,
            FirstFramePrompt = "咖啡师正在制作拿铁，特写镜头",
            LastFramePrompt = "拉花完成，呈现美图案",
            ShotType = "特写",
            CoreContent = "咖啡制作",
            ActionCommand = "稳定拍摄",
            SceneSettings = "室内，暖光，浅景深",
            SelectedModel = "Pika"
        };
        AttachShotEventHandlers(shot2);
        Shots.Add(shot2);
        
        var shot3 = new ShotItem(3)
        {
            Duration = 4,
            FirstFramePrompt = "顾客坐在窗边，手握咖啡杯",
            LastFramePrompt = "望向窗外，露出微笑",
            ShotType = "中景",
            CoreContent = "人物场景",
            ActionCommand = "自然转头",
            SceneSettings = "窗边，逆光，柔和",
            SelectedModel = "RunwayGen3"
        };
        AttachShotEventHandlers(shot3);
        Shots.Add(shot3);
    }

    [RelayCommand]
    private async Task UploadVideoAsync()
    {
        // TODO: Implement Avalonia file picker
        // For now, use a test video path or skip
        // var storageProvider = TopLevel.GetTopLevel(mainWindow)?.StorageProvider;
        // if (storageProvider != null) { ... }
        
        // Temporary: allow manual path input or skip
        return;
    }

    [RelayCommand]
    private void AddShot()
    {
        var newShot = new ShotItem(Shots.Count + 1);
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
            // TODO: Implement Avalonia message dialog
            System.Diagnostics.Debug.WriteLine($"视频分析失败: {ex.Message}");
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
            if (string.IsNullOrWhiteSpace(shot.GeneratedVideoPath) && !shot.IsVideoGenerating)
                EnqueueVideoJob(shot);
        }

        // 整片合成（Full Render）也进入队列：一键完成最终视频输出
        EnqueueFullRenderJob();
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

    private void EnqueueFirstFrameJob(ShotItem shot)
    {
        _jobQueue.Enqueue(
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
                    var imagePath = await _imageGenerationService.GenerateImageAsync(shot.FirstFramePrompt, shot.SelectedModel);
                    ct.ThrowIfCancellationRequested();

                    OnUi(() =>
                    {
                        shot.FirstFrameImagePath = imagePath;
                        GeneratedImagesCount++;
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

    private void EnqueueLastFrameJob(ShotItem shot)
    {
        _jobQueue.Enqueue(
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
                    var imagePath = await _imageGenerationService.GenerateImageAsync(shot.LastFramePrompt, shot.SelectedModel);
                    ct.ThrowIfCancellationRequested();

                    OnUi(() =>
                    {
                        shot.LastFrameImagePath = imagePath;
                        GeneratedImagesCount++;
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

    private void EnqueueVideoJob(ShotItem shot)
    {
        _jobQueue.Enqueue(
            GenerationJobType.Video,
            shot.ShotNumber,
            async (ct, progress) =>
            {
                ct.ThrowIfCancellationRequested();
                progress.Report(0);

                if (string.IsNullOrWhiteSpace(shot.FirstFrameImagePath) || string.IsNullOrWhiteSpace(shot.LastFrameImagePath))
                    throw new InvalidOperationException("缺少首帧/尾帧图片，请先生成图片");

                OnUi(() => shot.IsVideoGenerating = true);
                try
                {
                    var videoPath = await _videoGenerationService.GenerateVideoAsync(shot);
                    ct.ThrowIfCancellationRequested();

                    OnUi(() =>
                    {
                        shot.GeneratedVideoPath = videoPath;
                        GeneratedVideosCount++;
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

        OnPropertyChanged(nameof(HasShots));
        OnPropertyChanged(nameof(CanExportVideo));
    }
}


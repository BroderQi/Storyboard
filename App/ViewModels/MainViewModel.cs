using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Models;
using Storyboard.ViewModels.Project;
using Storyboard.ViewModels.Queue;
using Storyboard.ViewModels.Import;
using Storyboard.ViewModels.Shot;
using Storyboard.ViewModels.Generation;
using Storyboard.ViewModels.Shared;
using System.Linq;

namespace Storyboard.ViewModels;

/// <summary>
/// 主 ViewModel - 作为主协调器，管理所有子 ViewModel
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IMessenger _messenger;
    private readonly ILogger<MainViewModel> _logger;

    // 子 ViewModels
    public ProjectManagementViewModel ProjectManagement { get; }
    public ShotListViewModel ShotList { get; }
    public VideoImportViewModel VideoImport { get; }
    public FrameExtractionViewModel FrameExtraction { get; }
    public AiAnalysisViewModel AiAnalysis { get; }
    public ImageGenerationViewModel ImageGeneration { get; }
    public VideoGenerationViewModel VideoGeneration { get; }
    public ExportViewModel Export { get; }
    public JobQueueViewModel JobQueue { get; }
    public HistoryViewModel History { get; }
    public TimelineViewModel Timeline { get; }

    // 全局 UI 状态
    [ObservableProperty]
    private bool _isGridView = true;

    [ObservableProperty]
    private bool _isListView;

    [ObservableProperty]
    private bool _isTimelineView;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private bool _isProviderSettingsDialogOpen;

    [ObservableProperty]
    private bool _isTextToShotDialogOpen;

    [ObservableProperty]
    private string _textToShotPrompt = string.Empty;

    // 委托属性 - 暴露子 ViewModel 的属性以保持向后兼容
    public bool HasProjects => ProjectManagement.HasProjects;
    public bool HasShots => ShotList.HasShots;
    public bool HasSelectedShots => ShotList.HasSelectedShots;
    public string SelectedShotsCountText => ShotList.SelectedShotsCountText;
    public bool CanExportVideo => Export.CanExportVideo;

    // 项目相关属性
    public bool HasCurrentProject => ProjectManagement.HasCurrentProject;
    public Models.ProjectInfo? CurrentProject => ProjectManagement.Projects.FirstOrDefault(p => p.Id == ProjectManagement.CurrentProjectId);
    public System.Collections.ObjectModel.ObservableCollection<Models.ProjectInfo> Projects => ProjectManagement.Projects;
    public string ProjectName => ProjectManagement.ProjectName;
    public string? CurrentProjectId => ProjectManagement.CurrentProjectId;
    public string NewProjectName
    {
        get => ProjectManagement.NewProjectName;
        set => ProjectManagement.NewProjectName = value;
    }
    public bool IsNewProjectDialogOpen
    {
        get => ProjectManagement.IsNewProjectDialogOpen;
        set => ProjectManagement.IsNewProjectDialogOpen = value;
    }

    // 镜头相关属性
    public System.Collections.ObjectModel.ObservableCollection<Models.ShotItem> Shots => ShotList.Shots;
    public Models.ShotItem? SelectedShot
    {
        get => ShotList.SelectedShot;
        set => ShotList.SelectedShot = value;
    }
    public double TotalDuration => ShotList.TotalDuration;
    public int CompletedShotsCount => ShotList.CompletedShotsCount;
    public int CompletedVideoShotsCount => ShotList.CompletedVideoShotsCount;

    // 视频导入相关属性
    public bool HasVideoFile => VideoImport.HasVideoFile;
    public string VideoFileDuration => VideoImport.VideoFileDuration;
    public string VideoFileResolution => VideoImport.VideoFileResolution;
    public string VideoFileFps => VideoImport.VideoFileFps;

    // 帧提取相关属性
    public int ExtractModeIndex
    {
        get => FrameExtraction.ExtractModeIndex;
        set => FrameExtraction.ExtractModeIndex = value;
    }
    public bool IsFixedOrDynamicMode => FrameExtraction.IsFixedOrDynamicMode;
    public bool IsIntervalMode => FrameExtraction.IsIntervalMode;
    public bool IsKeyframeMode => FrameExtraction.IsKeyframeMode;
    public int FrameCount
    {
        get => FrameExtraction.FrameCount;
        set => FrameExtraction.FrameCount = value;
    }
    public double TimeInterval
    {
        get => FrameExtraction.TimeInterval;
        set => FrameExtraction.TimeInterval = value;
    }
    public double DetectionSensitivity
    {
        get => FrameExtraction.DetectionSensitivity;
        set => FrameExtraction.DetectionSensitivity = value;
    }

    // 任务队列相关属性
    public bool IsTaskManagerDialogOpen
    {
        get => JobQueue.IsTaskManagerDialogOpen;
        set => JobQueue.IsTaskManagerDialogOpen = value;
    }
    public System.Collections.ObjectModel.ObservableCollection<Models.GenerationJob> JobHistory => JobQueue.JobHistory;

    // 导出相关属性
    public bool IsExportDialogOpen
    {
        get => Export.IsExportDialogOpen;
        set => Export.IsExportDialogOpen = value;
    }

    // 历史记录相关属性
    public bool CanUndo => History.CanUndo;
    public bool CanRedo => History.CanRedo;

    // 时间轴相关属性
    public System.Collections.ObjectModel.ObservableCollection<Storyboard.ViewModels.Shot.TimeMarker> TimeMarkers => Timeline.TimeMarkers;
    public double TimelinePixelsPerSecond => Timeline.TimelinePixelsPerSecond;
    public double TimelineWidth => Timeline.TimelineWidth;

    // 命令委托
    public IRelayCommand ShowCreateProjectDialogCommand => ProjectManagement.ShowCreateProjectDialogCommand;
    public IRelayCommand<string?> CreateNewProjectCommand => ProjectManagement.CreateNewProjectCommand;
    public IAsyncRelayCommand<Models.ProjectInfo?> OpenProjectCommand => ProjectManagement.OpenProjectCommand;
    public IAsyncRelayCommand<Models.ProjectInfo?> DeleteProjectCommand => ProjectManagement.DeleteProjectCommand;
    public IRelayCommand CloseProjectCommand => ProjectManagement.CloseProjectCommand;

    public IAsyncRelayCommand ImportVideoCommand => VideoImport.ImportVideoCommand;
    public IAsyncRelayCommand ExtractFramesCommand => FrameExtraction.ExtractFramesCommand;

    public IRelayCommand AIAnalyzeAllCommand => AiAnalysis.AIAnalyzeAllCommand;

    public IRelayCommand AddShotCommand => ShotList.AddShotCommand;

    public IRelayCommand ShowExportDialogCommand => Export.ShowExportDialogCommand;
    public IAsyncRelayCommand<string?> ExportVideoCommand => Export.ExportVideoCommand;

    public IRelayCommand ShowTaskManagerCommand => JobQueue.ShowTaskManagerCommand;
    public IRelayCommand ToggleTaskManagerCommand => JobQueue.ToggleTaskManagerCommand;
    public IRelayCommand<Models.GenerationJob?> CancelJobCommand => JobQueue.CancelJobCommand;

    public IRelayCommand UndoCommand => History.UndoCommand;
    public IRelayCommand RedoCommand => History.RedoCommand;

    public System.Threading.Tasks.Task ExportVideoAsync(string? outputPath) => Export.ExportVideoCommand.ExecuteAsync(outputPath);

    public MainViewModel(
        ProjectManagementViewModel projectManagement,
        ShotListViewModel shotList,
        VideoImportViewModel videoImport,
        FrameExtractionViewModel frameExtraction,
        AiAnalysisViewModel aiAnalysis,
        ImageGenerationViewModel imageGeneration,
        VideoGenerationViewModel videoGeneration,
        ExportViewModel export,
        JobQueueViewModel jobQueue,
        HistoryViewModel history,
        TimelineViewModel timeline,
        IMessenger messenger,
        ILogger<MainViewModel> logger)
    {
        ProjectManagement = projectManagement;
        ShotList = shotList;
        VideoImport = videoImport;
        FrameExtraction = frameExtraction;
        AiAnalysis = aiAnalysis;
        ImageGeneration = imageGeneration;
        VideoGeneration = videoGeneration;
        Export = export;
        JobQueue = jobQueue;
        History = history;
        Timeline = timeline;
        _messenger = messenger;
        _logger = logger;

        // 订阅子 ViewModel 的属性变更以更新计算属性
        ProjectManagement.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ProjectManagement.HasProjects))
                OnPropertyChanged(nameof(HasProjects));
            if (e.PropertyName == nameof(ProjectManagement.HasCurrentProject))
                OnPropertyChanged(nameof(HasCurrentProject));
            if (e.PropertyName == nameof(ProjectManagement.CurrentProjectId))
                OnPropertyChanged(nameof(CurrentProjectId));
            if (e.PropertyName == nameof(ProjectManagement.ProjectName))
                OnPropertyChanged(nameof(ProjectName));
        };

        ShotList.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ShotList.HasShots))
                OnPropertyChanged(nameof(HasShots));
            if (e.PropertyName == nameof(ShotList.HasSelectedShots))
                OnPropertyChanged(nameof(HasSelectedShots));
            if (e.PropertyName == nameof(ShotList.SelectedShotsCountText))
                OnPropertyChanged(nameof(SelectedShotsCountText));
            if (e.PropertyName == nameof(ShotList.SelectedShot))
                OnPropertyChanged(nameof(SelectedShot));
        };

        Export.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Export.CanExportVideo))
                OnPropertyChanged(nameof(CanExportVideo));
            if (e.PropertyName == nameof(Export.IsExportDialogOpen))
                OnPropertyChanged(nameof(IsExportDialogOpen));
        };

        JobQueue.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(JobQueue.IsTaskManagerDialogOpen))
                OnPropertyChanged(nameof(IsTaskManagerDialogOpen));
        };

        _logger.LogInformation("MainViewModel 初始化完成");
    }

    // 视图模式切换命令
    [RelayCommand]
    private void ToggleViewMode()
    {
        if (IsGridView)
        {
            IsGridView = false;
            IsListView = true;
            IsTimelineView = false;
        }
        else if (IsListView)
        {
            IsGridView = false;
            IsListView = false;
            IsTimelineView = true;
        }
        else
        {
            IsGridView = true;
            IsListView = false;
            IsTimelineView = false;
        }
    }

    [RelayCommand]
    private void SetGridView()
    {
        IsGridView = true;
        IsListView = false;
        IsTimelineView = false;
    }

    [RelayCommand]
    private void SetListView()
    {
        IsGridView = false;
        IsListView = true;
        IsTimelineView = false;
    }

    [RelayCommand]
    private void SetTimelineView()
    {
        IsGridView = false;
        IsListView = false;
        IsTimelineView = true;
    }

    // 对话框命令
    [RelayCommand]
    private void ToggleProviderSettings()
    {
        IsProviderSettingsDialogOpen = !IsProviderSettingsDialogOpen;
    }

    [RelayCommand]
    private void ShowProviderSettings()
    {
        IsProviderSettingsDialogOpen = true;
    }

    [RelayCommand]
    private void ShowTextToShotDialog()
    {
        IsTextToShotDialogOpen = true;
    }

    // 文件查看和文件夹打开命令
    [RelayCommand]
    private void ViewFirstFrameImage(ShotItem? shot)
    {
        if (shot?.FirstFrameImagePath != null && System.IO.File.Exists(shot.FirstFrameImagePath))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = shot.FirstFrameImagePath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "打开首帧图片失败: {Path}", shot.FirstFrameImagePath);
            }
        }
    }

    [RelayCommand]
    private void OpenFirstFrameFolder(ShotItem? shot)
    {
        if (shot?.FirstFrameImagePath != null && System.IO.File.Exists(shot.FirstFrameImagePath))
        {
            try
            {
                var folderPath = System.IO.Path.GetDirectoryName(shot.FirstFrameImagePath);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "打开首帧文件夹失败: {Path}", shot.FirstFrameImagePath);
            }
        }
    }

    [RelayCommand]
    private void ViewLastFrameImage(ShotItem? shot)
    {
        if (shot?.LastFrameImagePath != null && System.IO.File.Exists(shot.LastFrameImagePath))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = shot.LastFrameImagePath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "打开尾帧图片失败: {Path}", shot.LastFrameImagePath);
            }
        }
    }

    [RelayCommand]
    private void OpenLastFrameFolder(ShotItem? shot)
    {
        if (shot?.LastFrameImagePath != null && System.IO.File.Exists(shot.LastFrameImagePath))
        {
            try
            {
                var folderPath = System.IO.Path.GetDirectoryName(shot.LastFrameImagePath);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "打开尾帧文件夹失败: {Path}", shot.LastFrameImagePath);
            }
        }
    }

    [RelayCommand]
    private void OpenVideoFolder(ShotItem? shot)
    {
        if (shot?.GeneratedVideoPath != null && System.IO.File.Exists(shot.GeneratedVideoPath))
        {
            try
            {
                var folderPath = System.IO.Path.GetDirectoryName(shot.GeneratedVideoPath);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "打开视频文件夹失败: {Path}", shot.GeneratedVideoPath);
            }
        }
    }

    // 额外的委托方法
    public void RenumberShotsForDrag() => ShotList.RenumberShotsForDrag();

    public System.Threading.Tasks.Task GenerateShotsFromTextPromptAsync()
    {
        // TODO: 实现文本生成分镜功能
        return System.Threading.Tasks.Task.CompletedTask;
    }
}

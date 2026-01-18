using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Application.Services;
using Storyboard.Messages;
using Storyboard.Models;
using Storyboard.ViewModels.Project;
using Storyboard.ViewModels.Queue;
using Storyboard.ViewModels.Import;
using Storyboard.ViewModels.Shot;
using Storyboard.ViewModels.Generation;
using Storyboard.ViewModels.Shared;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels;

/// <summary>
/// 主 ViewModel - 作为主协调器，管理所有子 ViewModel
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IMessenger _messenger;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IProjectStore _projectStore;

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

    // 创作意图属性
    [ObservableProperty]
    private string? _creativeGoal;

    [ObservableProperty]
    private string? _targetAudience;

    [ObservableProperty]
    private string? _videoTone;

    [ObservableProperty]
    private string? _keyMessage;

    // 创作模式属性
    [ObservableProperty]
    private string _creationMode = "Text"; // "Text" or "Video"

    public bool IsTextMode => CreationMode == "Text";
    public bool IsVideoMode => CreationMode == "Video";

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
    public string? ImportErrorMessage => VideoImport.ImportErrorMessage;

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
        IProjectStore projectStore,
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
        _projectStore = projectStore;
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
            if (e.PropertyName == nameof(ProjectManagement.IsNewProjectDialogOpen))
                OnPropertyChanged(nameof(IsNewProjectDialogOpen));
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

        VideoImport.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(VideoImport.HasVideoFile))
                OnPropertyChanged(nameof(HasVideoFile));
            if (e.PropertyName == nameof(VideoImport.VideoFileDuration))
                OnPropertyChanged(nameof(VideoFileDuration));
            if (e.PropertyName == nameof(VideoImport.VideoFileResolution))
                OnPropertyChanged(nameof(VideoFileResolution));
            if (e.PropertyName == nameof(VideoImport.VideoFileFps))
                OnPropertyChanged(nameof(VideoFileFps));
            if (e.PropertyName == nameof(VideoImport.ImportErrorMessage))
                OnPropertyChanged(nameof(ImportErrorMessage));
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

        // 订阅需要自动保存的消息
        _messenger.Register<VideoImportedMessage>(this, (r, m) => _ = SaveProjectAsync());
        _messenger.Register<FramesExtractedMessage>(this, OnFramesExtracted);
        _messenger.Register<ShotAddedMessage>(this, (r, m) => _ = SaveProjectAsync());
        _messenger.Register<ShotDeletedMessage>(this, (r, m) => _ = SaveProjectAsync());
        _messenger.Register<ShotUpdatedMessage>(this, (r, m) => _ = SaveProjectAsync());
        _messenger.Register<ImageGenerationCompletedMessage>(this, (r, m) => _ = SaveProjectAsync());
        _messenger.Register<VideoGenerationCompletedMessage>(this, (r, m) => _ = SaveProjectAsync());

        // 订阅项目数据加载消息，加载创作意图
        _messenger.Register<ProjectDataLoadedMessage>(this, OnProjectDataLoaded);
        _messenger.Register<ProjectClosedMessage>(this, OnProjectClosed);

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

    // 切换创作模式命令
    [RelayCommand]
    private void SelectCreationMode(string mode)
    {
        if (mode == "Text" || mode == "Video")
        {
            CreationMode = mode;
            OnPropertyChanged(nameof(IsTextMode));
            OnPropertyChanged(nameof(IsVideoMode));
            _logger.LogInformation("切换创作模式: {Mode}", mode);
        }
    }

    // 保存创作意图命令
    [RelayCommand]
    private async Task SaveCreativeIntent()
    {
        if (string.IsNullOrWhiteSpace(CurrentProjectId))
        {
            _logger.LogWarning("无法保存创作意图：项目 ID 为空");
            StatusMessage = "保存失败：没有打开的项目";
            return;
        }

        try
        {
            _logger.LogInformation("保存创作意图: ProjectId={ProjectId}", CurrentProjectId);
            StatusMessage = "正在保存创作意图...";

            // 保存项目（会自动包含创作意图）
            await SaveProjectAsync();

            _logger.LogInformation("创作意图已保存");
            StatusMessage = "创作意图已保存";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存创作意图失败: {ProjectId}", CurrentProjectId);
            StatusMessage = $"保存失败：{ex.Message}";
        }
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

    public async System.Threading.Tasks.Task GenerateShotsFromTextPromptAsync()
    {
        if (string.IsNullOrWhiteSpace(TextToShotPrompt))
        {
            _logger.LogWarning("文本生成分镜：提示词为空");
            return;
        }

        if (!HasCurrentProject)
        {
            _logger.LogWarning("文本生成分镜：没有打开的项目");
            return;
        }

        try
        {
            _logger.LogInformation("开始文本生成分镜：{Prompt}", TextToShotPrompt);
            StatusMessage = "正在生成分镜...";

            // 调用 AI 服务生成分镜，传入创作意图
            var generatedShots = await AiAnalysis.GenerateShotsFromTextAsync(
                TextToShotPrompt,
                CreativeGoal,
                TargetAudience,
                VideoTone,
                KeyMessage);

            if (generatedShots == null || generatedShots.Count == 0)
            {
                _logger.LogWarning("文本生成分镜：未生成任何分镜");
                StatusMessage = "生成失败：未生成任何分镜";
                return;
            }

            // 将生成的分镜添加到列表
            var startNumber = Shots.Count > 0 ? Shots.Max(s => s.ShotNumber) + 1 : 1;
            foreach (var shotDesc in generatedShots)
            {
                var shot = new ShotItem(startNumber++);

                // 使用批量更新方法应用所有 AI 解析结果
                shot.ApplyAiAnalysisResult(shotDesc);

                ShotList.AddShot(shot);
            }

            _logger.LogInformation("文本生成分镜完成：生成了 {Count} 个分镜", generatedShots.Count);
            StatusMessage = $"成功生成 {generatedShots.Count} 个分镜";

            // 保存项目
            await SaveProjectAsync();

            // 清空提示词
            TextToShotPrompt = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文本生成分镜失败");
            StatusMessage = $"生成失败：{ex.Message}";
        }
    }

    // 自动保存项目
    private async void OnFramesExtracted(object recipient, FramesExtractedMessage message)
    {
        await SaveProjectAsync();
    }

    // 加载项目数据时恢复创作意图
    private void OnProjectDataLoaded(object recipient, ProjectDataLoadedMessage message)
    {
        var state = message.ProjectState;

        CreativeGoal = state.CreativeGoal;
        TargetAudience = state.TargetAudience;
        VideoTone = state.VideoTone;
        KeyMessage = state.KeyMessage;

        _logger.LogInformation("创作意图已加载: Goal={Goal}, Audience={Audience}",
            CreativeGoal, TargetAudience);
    }

    // 关闭项目时清空创作意图
    private void OnProjectClosed(object recipient, ProjectClosedMessage message)
    {
        CreativeGoal = null;
        TargetAudience = null;
        VideoTone = null;
        KeyMessage = null;

        _logger.LogInformation("创作意图已清空");
    }

    private async Task SaveProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentProjectId))
        {
            _logger.LogWarning("无法保存项目：项目 ID 为空");
            return;
        }

        try
        {
            var projectState = BuildProjectState();
            await _projectStore.SaveAsync(projectState);
            _logger.LogInformation("项目已自动保存: {ProjectId}", CurrentProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存项目失败: {ProjectId}", CurrentProjectId);
        }
    }

    private ProjectState BuildProjectState()
    {
        _logger.LogInformation("构建项目状态: HasVideoFile={HasVideoFile}, VideoPath={VideoPath}",
            VideoImport.HasVideoFile, VideoImport.SelectedVideoPath);

        var shotStates = ShotList.Shots.Select(shot => new ShotState(
            shot.ShotNumber,
            shot.Duration,
            shot.StartTime,
            shot.EndTime,
            shot.FirstFramePrompt,
            shot.LastFramePrompt,
            shot.ShotType,
            shot.CoreContent,
            shot.ActionCommand,
            shot.SceneSettings,
            shot.SelectedModel,
            shot.FirstFrameImagePath,
            shot.LastFrameImagePath,
            shot.GeneratedVideoPath,
            shot.MaterialThumbnailPath,
            shot.MaterialFilePath,
            shot.FirstFrameAssets.Concat(shot.LastFrameAssets).Concat(shot.VideoAssets).Select(asset => new ShotAssetState(
                asset.Type,
                asset.FilePath,
                asset.ThumbnailPath,
                asset.VideoThumbnailPath,
                asset.Prompt,
                asset.Model,
                asset.CreatedAt
            )).ToList(),
            shot.MaterialResolution,
            shot.MaterialFileSize,
            shot.MaterialFormat,
            shot.MaterialColorTone,
            shot.MaterialBrightness,
            shot.ImageSize,
            shot.NegativePrompt,
            shot.AspectRatio,
            shot.LightingType,
            shot.TimeOfDay,
            shot.Composition,
            shot.ColorStyle,
            shot.LensType,
            // First frame professional parameters
            shot.FirstFrameComposition,
            shot.FirstFrameLightingType,
            shot.FirstFrameTimeOfDay,
            shot.FirstFrameColorStyle,
            shot.FirstFrameLensType,
            shot.FirstFrameNegativePrompt,
            shot.FirstFrameImageSize,
            shot.FirstFrameAspectRatio,
            shot.FirstFrameSelectedModel,
            shot.FirstFrameSeed,
            // Last frame professional parameters
            shot.LastFrameComposition,
            shot.LastFrameLightingType,
            shot.LastFrameTimeOfDay,
            shot.LastFrameColorStyle,
            shot.LastFrameLensType,
            shot.LastFrameNegativePrompt,
            shot.LastFrameImageSize,
            shot.LastFrameAspectRatio,
            shot.LastFrameSelectedModel,
            shot.LastFrameSeed,
            shot.VideoPrompt,
            shot.SceneDescription,
            shot.ActionDescription,
            shot.StyleDescription,
            shot.VideoNegativePrompt,
            shot.CameraMovement,
            shot.ShootingStyle,
            shot.VideoEffect,
            shot.VideoResolution,
            shot.VideoRatio,
            shot.VideoFrames,
            shot.UseFirstFrameReference,
            shot.UseLastFrameReference,
            shot.Seed,
            shot.CameraFixed,
            shot.Watermark
        )).ToList();

        return new ProjectState(
            CurrentProjectId!,
            ProjectName,
            VideoImport.SelectedVideoPath,
            !string.IsNullOrWhiteSpace(VideoImport.SelectedVideoPath), // 如果有视频路径，就设置为 true
            VideoImport.VideoFileDuration,
            VideoImport.VideoFileResolution,
            VideoImport.VideoFileFps,
            FrameExtraction.ExtractModeIndex,
            FrameExtraction.FrameCount,
            FrameExtraction.TimeInterval,
            FrameExtraction.DetectionSensitivity,
            shotStates,
            // 创作意图
            CreativeGoal,
            TargetAudience,
            VideoTone,
            KeyMessage
        );
    }
}

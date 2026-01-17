using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Messages;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels.Import;

/// <summary>
/// 视频导入 ViewModel - 负责视频文件选择和元数据显示
/// </summary>
public partial class VideoImportViewModel : ObservableObject
{
    private readonly IVideoMetadataService _videoMetadataService;
    private readonly IMessenger _messenger;
    private readonly ILogger<VideoImportViewModel> _logger;

    [ObservableProperty]
    private string? _selectedVideoPath;

    [ObservableProperty]
    private bool _hasVideoFile;

    [ObservableProperty]
    private string _videoFileDuration = "--:--";

    [ObservableProperty]
    private string _videoFileResolution = "-- x --";

    [ObservableProperty]
    private string _videoFileFps = "--";

    [ObservableProperty]
    private string? _importErrorMessage;

    public VideoImportViewModel(
        IVideoMetadataService videoMetadataService,
        IMessenger messenger,
        ILogger<VideoImportViewModel> logger)
    {
        _videoMetadataService = videoMetadataService;
        _messenger = messenger;
        _logger = logger;

        // 订阅项目打开/关闭消息
        _messenger.Register<ProjectCreatedMessage>(this, OnProjectCreated);
        _messenger.Register<ProjectOpenedMessage>(this, OnProjectOpened);
        _messenger.Register<ProjectDataLoadedMessage>(this, OnProjectDataLoaded);
        _messenger.Register<ProjectClosedMessage>(this, OnProjectClosed);
    }

    [RelayCommand]
    private async Task ImportVideo()
    {
        _logger.LogInformation("ImportVideo command called");

        // 清空之前的错误消息
        ImportErrorMessage = null;

        var path = await PickVideoPathAsync();

        _logger.LogInformation("Selected video path: {Path}", path ?? "null");

        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogWarning("No video file selected");
            return;
        }

        try
        {
            var metadata = await _videoMetadataService.GetMetadataAsync(path);
            if (metadata != null)
            {
                SelectedVideoPath = path;
                HasVideoFile = true;
                VideoFileDuration = FormatDuration(metadata.DurationSeconds);
                VideoFileResolution = $"{metadata.Width} x {metadata.Height}";
                VideoFileFps = $"{metadata.Fps:F1}";

                // 发送视频导入消息
                _messenger.Send(new VideoImportedMessage(path));

                _logger.LogInformation("视频导入成功: {Path}, 时长: {Duration}, 分辨率: {Resolution}",
                    path, VideoFileDuration, VideoFileResolution);
            }
            else
            {
                ImportErrorMessage = "无法读取视频元数据";
                _logger.LogWarning("视频元数据为空: {Path}", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取视频元数据失败: {Path}", path);
            HasVideoFile = false;
            VideoFileDuration = "--:--";
            VideoFileResolution = "-- x --";
            VideoFileFps = "--";

            // 设置用户友好的错误消息
            if (ex.Message.Contains("ffprobe") || ex.Message.Contains("ffmpeg"))
            {
                ImportErrorMessage = "视频导入失败：未找到 ffmpeg/ffprobe 工具。请确保已安装 ffmpeg 并加入系统 PATH。";
            }
            else if (ex is FileNotFoundException)
            {
                ImportErrorMessage = "视频文件不存在";
            }
            else
            {
                ImportErrorMessage = $"视频导入失败：{ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task UploadVideo()
    {
        await ImportVideo();
    }

    private async Task<string?> PickVideoPathAsync()
    {
        _logger.LogInformation("PickVideoPathAsync called");

        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            _logger.LogWarning("ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime");
            return null;
        }

        var mainWindow = desktop.MainWindow;
        if (mainWindow == null)
        {
            _logger.LogWarning("MainWindow is null");
            return null;
        }

        _logger.LogInformation("Opening file picker dialog");

        var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择视频文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("视频文件")
                {
                    Patterns = new[] { "*.mp4", "*.avi", "*.mov", "*.mkv", "*.flv", "*.wmv" }
                },
                new FilePickerFileType("所有文件")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        var selectedPath = files?.FirstOrDefault()?.Path.LocalPath;
        _logger.LogInformation("File picker returned: {Path}", selectedPath ?? "null");

        return selectedPath;
    }

    private static string FormatDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private void OnProjectCreated(object recipient, ProjectCreatedMessage message)
    {
        // 项目创建时，清空视频数据，准备导入新视频
        SelectedVideoPath = null;
        HasVideoFile = false;
        VideoFileDuration = "--:--";
        VideoFileResolution = "-- x --";
        VideoFileFps = "--";

        _logger.LogInformation("项目创建完成，准备导入视频: {ProjectId}", message.ProjectId);
    }

    private void OnProjectOpened(object recipient, ProjectOpenedMessage message)
    {
        // 项目打开时，视频路径会由项目数据加载
        // 这里暂时不做处理，等待完整的项目加载逻辑
    }

    private void OnProjectDataLoaded(object recipient, ProjectDataLoadedMessage message)
    {
        var state = message.ProjectState;

        SelectedVideoPath = state.SelectedVideoPath;
        // 修正：如果有视频路径但 HasVideoFile 是 false，自动修正为 true
        HasVideoFile = !string.IsNullOrWhiteSpace(state.SelectedVideoPath);
        VideoFileDuration = state.VideoFileDuration;
        VideoFileResolution = state.VideoFileResolution;
        VideoFileFps = state.VideoFileFps;

        _logger.LogInformation("项目视频数据加载完成: {HasVideo}", HasVideoFile);
    }

    private void OnProjectClosed(object recipient, ProjectClosedMessage message)
    {
        SelectedVideoPath = null;
        HasVideoFile = false;
        VideoFileDuration = "--:--";
        VideoFileResolution = "-- x --";
        VideoFileFps = "--";
    }
}

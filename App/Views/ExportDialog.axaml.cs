using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Storyboard.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Storyboard.Views;

public partial class ExportDialog : Window
{
    public ExportDialog()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnExportJsonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var storageProvider = StorageProvider;
        
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出项目",
            SuggestedFileName = $"{viewModel.ProjectName}_分镜.json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON 文件") { Patterns = new[] { "*.json" } }
            }
        });

        if (file != null)
        {
            var exportData = new
            {
                projectName = viewModel.ProjectName,
                exportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                shots = viewModel.Shots.Select(shot => new
                {
                    shotNumber = shot.ShotNumber,
                    duration = shot.Duration,
                    shotType = shot.ShotType,
                    coreContent = shot.CoreContent,
                    actionCommand = shot.ActionCommand,
                    sceneSettings = shot.SceneSettings,
                    firstFramePrompt = shot.FirstFramePrompt,
                    lastFramePrompt = shot.LastFramePrompt,
                    videoPrompt = shot.VideoPrompt,

                    // 当前使用的资源路径
                    currentFirstFrameImagePath = shot.FirstFrameImagePath,
                    currentLastFrameImagePath = shot.LastFrameImagePath,
                    currentGeneratedVideoPath = shot.GeneratedVideoPath,

                    // 历史生成的所有资源
                    firstFrameAssets = shot.FirstFrameAssets.Select(asset => new
                    {
                        filePath = asset.FilePath,
                        createdAt = asset.CreatedAt,
                        isSelected = asset.IsSelected
                    }).ToArray(),
                    lastFrameAssets = shot.LastFrameAssets.Select(asset => new
                    {
                        filePath = asset.FilePath,
                        createdAt = asset.CreatedAt,
                        isSelected = asset.IsSelected
                    }).ToArray(),
                    videoAssets = shot.VideoAssets.Select(asset => new
                    {
                        filePath = asset.FilePath,
                        createdAt = asset.CreatedAt,
                        isSelected = asset.IsSelected
                    }).ToArray(),

                    // 状态标记
                    hasFirstFrame = !string.IsNullOrEmpty(shot.FirstFrameImagePath),
                    hasLastFrame = !string.IsNullOrEmpty(shot.LastFrameImagePath),
                    hasVideo = !string.IsNullOrEmpty(shot.VideoOutputPath)
                }).ToArray(),
                statistics = new
                {
                    totalShots = viewModel.Shots.Count,
                    totalDuration = viewModel.TotalDuration,
                    completedShots = viewModel.CompletedShotsCount,
                    completedVideoShots = viewModel.CompletedVideoShotsCount
                }
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
            
            Close();
        }
    }

    private async void OnExportVideoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (!viewModel.CanExportVideo)
            return;

        var storageProvider = StorageProvider;
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出视频",
            SuggestedFileName = $"{viewModel.ProjectName}_成片.mp4",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("MP4 文件") { Patterns = new[] { "*.mp4" } }
            }
        });

        if (file == null)
            return;

        await viewModel.ExportVideoAsync(file.Path.LocalPath);
        Close();
    }
}

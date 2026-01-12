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
                    hasFirstFrame = !string.IsNullOrEmpty(shot.FirstFrameImagePath),
                    hasLastFrame = !string.IsNullOrEmpty(shot.LastFrameImagePath),
                    hasVideo = !string.IsNullOrEmpty(shot.VideoOutputPath)
                }).ToArray(),
                statistics = new
                {
                    totalShots = viewModel.Shots.Count,
                    totalDuration = viewModel.TotalDuration,
                    completedShots = viewModel.CompletedShotsCount
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


using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Storyboard.Models;
using Storyboard.Services;
using Storyboard.Views.Windows;

namespace Storyboard.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IVideoAnalysisService _videoAnalysisService;
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IVideoGenerationService _videoGenerationService;

    [ObservableProperty]
    private string? _selectedVideoPath;

    [ObservableProperty]
    private ObservableCollection<ShotItem> _shots = new();

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private double _totalDuration;

    [ObservableProperty]
    private int _completedShotsCount;

    [ObservableProperty]
    private int _shotsRenderingCount;

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
            var owner = Application.Current?.MainWindow;
            var previewWindow = new ImagePreviewWindow(previewPath)
            {
                Owner = owner
            };
            previewWindow.ShowDialog();
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

        try
        {
            var owner = Application.Current?.MainWindow;
            var previewWindow = new ImagePreviewWindow(shot.FirstFrameImagePath)
            {
                Owner = owner
            };
            previewWindow.ShowDialog();
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
            var owner = Application.Current?.MainWindow;
            var previewWindow = new ImagePreviewWindow(shot.LastFrameImagePath)
            {
                Owner = owner
            };
            previewWindow.ShowDialog();
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

    public MainViewModel(
        IVideoAnalysisService videoAnalysisService,
        IImageGenerationService imageGenerationService,
        IVideoGenerationService videoGenerationService)
    {
        _videoAnalysisService = videoAnalysisService;
        _imageGenerationService = imageGenerationService;
        _videoGenerationService = videoGenerationService;

        Shots.CollectionChanged += Shots_CollectionChanged;
        UpdateSummaryCounts();
        
        // 添加测试数据
        LoadTestData();
    }
    
    private void LoadTestData()
    {
        Shots.Add(new ShotItem(1)
        {
            Duration = 3.5,
            FirstFramePrompt = "清晨的城市街道，阳光透过高楼洒下",
            LastFramePrompt = "镜头缓缓推进到咖啡店门口",
            ShotType = "推镜",
            CoreContent = "城市街景",
            ActionCommand = "缓慢推进",
            SceneSettings = "早晨，暖色调，自然光",
            SelectedModel = "RunwayGen3"
        });
        
        Shots.Add(new ShotItem(2)
        {
            Duration = 2,
            FirstFramePrompt = "咖啡师正在制作拿铁，特写镜头",
            LastFramePrompt = "拉花完成，呈现美图案",
            ShotType = "特写",
            CoreContent = "咖啡制作",
            ActionCommand = "稳定拍摄",
            SceneSettings = "室内，暖光，浅景深",
            SelectedModel = "Pika"
        });
        
        Shots.Add(new ShotItem(3)
        {
            Duration = 4,
            FirstFramePrompt = "顾客坐在窗边，手握咖啡杯",
            LastFramePrompt = "望向窗外，露出微笑",
            ShotType = "中景",
            CoreContent = "人物场景",
            ActionCommand = "自然转头",
            SceneSettings = "窗边，逆光，柔和",
            SelectedModel = "RunwayGen3"
        });
    }

    [RelayCommand]
    private async Task UploadVideoAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "视频文件|*.mp4;*.avi;*.mov;*.mkv|所有文件|*.*",
            Title = "选择视频文件"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            SelectedVideoPath = openFileDialog.FileName;
            await AnalyzeVideoAsync();
        }
    }

    private void RenumberShots()
    {
        for (int i = 0; i < Shots.Count; i++)
        {
            Shots[i].ShotNumber = i + 1;
        }
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
            MessageBox.Show($"视频分析失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    private async Task GenerateFirstFrameAsync(ShotItem shot)
    {
        if (string.IsNullOrEmpty(shot.FirstFramePrompt))
        {
            MessageBox.Show("请先输入首帧提示词", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            shot.IsFirstFrameGenerating = true;
            var imagePath = await _imageGenerationService.GenerateImageAsync(shot.FirstFramePrompt, shot.SelectedModel);
            shot.FirstFrameImagePath = imagePath;
            GeneratedImagesCount++;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"首帧生成失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            shot.IsFirstFrameGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateLastFrameAsync(ShotItem shot)
    {
        if (string.IsNullOrEmpty(shot.LastFramePrompt))
        {
            MessageBox.Show("请先输入尾帧提示词", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            shot.IsLastFrameGenerating = true;
            var imagePath = await _imageGenerationService.GenerateImageAsync(shot.LastFramePrompt, shot.SelectedModel);
            shot.LastFrameImagePath = imagePath;
            GeneratedImagesCount++;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"尾帧生成失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            shot.IsLastFrameGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateVideoAsync(ShotItem shot)
    {
        if (string.IsNullOrEmpty(shot.FirstFrameImagePath) || string.IsNullOrEmpty(shot.LastFrameImagePath))
        {
            MessageBox.Show("请先生成首帧和尾帧图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            shot.IsVideoGenerating = true;
            var videoPath = await _videoGenerationService.GenerateVideoAsync(shot);
            shot.GeneratedVideoPath = videoPath;
            GeneratedVideosCount++;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"视频生成失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            shot.IsVideoGenerating = false;
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

        UpdateSummaryCounts();
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
        if (e.PropertyName is nameof(ShotItem.FirstFrameImagePath)
            or nameof(ShotItem.IsFirstFrameGenerating)
            or nameof(ShotItem.IsLastFrameGenerating)
            or nameof(ShotItem.IsVideoGenerating))
        {
            UpdateSummaryCounts();
        }
    }

    private void UpdateSummaryCounts()
    {
        CompletedShotsCount = Shots.Count(shot => !string.IsNullOrWhiteSpace(shot.FirstFrameImagePath));
        ShotsRenderingCount = Shots.Count(shot => shot.IsFirstFrameGenerating || shot.IsLastFrameGenerating || shot.IsVideoGenerating);
    }
}


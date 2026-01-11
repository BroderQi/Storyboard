
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
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Storyboard.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IVideoAnalysisService _videoAnalysisService;
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IVideoGenerationService _videoGenerationService;
    private readonly IFinalRenderService _finalRenderService;
    private readonly JobQueueService _jobQueue;

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
        IVideoGenerationService videoGenerationService,
        IFinalRenderService finalRenderService,
        JobQueueService jobQueue)
    {
        _videoAnalysisService = videoAnalysisService;
        _imageGenerationService = imageGenerationService;
        _videoGenerationService = videoGenerationService;
        _finalRenderService = finalRenderService;
        _jobQueue = jobQueue;

        Shots.CollectionChanged += Shots_CollectionChanged;
        UpdateSummaryCounts();
    }

    public ObservableCollection<GenerationJob> JobHistory => _jobQueue.Jobs;

    [RelayCommand]
    private void CancelJob(GenerationJob? job)
    {
        if (job == null)
            return;

        _jobQueue.Cancel(job);
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

    [RelayCommand]
    private void AddShot()
    {
        Shots.Add(new ShotItem(Shots.Count + 1));
        RenumberShots();
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
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            action();
            return;
        }

        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
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


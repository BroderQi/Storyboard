using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Storyboard.Models;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Storyboard.ViewModels;

public enum BatchOperationKind
{
    Parse,
    ImageFirst,
    ImageLast,
    Video
}

public enum BatchTaskStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

public partial class BatchTaskViewModel : ObservableObject
{
    public int ShotNumber { get; }
    public BatchOperationKind Operation { get; }

    [ObservableProperty]
    private BatchTaskStatus status = BatchTaskStatus.Pending;

    [ObservableProperty]
    private int progress;

    public BatchTaskViewModel(int shotNumber, BatchOperationKind operation)
    {
        ShotNumber = shotNumber;
        Operation = operation;
    }

    public string OperationName => Operation switch
    {
        BatchOperationKind.Parse => "AI 解析",
        BatchOperationKind.ImageFirst => "生成首帧",
        BatchOperationKind.ImageLast => "生成尾帧",
        BatchOperationKind.Video => "生成视频",
        _ => "未知"
    };

    public string Title => $"分镜 #{ShotNumber} - {OperationName}";

    public string StatusText => Status switch
    {
        BatchTaskStatus.Pending => "等待",
        BatchTaskStatus.Running => "执行中",
        BatchTaskStatus.Completed => "完成",
        BatchTaskStatus.Failed => "失败",
        _ => "未知"
    };

    public bool IsRunning => Status == BatchTaskStatus.Running;
}

public partial class BatchOperationsViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    public ObservableCollection<ShotItem> Shots { get; }

    public ObservableCollection<BatchTaskViewModel> Tasks { get; } = new();

    [ObservableProperty]
    private bool parse = true;

    [ObservableProperty]
    private bool imageFirst = true;

    [ObservableProperty]
    private bool imageLast = true;

    [ObservableProperty]
    private bool video;

    [ObservableProperty]
    private bool isRunning;

    public BatchOperationsViewModel(ObservableCollection<ShotItem> shots)
    {
        Shots = shots;

        Shots.CollectionChanged += Shots_CollectionChanged;
        foreach (var shot in Shots)
        {
            shot.PropertyChanged += Shot_PropertyChanged;
        }
    }

    public event EventHandler? RequestClose;

    public int SelectedShotsCount => Shots.Count(s => s.IsChecked);
    public bool HasSelectedShots => SelectedShotsCount > 0;
    public string SelectedShotsCountText => $"{SelectedShotsCount} / {Shots.Count}";

    public int CompletedTasksCount => Tasks.Count(t => t.Status == BatchTaskStatus.Completed);
    public int TotalTasksCount => Tasks.Count;

    public bool HasTasks => TotalTasksCount > 0;

    public double OverallProgressPercent => TotalTasksCount == 0
        ? 0
        : (double)CompletedTasksCount / TotalTasksCount * 100;

    public string OverallProgressText => TotalTasksCount == 0
        ? ""
        : $"总体进度: {Math.Round(OverallProgressPercent)}%";

    public string CompletedTasksText => TotalTasksCount == 0
        ? ""
        : $"{CompletedTasksCount} / {TotalTasksCount}";

    public bool CanStart => !IsRunning && HasSelectedShots;

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var shot in Shots)
        {
            shot.IsChecked = true;
        }
        RaiseSelectionDependent();
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var shot in Shots)
        {
            shot.IsChecked = false;
        }
        RaiseSelectionDependent();
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (!CanStart)
            return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsRunning = true;
        try
        {
            Tasks.Clear();

            var selectedShots = Shots.Where(s => s.IsChecked).ToList();
            foreach (var shot in selectedShots)
            {
                if (Parse)
                    Tasks.Add(new BatchTaskViewModel(shot.ShotNumber, BatchOperationKind.Parse));
                if (ImageFirst)
                    Tasks.Add(new BatchTaskViewModel(shot.ShotNumber, BatchOperationKind.ImageFirst));
                if (ImageLast)
                    Tasks.Add(new BatchTaskViewModel(shot.ShotNumber, BatchOperationKind.ImageLast));
                if (Video && shot.CanGenerateVideo)
                    Tasks.Add(new BatchTaskViewModel(shot.ShotNumber, BatchOperationKind.Video));
            }

            RaiseTaskDependent();

            for (var i = 0; i < Tasks.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                var task = Tasks[i];
                task.Status = BatchTaskStatus.Running;
                task.Progress = 0;

                for (var progress = 0; progress <= 100; progress += 20)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(200, token);
                    task.Progress = progress;
                }

                task.Status = BatchTaskStatus.Completed;
                task.Progress = 100;
                RaiseTaskDependent();
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        finally
        {
            IsRunning = false;
            RaiseSelectionDependent();
        }
    }

    private void Shots_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<ShotItem>())
            {
                item.PropertyChanged -= Shot_PropertyChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<ShotItem>())
            {
                item.PropertyChanged += Shot_PropertyChanged;
            }
        }

        RaiseSelectionDependent();
    }

    private void Shot_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShotItem.IsChecked))
        {
            RaiseSelectionDependent();
        }
    }

    private void RaiseSelectionDependent()
    {
        OnPropertyChanged(nameof(SelectedShotsCount));
        OnPropertyChanged(nameof(HasSelectedShots));
        OnPropertyChanged(nameof(SelectedShotsCountText));
        OnPropertyChanged(nameof(CanStart));
    }

    private void RaiseTaskDependent()
    {
        OnPropertyChanged(nameof(CompletedTasksCount));
        OnPropertyChanged(nameof(TotalTasksCount));
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(OverallProgressPercent));
        OnPropertyChanged(nameof(OverallProgressText));
        OnPropertyChanged(nameof(CompletedTasksText));
    }
}

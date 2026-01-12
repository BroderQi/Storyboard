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

public partial class BatchTaskViewModel : ObservableObject
{
    public GenerationJob Job { get; }
    public BatchOperationKind Operation { get; }

    public BatchTaskViewModel(GenerationJob job, BatchOperationKind operation)
    {
        Job = job;
        Operation = operation;
        Job.PropertyChanged += Job_PropertyChanged;
    }

    public int ShotNumber => Job.ShotNumber ?? 0;

    public string OperationName => Operation switch
    {
        BatchOperationKind.Parse => "AI 解析",
        BatchOperationKind.ImageFirst => "生成首帧",
        BatchOperationKind.ImageLast => "生成尾帧",
        BatchOperationKind.Video => "生成视频",
        _ => "未知"
    };

    public string Title => $"分镜 #{ShotNumber} - {OperationName}";

    public string StatusText => Job.StatusText;

    public bool IsRunning => Job.Status is GenerationJobStatus.Queued or GenerationJobStatus.Running or GenerationJobStatus.Retrying;
    public bool IsCompleted => Job.Status is GenerationJobStatus.Succeeded;
    public bool IsFailed => Job.Status is GenerationJobStatus.Failed or GenerationJobStatus.Canceled;
    public int Progress => (int)Math.Round(Job.Progress * 100);

    private void Job_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GenerationJob.Status)
            or nameof(GenerationJob.Progress))
        {
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(IsFailed));
            OnPropertyChanged(nameof(Progress));
        }
    }
}

public partial class BatchOperationsViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;

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

    partial void OnParseChanged(bool value) => RaiseSelectionDependent();
    partial void OnImageFirstChanged(bool value) => RaiseSelectionDependent();
    partial void OnImageLastChanged(bool value) => RaiseSelectionDependent();
    partial void OnVideoChanged(bool value) => RaiseSelectionDependent();

    [ObservableProperty]
    private bool isRunning;

    public BatchOperationsViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        Shots = mainViewModel.Shots;

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

    public int CompletedTasksCount => Tasks.Count(t => t.IsCompleted);
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

    public bool HasOperationsSelected => Parse || ImageFirst || ImageLast || Video;
    public bool CanStart => !IsRunning && HasSelectedShots && HasOperationsSelected;

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
        foreach (var task in Tasks)
        {
            if (task.Job.CanCancel)
                _mainViewModel.CancelJobCommand.Execute(task.Job);
        }
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (!CanStart)
            return;

        IsRunning = true;
        try
        {
            Tasks.Clear();

            var selectedShots = Shots.Where(s => s.IsChecked).ToList();
            if (!Parse && !ImageFirst && !ImageLast && !Video)
            {
                IsRunning = false;
                RaiseSelectionDependent();
                return;
            }
            AiWriteMode? writeMode = null;
            if (Parse && selectedShots.Any(_mainViewModel.NeedsAiWriteModeForBatch))
            {
                writeMode = await _mainViewModel.PromptAiWriteModeAsync();
                if (writeMode == null)
                {
                    IsRunning = false;
                    RaiseSelectionDependent();
                    return;
                }
            }

            foreach (var shot in selectedShots)
            {
                if (Parse)
                {
                    var job = _mainViewModel.QueueAiParse(shot, writeMode);
                    Tasks.Add(new BatchTaskViewModel(job, BatchOperationKind.Parse));
                }
                if (ImageFirst)
                {
                    var job = _mainViewModel.QueueFirstFrame(shot);
                    Tasks.Add(new BatchTaskViewModel(job, BatchOperationKind.ImageFirst));
                }
                if (ImageLast)
                {
                    var job = _mainViewModel.QueueLastFrame(shot);
                    Tasks.Add(new BatchTaskViewModel(job, BatchOperationKind.ImageLast));
                }
                if (Video && shot.CanGenerateVideo)
                {
                    var job = _mainViewModel.QueueVideo(shot);
                    Tasks.Add(new BatchTaskViewModel(job, BatchOperationKind.Video));
                }
            }

            foreach (var task in Tasks)
                task.PropertyChanged += (_, __) => RaiseTaskDependent();

            RaiseTaskDependent();

            // Stay running until all tasks are finished.
            _ = Task.Run(async () =>
            {
                while (Tasks.Any(t => t.IsRunning))
                {
                    await Task.Delay(200);
                }

                OnUi(() =>
                {
                    IsRunning = false;
                    RaiseSelectionDependent();
                });
            });
        }
        finally
        {
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
        OnPropertyChanged(nameof(HasOperationsSelected));
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
}

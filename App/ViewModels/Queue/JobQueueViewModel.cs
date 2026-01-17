using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Storyboard.Application.Abstractions;
using Storyboard.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace Storyboard.ViewModels.Queue;

/// <summary>
/// 任务队列 ViewModel - 负责任务列表显示和操作
/// </summary>
public partial class JobQueueViewModel : ObservableObject
{
    private readonly IJobQueueService _jobQueue;
    private readonly IMessenger _messenger;

    [ObservableProperty]
    private bool _isTaskManagerDialogOpen;

    public ObservableCollection<GenerationJob> JobHistory => _jobQueue.Jobs;

    public int GeneratingCount => JobHistory.Count(j => j.Status == GenerationJobStatus.Running || j.Status == GenerationJobStatus.Queued);

    public int ShotsRenderingCount => JobHistory.Count(j =>
        j.Status == GenerationJobStatus.Running &&
        j.Type == GenerationJobType.Video);

    public JobQueueViewModel(
        IJobQueueService jobQueue,
        IMessenger messenger)
    {
        _jobQueue = jobQueue;
        _messenger = messenger;

        // 订阅任务状态变更以更新计数
        _jobQueue.Jobs.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(GeneratingCount));
            OnPropertyChanged(nameof(ShotsRenderingCount));
        };
    }

    [RelayCommand]
    private void ToggleTaskManager()
    {
        IsTaskManagerDialogOpen = !IsTaskManagerDialogOpen;
    }

    [RelayCommand]
    private void ShowTaskManager()
    {
        IsTaskManagerDialogOpen = true;
    }

    [RelayCommand]
    private void CancelJob(GenerationJob? job)
    {
        if (job == null)
            return;

        _jobQueue.Cancel(job);
    }

    [RelayCommand]
    private void RetryJob(GenerationJob? job)
    {
        if (job == null)
            return;

        _jobQueue.Retry(job);
    }

    [RelayCommand]
    private void DeleteJob(GenerationJob? job)
    {
        if (job == null)
            return;

        _jobQueue.Remove(job);
    }
}

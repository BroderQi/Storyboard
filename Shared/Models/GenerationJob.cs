using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Storyboard.Models;

public partial class GenerationJob : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public GenerationJobType Type { get; init; }

    public int? ShotNumber { get; init; }

    [ObservableProperty]
    private GenerationJobStatus _status = GenerationJobStatus.Queued;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _error = string.Empty;

    [ObservableProperty]
    private int _attempt;

    public int MaxAttempts { get; init; } = 2;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    [ObservableProperty]
    private DateTimeOffset? _startedAt;

    [ObservableProperty]
    private DateTimeOffset? _completedAt;

    public string TypeText => Type switch
    {
        GenerationJobType.AiParse => "AI 解析",
        GenerationJobType.TextToShot => "文本分镜",
        GenerationJobType.FrameExtract => "抽帧",
        GenerationJobType.ImageFirst => "首帧图片",
        GenerationJobType.ImageLast => "尾帧图片",
        GenerationJobType.Video => "分镜视频",
        GenerationJobType.FullRender => "整片合成",
        _ => Type.ToString()
    };

    public string StatusText => Status switch
    {
        GenerationJobStatus.Queued => "队列中",
        GenerationJobStatus.Running => "执行中",
        GenerationJobStatus.Retrying => "重试中",
        GenerationJobStatus.Succeeded => "完成",
        GenerationJobStatus.Failed => "失败",
        GenerationJobStatus.Canceled => "已取消",
        _ => Status.ToString()
    };

    public string TargetText => ShotNumber.HasValue ? $"#{ShotNumber.Value}" : "-";

    public bool CanCancel => Status is GenerationJobStatus.Queued or GenerationJobStatus.Running or GenerationJobStatus.Retrying;
    public bool CanRetry => Status is GenerationJobStatus.Failed or GenerationJobStatus.Canceled;

    partial void OnStatusChanged(GenerationJobStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanRetry));
    }
}

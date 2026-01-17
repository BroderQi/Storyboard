using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Storyboard.ViewModels.Shot;

/// <summary>
/// 时间轴 ViewModel - 负责时间轴布局计算
/// </summary>
public partial class TimelineViewModel : ObservableObject
{
    private readonly IMessenger _messenger;
    private readonly ILogger<TimelineViewModel> _logger;

    [ObservableProperty]
    private double _timelinePixelsPerSecond = 50;

    [ObservableProperty]
    private double _timelineWidth;

    [ObservableProperty]
    private double _totalDuration;

    [ObservableProperty]
    private ObservableCollection<TimeMarker> _timeMarkers = new();

    partial void OnTimelinePixelsPerSecondChanged(double value)
    {
        RecalculateTimelineLayout();
    }

    public TimelineViewModel(
        IMessenger messenger,
        ILogger<TimelineViewModel> logger)
    {
        _messenger = messenger;
        _logger = logger;

        // 订阅镜头变更消息
        _messenger.Register<ShotAddedMessage>(this, (r, m) => RecalculateTimelineLayout());
        _messenger.Register<ShotDeletedMessage>(this, (r, m) => RecalculateTimelineLayout());
        _messenger.Register<ShotUpdatedMessage>(this, (r, m) => RecalculateTimelineLayout());
        _messenger.Register<ShotMovedMessage>(this, (r, m) => RecalculateTimelineLayout());
    }

    public void RecalculateTimelineLayout()
    {
        // TODO: 从 ShotListViewModel 获取镜头列表
        // 暂时使用默认值
        TotalDuration = 0;
        TimelineWidth = TotalDuration * TimelinePixelsPerSecond;

        // 生成时间标记
        TimeMarkers.Clear();
        var interval = CalculateTimeMarkerInterval(TotalDuration);
        for (double t = 0; t <= TotalDuration; t += interval)
        {
            TimeMarkers.Add(new TimeMarker
            {
                Time = t,
                Position = t * TimelinePixelsPerSecond,
                Label = FormatTime(t)
            });
        }

        _logger.LogDebug("重新计算时间轴布局: Duration={Duration}s, Width={Width}px",
            TotalDuration, TimelineWidth);
    }

    private double CalculateTimeMarkerInterval(double duration)
    {
        if (duration <= 10) return 1;
        if (duration <= 30) return 5;
        if (duration <= 60) return 10;
        if (duration <= 300) return 30;
        return 60;
    }

    private string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}

// 时间标记
public class TimeMarker
{
    public double Time { get; set; }
    public double Position { get; set; }
    public string Label { get; set; } = string.Empty;
}

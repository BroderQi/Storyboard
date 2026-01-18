// Project 实体代表一个分镜项目，包含多个镜头（Shot），用于组织和管理整个分镜流程。
namespace Storyboard.Domain.Entities;

public sealed class Project
{
    /// <summary>
    /// 项目唯一标识
    /// </summary>
    public string Id { get; set; } = default!;

    /// <summary>
    /// 项目名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// 选中的视频文件路径
    /// </summary>
    public string? SelectedVideoPath { get; set; }

    /// <summary>
    /// 是否有视频文件
    /// </summary>
    public bool HasVideoFile { get; set; }

    /// <summary>
    /// 视频文件时长（格式：mm:ss）
    /// </summary>
    public string VideoFileDuration { get; set; } = "--:--";

    /// <summary>
    /// 视频分辨率（格式：宽 x 高）
    /// </summary>
    public string VideoFileResolution { get; set; } = "-- x --";

    /// <summary>
    /// 视频帧率
    /// </summary>
    public string VideoFileFps { get; set; } = "--";

    /// <summary>
    /// 帧提取模式索引
    /// </summary>
    public int ExtractModeIndex { get; set; }

    /// <summary>
    /// 提取帧数
    /// </summary>
    public int FrameCount { get; set; } = 10;

    /// <summary>
    /// 帧提取时间间隔（毫秒）
    /// </summary>
    public double TimeInterval { get; set; } = 1000;

    /// <summary>
    /// 画面变化检测灵敏度
    /// </summary>
    public double DetectionSensitivity { get; set; } = 0.5;

    /// <summary>
    /// 创作目标（为什么拍这个视频）
    /// </summary>
    public string? CreativeGoal { get; set; }

    /// <summary>
    /// 目标受众（给谁看）
    /// </summary>
    public string? TargetAudience { get; set; }

    /// <summary>
    /// 视频基调（情绪氛围）
    /// </summary>
    public string? VideoTone { get; set; }

    /// <summary>
    /// 核心信息（重点是什么）
    /// </summary>
    public string? KeyMessage { get; set; }

    /// <summary>
    /// 项目下的镜头列表
    /// </summary>
    public List<Shot> Shots { get; set; } = new();
}

namespace Storyboard.Domain.Entities;

public sealed class Shot
{
    /// <summary>
    /// 镜头唯一标识
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属项目的唯一标识
    /// </summary>
    public string ProjectId { get; set; } = default!;

    /// <summary>
    /// 所属项目实体
    /// </summary>
    public Project Project { get; set; } = default!;

    /// <summary>
    /// 镜头编号（在项目中的顺序）
    /// </summary>
    public int ShotNumber { get; set; }

    /// <summary>
    /// 镜头时长（秒）
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// 镜头起始时间（秒）
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// 镜头结束时间（秒）
    /// </summary>
    public double EndTime { get; set; }

    /// <summary>
    /// 首帧生成提示词
    /// </summary>
    public string FirstFramePrompt { get; set; } = string.Empty;

    /// <summary>
    /// 末帧生成提示词
    /// </summary>
    public string LastFramePrompt { get; set; } = string.Empty;

    /// <summary>
    /// 镜头类型（如远景、近景等）
    /// </summary>
    public string ShotType { get; set; } = string.Empty;

    /// <summary>
    /// 镜头核心内容描述
    /// </summary>
    public string CoreContent { get; set; } = string.Empty;

    /// <summary>
    /// 镜头动作指令
    /// </summary>
    public string ActionCommand { get; set; } = string.Empty;

    /// <summary>
    /// 场景设定描述
    /// </summary>
    public string SceneSettings { get; set; } = string.Empty;

    /// <summary>
    /// 选用的AI模型
    /// </summary>
    public string SelectedModel { get; set; } = string.Empty;

    /// <summary>
    /// 首帧图片路径
    /// </summary>
    public string? FirstFrameImagePath { get; set; }

    /// <summary>
    /// 末帧图片路径
    /// </summary>
    public string? LastFrameImagePath { get; set; }

    /// <summary>
    /// 生成的视频文件路径
    /// </summary>
    public string? GeneratedVideoPath { get; set; }

    /// <summary>
    /// 素材缩略图路径
    /// </summary>
    public string? MaterialThumbnailPath { get; set; }

    /// <summary>
    /// 素材文件路径
    /// </summary>
    public string? MaterialFilePath { get; set; }

    /// <summary>
    /// 镜头下的素材列表
    /// </summary>
    public List<ShotAsset> Assets { get; set; } = new();
}

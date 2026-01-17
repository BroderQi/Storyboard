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
    /// 素材分辨率
    /// </summary>
    public string MaterialResolution { get; set; } = string.Empty;

    /// <summary>
    /// 素材文件大小
    /// </summary>
    public string MaterialFileSize { get; set; } = string.Empty;

    /// <summary>
    /// 素材格式
    /// </summary>
    public string MaterialFormat { get; set; } = string.Empty;

    /// <summary>
    /// 素材主色调
    /// </summary>
    public string MaterialColorTone { get; set; } = string.Empty;

    /// <summary>
    /// 素材亮度
    /// </summary>
    public string MaterialBrightness { get; set; } = string.Empty;

    // Image generation parameters
    /// <summary>
    /// 图片尺寸
    /// </summary>
    public string ImageSize { get; set; } = string.Empty;

    /// <summary>
    /// 负面提示词
    /// </summary>
    public string NegativePrompt { get; set; } = string.Empty;

    // Image professional parameters
    /// <summary>
    /// 宽高比
    /// </summary>
    public string AspectRatio { get; set; } = string.Empty;

    /// <summary>
    /// 光线类型
    /// </summary>
    public string LightingType { get; set; } = string.Empty;

    /// <summary>
    /// 时间段
    /// </summary>
    public string TimeOfDay { get; set; } = string.Empty;

    /// <summary>
    /// 构图
    /// </summary>
    public string Composition { get; set; } = string.Empty;

    /// <summary>
    /// 色调风格
    /// </summary>
    public string ColorStyle { get; set; } = string.Empty;

    /// <summary>
    /// 镜头类型
    /// </summary>
    public string LensType { get; set; } = string.Empty;

    // Video generation parameters
    /// <summary>
    /// 视频提示词
    /// </summary>
    public string VideoPrompt { get; set; } = string.Empty;

    /// <summary>
    /// 场景描述
    /// </summary>
    public string SceneDescription { get; set; } = string.Empty;

    /// <summary>
    /// 动作描述
    /// </summary>
    public string ActionDescription { get; set; } = string.Empty;

    /// <summary>
    /// 风格描述
    /// </summary>
    public string StyleDescription { get; set; } = string.Empty;

    /// <summary>
    /// 视频负面提示词
    /// </summary>
    public string VideoNegativePrompt { get; set; } = string.Empty;

    // Video professional parameters
    /// <summary>
    /// 运镜方式
    /// </summary>
    public string CameraMovement { get; set; } = string.Empty;

    /// <summary>
    /// 拍摄风格
    /// </summary>
    public string ShootingStyle { get; set; } = string.Empty;

    /// <summary>
    /// 视频特效
    /// </summary>
    public string VideoEffect { get; set; } = string.Empty;

    /// <summary>
    /// 视频分辨率
    /// </summary>
    public string VideoResolution { get; set; } = string.Empty;

    /// <summary>
    /// 视频比例
    /// </summary>
    public string VideoRatio { get; set; } = string.Empty;

    /// <summary>
    /// 视频帧数
    /// </summary>
    public int VideoFrames { get; set; }

    /// <summary>
    /// 使用首帧参考
    /// </summary>
    public bool UseFirstFrameReference { get; set; } = true;

    /// <summary>
    /// 使用尾帧参考
    /// </summary>
    public bool UseLastFrameReference { get; set; }

    /// <summary>
    /// 随机种子
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// 固定摄影机
    /// </summary>
    public bool CameraFixed { get; set; }

    /// <summary>
    /// 水印
    /// </summary>
    public bool Watermark { get; set; }

    // First frame professional parameters
    /// <summary>
    /// 首帧构图
    /// </summary>
    public string FirstFrameComposition { get; set; } = string.Empty;

    /// <summary>
    /// 首帧光线类型
    /// </summary>
    public string FirstFrameLightingType { get; set; } = string.Empty;

    /// <summary>
    /// 首帧时间
    /// </summary>
    public string FirstFrameTimeOfDay { get; set; } = string.Empty;

    /// <summary>
    /// 首帧色调风格
    /// </summary>
    public string FirstFrameColorStyle { get; set; } = string.Empty;

    /// <summary>
    /// 首帧景别
    /// </summary>
    public string FirstFrameLensType { get; set; } = string.Empty;

    /// <summary>
    /// 首帧负面提示词
    /// </summary>
    public string FirstFrameNegativePrompt { get; set; } = string.Empty;

    /// <summary>
    /// 首帧图片尺寸
    /// </summary>
    public string FirstFrameImageSize { get; set; } = string.Empty;

    /// <summary>
    /// 首帧宽高比
    /// </summary>
    public string FirstFrameAspectRatio { get; set; } = string.Empty;

    /// <summary>
    /// 首帧选择的模型
    /// </summary>
    public string FirstFrameSelectedModel { get; set; } = string.Empty;

    /// <summary>
    /// 首帧随机种子
    /// </summary>
    public int? FirstFrameSeed { get; set; }

    // Last frame professional parameters
    /// <summary>
    /// 尾帧构图
    /// </summary>
    public string LastFrameComposition { get; set; } = string.Empty;

    /// <summary>
    /// 尾帧光线类型
    /// </summary>
    public string LastFrameLightingType { get; set; } = string.Empty;

    /// <summary>
    /// 尾帧时间
    /// </summary>
    public string LastFrameTimeOfDay { get; set; } = string.Empty;

    /// <summary>
    /// 尾帧色调风格
    /// </summary>
    public string LastFrameColorStyle { get; set; } = string.Empty;

    /// <summary>
    /// 尾帧景别
    /// </summary>
    public string LastFrameLensType { get; set; } = string.Empty;

    /// <summary>
    /// 尾帧负面提示词
    /// </summary>
    public string LastFrameNegativePrompt { get; set; } = string.Empty;

    /// <summary>
    /// 尾帧图片尺寸
    /// </summary>
    public string LastFrameImageSize { get; set; } = string.Empty;

    /// <summary>
    /// 尾帧宽高比
    /// </summary>
    public string LastFrameAspectRatio { get; set; } = string.Empty;

    /// <summary>
    /// 尾帧选择的模型
    /// </summary>
    public string LastFrameSelectedModel { get; set; } = string.Empty;

    /// <summary>
    /// 尾帧随机种子
    /// </summary>
    public int? LastFrameSeed { get; set; }

    /// <summary>
    /// 镜头下的素材列表
    /// </summary>
    public List<ShotAsset> Assets { get; set; } = new();
}

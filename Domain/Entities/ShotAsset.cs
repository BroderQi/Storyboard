// ShotAsset 实体代表镜头（Shot）下的具体素材，如图片、音频、视频等，关联到 Shot。
namespace Storyboard.Domain.Entities;

public sealed class ShotAsset
{
    /// <summary>
    /// 素材唯一标识
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属镜头的唯一标识
    /// </summary>
    public long ShotId { get; set; }

    /// <summary>
    /// 所属镜头实体
    /// </summary>
    public Shot Shot { get; set; } = default!;

    /// <summary>
    /// 所属项目的唯一标识
    /// </summary>
    public string ProjectId { get; set; } = default!;

    /// <summary>
    /// 素材类型（如首帧图片、末帧图片、生成视频等）
    /// </summary>
    public ShotAssetType Type { get; set; }

    /// <summary>
    /// 素材文件路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 素材缩略图路径
    /// </summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>
    /// 生成提示词
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// 生成所用模型
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

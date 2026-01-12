// ShotAssetType 枚举用于定义 ShotAsset 的类型（如图片、音频、视频等），便于分类和处理不同类型的素材。
namespace Storyboard.Domain.Entities;

public enum ShotAssetType
{
    /// <summary>
    /// 首帧图片
    /// </summary>
    FirstFrameImage = 1,

    /// <summary>
    /// 末帧图片
    /// </summary>
    LastFrameImage = 2,

    /// <summary>
    /// 生成的视频
    /// </summary>
    GeneratedVideo = 3
}

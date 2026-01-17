using Storyboard.Models;

namespace Storyboard.Messages;

// 镜头添加消息
public record ShotAddedMessage(ShotItem Shot);

// 镜头删除消息
public record ShotDeletedMessage(ShotItem Shot);

// 镜头更新消息
public record ShotUpdatedMessage(ShotItem Shot);

// 镜头移动消息
public record ShotMovedMessage(ShotItem Shot, int FromIndex, int ToIndex);

// 镜头选中消息
public record ShotSelectedMessage(ShotItem? Shot);

// 镜头复制请求消息
public record ShotDuplicateRequestedMessage(ShotItem Shot);

// 镜头删除请求消息
public record ShotDeleteRequestedMessage(ShotItem Shot);

// 抽帧完成消息
public record FramesExtractedMessage(IReadOnlyList<ShotItem> Shots);

// 查询所有镜头消息（用于跨ViewModel数据访问）
public class GetAllShotsQuery
{
    public IReadOnlyList<ShotItem>? Shots { get; set; }
}

// 查询当前项目ID消息
public class GetCurrentProjectIdQuery
{
    public string? ProjectId { get; set; }
}

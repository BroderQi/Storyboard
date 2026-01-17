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

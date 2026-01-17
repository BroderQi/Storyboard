using Storyboard.Models;

namespace Storyboard.Messages;

// AI 解析请求消息
public record AiParseRequestedMessage(ShotItem Shot);

// AI 解析完成消息
public record AiParseCompletedMessage(ShotItem Shot, bool Success);

// 图像生成请求消息
public record ImageGenerationRequestedMessage(ShotItem Shot, bool IsFirstFrame);

// 图像生成完成消息
public record ImageGenerationCompletedMessage(ShotItem Shot, bool IsFirstFrame, bool Success, string? ImagePath);

// 视频生成请求消息
public record VideoGenerationRequestedMessage(ShotItem Shot);

// 视频生成完成消息
public record VideoGenerationCompletedMessage(ShotItem Shot, bool Success, string? VideoPath);

// 导出请求消息
public record ExportRequestedMessage(string OutputPath);

// 导出完成消息
public record ExportCompletedMessage(bool Success, string? OutputPath);

using Storyboard.Application.Services;
using Storyboard.Models;

namespace Storyboard.Messages;

// 项目创建消息
public record ProjectCreatedMessage(string ProjectId, string ProjectName);

// 项目打开消息
public record ProjectOpenedMessage(string ProjectId, string ProjectName);

// 项目数据加载完成消息
public record ProjectDataLoadedMessage(ProjectState ProjectState);

// 项目关闭消息
public record ProjectClosedMessage(string ProjectId);

// 项目删除消息
public record ProjectDeletedMessage(string ProjectId);

// 项目更新消息
public record ProjectUpdatedMessage(string ProjectId);

// 视频导入消息
public record VideoImportedMessage(string VideoPath);

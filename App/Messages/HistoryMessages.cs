namespace Storyboard.Messages;

// 撤销请求消息
public record UndoRequestedMessage();

// 重做请求消息
public record RedoRequestedMessage();

// 历史状态变更消息
public record HistoryChangedMessage(bool CanUndo, bool CanRedo);

// 标记可撤销变更消息
public record MarkUndoableChangeMessage();

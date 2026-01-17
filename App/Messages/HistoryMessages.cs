namespace Storyboard.Messages;

// 撤销请求消息
public record UndoRequestedMessage();

// 重做请求消息
public record RedoRequestedMessage();

// 历史状态变更消息
public record HistoryChangedMessage(bool CanUndo, bool CanRedo);

// 标记可撤销变更消息
public record MarkUndoableChangeMessage();

// 恢复快照消息
public record RestoreSnapshotMessage(List<ShotSnapshotData> Shots);

// 镜头快照数据（用于消息传递）
public record ShotSnapshotData(
    int ShotNumber,
    double Duration,
    double StartTime,
    double EndTime,
    string FirstFramePrompt,
    string LastFramePrompt,
    string ShotType,
    string CoreContent,
    string ActionCommand,
    string SceneSettings,
    string SelectedModel);

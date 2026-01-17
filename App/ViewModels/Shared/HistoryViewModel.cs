using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Storyboard.ViewModels.Shared;

/// <summary>
/// 历史记录 ViewModel - 负责撤销/重做逻辑
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly IMessenger _messenger;
    private readonly ILogger<HistoryViewModel> _logger;

    private readonly Stack<ProjectSnapshot> _undoStack = new();
    private readonly Stack<ProjectSnapshot> _redoStack = new();

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    private bool _isHistorySuspended;
    private bool _isRestoringSnapshot;

    public HistoryViewModel(
        IMessenger messenger,
        ILogger<HistoryViewModel> logger)
    {
        _messenger = messenger;
        _logger = logger;

        // 订阅消息
        _messenger.Register<MarkUndoableChangeMessage>(this, OnMarkUndoableChange);
        _messenger.Register<ProjectOpenedMessage>(this, (r, m) => InitializeHistory());
        _messenger.Register<ProjectCreatedMessage>(this, (r, m) => InitializeHistory());
        _messenger.Register<ProjectClosedMessage>(this, (r, m) => ClearHistory());
    }

    [RelayCommand]
    private void Undo()
    {
        if (!CanUndo || _undoStack.Count == 0)
            return;

        var snapshot = _undoStack.Pop();
        var currentSnapshot = TakeSnapshot();

        if (currentSnapshot != null)
            _redoStack.Push(currentSnapshot);

        RestoreSnapshot(snapshot);
        UpdateUndoRedoState();

        _logger.LogInformation("执行撤销操作");
    }

    [RelayCommand]
    private void Redo()
    {
        if (!CanRedo || _redoStack.Count == 0)
            return;

        var snapshot = _redoStack.Pop();
        var currentSnapshot = TakeSnapshot();

        if (currentSnapshot != null)
            _undoStack.Push(currentSnapshot);

        RestoreSnapshot(snapshot);
        UpdateUndoRedoState();

        _logger.LogInformation("执行重做操作");
    }

    public void InitializeHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        UpdateUndoRedoState();

        _logger.LogInformation("初始化历史记录");
    }

    private void ClearHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        UpdateUndoRedoState();

        _logger.LogInformation("清空历史记录");
    }

    private void OnMarkUndoableChange(object recipient, MarkUndoableChangeMessage message)
    {
        if (_isHistorySuspended || _isRestoringSnapshot)
            return;

        var snapshot = TakeSnapshot();
        if (snapshot != null)
        {
            _undoStack.Push(snapshot);
            _redoStack.Clear();
            UpdateUndoRedoState();

            _logger.LogDebug("记录历史快照");
        }
    }

    private ProjectSnapshot? TakeSnapshot()
    {
        // 通过消息查询获取所有镜头数据
        var query = new GetAllShotsQuery();
        _messenger.Send(query);

        if (query.Shots == null || query.Shots.Count == 0)
            return null;

        // 创建镜头快照列表
        var shotSnapshots = query.Shots.Select(shot => new ShotSnapshot(
            shot.ShotNumber,
            shot.Duration,
            shot.StartTime,
            shot.EndTime,
            shot.FirstFramePrompt,
            shot.LastFramePrompt,
            shot.ShotType,
            shot.CoreContent,
            shot.ActionCommand,
            shot.SceneSettings,
            shot.SelectedModel
        )).ToList();

        return new ProjectSnapshot(shotSnapshots, DateTimeOffset.Now);
    }

    private void RestoreSnapshot(ProjectSnapshot snapshot)
    {
        _isRestoringSnapshot = true;

        try
        {
            // 将快照数据转换为消息数据格式
            var shotDataList = snapshot.Shots.Select(s => new ShotSnapshotData(
                s.ShotNumber,
                s.Duration,
                s.StartTime,
                s.EndTime,
                s.FirstFramePrompt,
                s.LastFramePrompt,
                s.ShotType,
                s.CoreContent,
                s.ActionCommand,
                s.SceneSettings,
                s.SelectedModel
            )).ToList();

            // 发送恢复快照消息，让 ShotListViewModel 处理
            _messenger.Send(new RestoreSnapshotMessage(shotDataList));

            _logger.LogInformation("恢复历史快照: {ShotCount} 个镜头", snapshot.Shots.Count);
        }
        finally
        {
            _isRestoringSnapshot = false;
        }
    }

    private void UpdateUndoRedoState()
    {
        CanUndo = _undoStack.Count > 0;
        CanRedo = _redoStack.Count > 0;

        _messenger.Send(new HistoryChangedMessage(CanUndo, CanRedo));
    }

    public void RunWithoutHistory(Action action)
    {
        _isHistorySuspended = true;
        try
        {
            action();
        }
        finally
        {
            _isHistorySuspended = false;
        }
    }
}

// 项目快照记录
public record ProjectSnapshot(
    List<ShotSnapshot> Shots,
    DateTimeOffset Timestamp);

// 镜头快照记录
public record ShotSnapshot(
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

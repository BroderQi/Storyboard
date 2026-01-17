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
        // TODO: 从 ShotListViewModel 和其他 ViewModel 获取当前状态
        // 这需要通过共享状态或消息机制实现
        return null;
    }

    private void RestoreSnapshot(ProjectSnapshot snapshot)
    {
        _isRestoringSnapshot = true;

        try
        {
            // TODO: 恢复快照到各个 ViewModel
            // 这需要发送消息通知各个 ViewModel 恢复状态
            _logger.LogInformation("恢复历史快照");
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

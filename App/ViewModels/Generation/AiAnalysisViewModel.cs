using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels.Generation;

/// <summary>
/// AI 分析 ViewModel - 负责 AI 解析镜头
/// </summary>
public partial class AiAnalysisViewModel : ObservableObject
{
    private readonly IAiShotService _aiShotService;
    private readonly IJobQueueService _jobQueue;
    private readonly IMessenger _messenger;
    private readonly ILogger<AiAnalysisViewModel> _logger;

    public AiAnalysisViewModel(
        IAiShotService aiShotService,
        IJobQueueService jobQueue,
        IMessenger messenger,
        ILogger<AiAnalysisViewModel> logger)
    {
        _aiShotService = aiShotService;
        _jobQueue = jobQueue;
        _messenger = messenger;
        _logger = logger;

        // 订阅 AI 解析请求消息
        _messenger.Register<AiParseRequestedMessage>(this, OnAiParseRequested);
    }

    [RelayCommand]
    private async Task AIAnalyzeAll()
    {
        // 需要从 ShotListViewModel 获取镜头列表
        // 这里暂时使用消息机制
        _logger.LogInformation("开始批量 AI 分析");

        // TODO: 实现批量 AI 分析逻辑
        // 需要获取所有镜头并逐个加入队列
    }

    private async void OnAiParseRequested(object recipient, AiParseRequestedMessage message)
    {
        var shot = message.Shot;

        try
        {
            shot.IsAiParsing = true;

            // 创建 AI 解析任务
            _jobQueue.Enqueue(
                GenerationJobType.AiParse,
                shot.ShotNumber,
                async (ct, progress) =>
                {
                    // 创建 AI 分析请求
                    var request = new AiShotAnalysisRequest(
                        FirstFramePath: shot.FirstFrameImagePath,
                        LastFramePath: shot.LastFrameImagePath,
                        ExistingShotType: shot.ShotType,
                        ExistingCoreContent: shot.CoreContent,
                        ExistingActionCommand: shot.ActionCommand,
                        ExistingSceneSettings: shot.SceneSettings,
                        ExistingFirstFramePrompt: shot.FirstFramePrompt,
                        ExistingLastFramePrompt: shot.LastFramePrompt
                    );

                    // 执行 AI 解析
                    var result = await _aiShotService.AnalyzeShotAsync(request, ct);

                    if (result != null)
                    {
                        // 应用 AI 解析结果
                        shot.FirstFramePrompt = result.FirstFramePrompt ?? shot.FirstFramePrompt;
                        shot.LastFramePrompt = result.LastFramePrompt ?? shot.LastFramePrompt;
                        shot.ShotType = result.ShotType ?? shot.ShotType;
                        shot.CoreContent = result.CoreContent ?? shot.CoreContent;
                        shot.ActionCommand = result.ActionCommand ?? shot.ActionCommand;
                        shot.SceneSettings = result.SceneSettings ?? shot.SceneSettings;

                        _messenger.Send(new AiParseCompletedMessage(shot, true));
                        _logger.LogInformation("AI 解析完成: Shot {ShotNumber}", shot.ShotNumber);
                    }
                    else
                    {
                        _messenger.Send(new AiParseCompletedMessage(shot, false));
                        _logger.LogWarning("AI 解析失败: Shot {ShotNumber}", shot.ShotNumber);
                    }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 解析异常: Shot {ShotNumber}", shot.ShotNumber);
            _messenger.Send(new AiParseCompletedMessage(shot, false));
        }
        finally
        {
            shot.IsAiParsing = false;
        }
    }
}

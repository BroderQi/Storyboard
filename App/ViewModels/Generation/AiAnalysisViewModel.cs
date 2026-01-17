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
        _logger.LogInformation("开始批量 AI 分析");

        // 查询所有镜头
        var query = new GetAllShotsQuery();
        _messenger.Send(query);
        var shots = query.Shots;

        if (shots == null || shots.Count == 0)
        {
            _logger.LogWarning("没有镜头可分析");
            return;
        }

        // 检查是否需要询问AI写入模式
        var needMode = shots.Any(NeedsAiWriteMode);
        AiWriteMode? mode = null;

        if (needMode)
        {
            mode = await RequestAiWriteModeAsync();
            if (mode == null)
            {
                _logger.LogInformation("用户取消了批量AI分析");
                return;
            }
        }

        var queuedCount = 0;
        var skippedCount = 0;

        foreach (var shot in shots)
        {
            // 跳过没有素材图片的镜头
            if (string.IsNullOrWhiteSpace(shot.MaterialFilePath) || !System.IO.File.Exists(shot.MaterialFilePath))
            {
                _logger.LogInformation("跳过缺少素材的镜头: Shot {ShotNumber}", shot.ShotNumber);
                skippedCount++;
                continue;
            }

            // 跳过正在解析的镜头
            if (shot.IsAiParsing)
            {
                _logger.LogInformation("跳过正在解析的镜头: Shot {ShotNumber}", shot.ShotNumber);
                skippedCount++;
                continue;
            }

            // 发送AI解析请求消息
            _messenger.Send(new AiParseRequestedMessage(shot));
            queuedCount++;
        }

        _logger.LogInformation("批量AI分析: 已加入队列 {Queued} 个镜头, 跳过 {Skipped} 个镜头", queuedCount, skippedCount);
    }

    private static bool NeedsAiWriteMode(ShotItem shot)
    {
        return !string.IsNullOrWhiteSpace(shot.ShotType)
            || !string.IsNullOrWhiteSpace(shot.CoreContent)
            || !string.IsNullOrWhiteSpace(shot.ActionCommand)
            || !string.IsNullOrWhiteSpace(shot.SceneSettings)
            || !string.IsNullOrWhiteSpace(shot.FirstFramePrompt)
            || !string.IsNullOrWhiteSpace(shot.LastFramePrompt);
    }

    private async Task<AiWriteMode?> RequestAiWriteModeAsync()
    {
        // 确保在UI线程上执行
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            return await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(RequestAiWriteModeOnUiThreadAsync);
        }

        return await RequestAiWriteModeOnUiThreadAsync();
    }

    private async Task<AiWriteMode?> RequestAiWriteModeOnUiThreadAsync()
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;
        var dialog = new Views.AiWriteModeDialog();

        if (owner == null)
            return AiWriteMode.Overwrite;

        return await dialog.ShowDialog<AiWriteMode?>(owner);
    }

    /// <summary>
    /// 根据文本提示生成分镜列表
    /// </summary>
    public async Task<IReadOnlyList<AiShotDescription>> GenerateShotsFromTextAsync(string prompt)
    {
        try
        {
            _logger.LogInformation("开始文本生成分镜");
            var result = await _aiShotService.GenerateShotsFromTextAsync(prompt);
            _logger.LogInformation("文本生成分镜完成，生成了 {Count} 个分镜", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文本生成分镜失败");
            throw;
        }
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
                    try
                    {
                        // 创建 AI 分析请求 - 使用素材图片进行分析
                        var request = new AiShotAnalysisRequest(
                            MaterialImagePath: shot.MaterialFilePath,
                            ExistingShotType: shot.ShotType,
                            ExistingCoreContent: shot.CoreContent,
                            ExistingActionCommand: shot.ActionCommand,
                            ExistingSceneSettings: shot.SceneSettings,
                            ExistingFirstFramePrompt: shot.FirstFramePrompt,
                            ExistingLastFramePrompt: shot.LastFramePrompt
                        );

                        // 执行 AI 解析
                        var result = await _aiShotService.AnalyzeShotAsync(request, ct);

                        // 在 UI 线程上更新结果
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (result != null)
                            {
                                // 记录 AI 解析结果（完整版）
                                _logger.LogInformation("AI 解析结果 - Shot {ShotNumber}:\n" +
                                    "  基础: ShotType={ShotType}, CoreContent={CoreContent}\n" +
                                    "  提示词: FirstFrame={FirstFrame}..., LastFrame={LastFrame}...\n" +
                                    "  图片参数: Composition={Composition}, Lighting={Lighting}, TimeOfDay={TimeOfDay}, ColorStyle={ColorStyle}, NegativePrompt={NegativePrompt}, ImageSize={ImageSize}\n" +
                                    "  视频参数: VideoPrompt={VideoPrompt}..., Scene={Scene}..., Action={Action}..., Style={Style}..., Camera={Camera}, Shooting={Shooting}, Effect={Effect}, VideoNegative={VideoNegative}, Resolution={Resolution}, Ratio={Ratio}",
                                    shot.ShotNumber,
                                    result.ShotType,
                                    result.CoreContent?.Substring(0, Math.Min(30, result.CoreContent?.Length ?? 0)),
                                    result.FirstFramePrompt?.Substring(0, Math.Min(30, result.FirstFramePrompt?.Length ?? 0)),
                                    result.LastFramePrompt?.Substring(0, Math.Min(30, result.LastFramePrompt?.Length ?? 0)),
                                    result.Composition ?? "null",
                                    result.LightingType ?? "null",
                                    result.TimeOfDay ?? "null",
                                    result.ColorStyle ?? "null",
                                    result.NegativePrompt?.Substring(0, Math.Min(20, result.NegativePrompt?.Length ?? 0)) ?? "null",
                                    result.ImageSize ?? "null",
                                    result.VideoPrompt?.Substring(0, Math.Min(30, result.VideoPrompt?.Length ?? 0)) ?? "null",
                                    result.SceneDescription?.Substring(0, Math.Min(30, result.SceneDescription?.Length ?? 0)) ?? "null",
                                    result.ActionDescription?.Substring(0, Math.Min(30, result.ActionDescription?.Length ?? 0)) ?? "null",
                                    result.StyleDescription?.Substring(0, Math.Min(30, result.StyleDescription?.Length ?? 0)) ?? "null",
                                    result.CameraMovement ?? "null",
                                    result.ShootingStyle ?? "null",
                                    result.VideoEffect ?? "null",
                                    result.VideoNegativePrompt?.Substring(0, Math.Min(20, result.VideoNegativePrompt?.Length ?? 0)) ?? "null",
                                    result.VideoResolution ?? "null",
                                    result.VideoRatio ?? "null");

                                // 使用批量更新方法，避免多次触发 PropertyChanged
                                shot.ApplyAiAnalysisResult(result);

                                _messenger.Send(new AiParseCompletedMessage(shot, true));
                                _logger.LogInformation("AI 解析完成并已应用到 Shot {ShotNumber}", shot.ShotNumber);
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
                        _logger.LogError(ex, "AI 解析任务执行异常: Shot {ShotNumber}", shot.ShotNumber);
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _messenger.Send(new AiParseCompletedMessage(shot, false));
                        });
                    }
                    finally
                    {
                        // 在 UI 线程上重置状态
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            shot.IsAiParsing = false;
                        });
                    }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 解析入队异常: Shot {ShotNumber}", shot.ShotNumber);
            _messenger.Send(new AiParseCompletedMessage(shot, false));
            shot.IsAiParsing = false;
        }
    }
}

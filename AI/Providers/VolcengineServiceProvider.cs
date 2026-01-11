using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Storyboard.AI.Core;
using Storyboard.AI.Adapters;

namespace Storyboard.AI.Providers;

/// <summary>
/// 火山引擎服务提供商
/// </summary>
public class VolcengineServiceProvider : BaseAIServiceProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public VolcengineServiceProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor, ILogger<VolcengineServiceProvider> logger)
        : base(logger)
    {
        _configMonitor = configMonitor;
    }

    private VolcengineConfig Config => _configMonitor.CurrentValue.Volcengine;

    public override AIProviderType ProviderType => AIProviderType.Volcengine;
    public override string DisplayName => "火山引擎";
    
    public override bool IsConfigured => 
        Config.Enabled &&
        !string.IsNullOrEmpty(Config.ApiKey) && 
        !string.IsNullOrEmpty(Config.EndpointId) &&
        !string.IsNullOrEmpty(Config.DefaultModel);

    public override IReadOnlyList<string> SupportedModels => new[]
    {
        "doubao-pro-4k",
        "doubao-pro-32k",
        "doubao-lite-4k"
    };

    protected override Task<Kernel> CreateKernelAsync(string? modelId = null)
    {
        var cfg = Config;
        var model = modelId ?? cfg.DefaultModel;
        var httpClient = CreateHttpClient(cfg.Endpoint, cfg.TimeoutSeconds);
        var chatService = new VolcengineChatCompletionService(cfg.ApiKey, cfg.EndpointId, model, httpClient);
        
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);

        Logger.LogInformation("火山引擎 Kernel 已创建，模型: {Model}", model);
        return Task.FromResult(builder.Build());
    }

    public override async Task<bool> ValidateConfigurationAsync()
    {
        if (!IsConfigured)
        {
            Logger.LogWarning("火山引擎配置不完整");
            return false;
        }

        try
        {
            var kernel = await GetKernelAsync();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("测试");

            await chatService.GetChatMessageContentsAsync(chatHistory);
            
            Logger.LogInformation("火山引擎配置验证成功");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "火山引擎配置验证失败");
            return false;
        }
    }
}

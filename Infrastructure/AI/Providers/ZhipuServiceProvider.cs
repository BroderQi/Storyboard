using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Storyboard.AI.Core;
using Storyboard.AI.Adapters;

namespace Storyboard.AI.Providers;

/// <summary>
/// 智谱AI服务提供商
/// </summary>
public class ZhipuServiceProvider : BaseAIServiceProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public ZhipuServiceProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor, ILogger<ZhipuServiceProvider> logger)
        : base(logger)
    {
        _configMonitor = configMonitor;
    }

    private ZhipuConfig Config => _configMonitor.CurrentValue.Zhipu;

    public override AIProviderType ProviderType => AIProviderType.Zhipu;
    public override string DisplayName => "智谱AI";
    
    public override bool IsConfigured => 
        Config.Enabled &&
        !string.IsNullOrEmpty(Config.ApiKey) && 
        !string.IsNullOrEmpty(Config.DefaultModel);

    public override IReadOnlyList<string> SupportedModels => new[]
    {
        "glm-4",
        "glm-4v",
        "glm-3-turbo"
    };

    protected override Task<Kernel> CreateKernelAsync(string? modelId = null)
    {
        var cfg = Config;
        var model = modelId ?? cfg.DefaultModel;
        var httpClient = CreateHttpClient(cfg.Endpoint, cfg.TimeoutSeconds);
        var chatService = new ZhipuChatCompletionService(cfg.ApiKey, model, httpClient);
        
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);

        Logger.LogInformation("智谱AI Kernel 已创建，模型: {Model}", model);
        return Task.FromResult(builder.Build());
    }

    public override async Task<bool> ValidateConfigurationAsync()
    {
        if (!IsConfigured)
        {
            Logger.LogWarning("智谱AI配置不完整");
            return false;
        }

        try
        {
            var kernel = await GetKernelAsync();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("测试");

            await chatService.GetChatMessageContentsAsync(chatHistory);
            
            Logger.LogInformation("智谱AI配置验证成功");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "智谱AI配置验证失败");
            return false;
        }
    }
}

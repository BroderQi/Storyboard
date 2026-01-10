using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using 分镜大师.AI.Core;
using 分镜大师.AI.Adapters;

namespace 分镜大师.AI.Providers;

/// <summary>
/// 火山引擎服务提供商
/// </summary>
public class VolcengineServiceProvider : BaseAIServiceProvider
{
    private readonly VolcengineConfig _config;

    public VolcengineServiceProvider(IOptions<AIServicesConfiguration> config, ILogger<VolcengineServiceProvider> logger)
        : base(logger)
    {
        _config = config.Value.Volcengine;
    }

    public override AIProviderType ProviderType => AIProviderType.Volcengine;
    public override string DisplayName => "火山引擎";
    
    public override bool IsConfigured => 
        !string.IsNullOrEmpty(_config.ApiKey) && 
        !string.IsNullOrEmpty(_config.EndpointId) &&
        !string.IsNullOrEmpty(_config.DefaultModel);

    public override IReadOnlyList<string> SupportedModels => new[]
    {
        "doubao-pro-4k",
        "doubao-pro-32k",
        "doubao-lite-4k"
    };

    protected override Task<Kernel> CreateKernelAsync(string? modelId = null)
    {
        var model = modelId ?? _config.DefaultModel;
        var httpClient = CreateHttpClient(_config.Endpoint, _config.TimeoutSeconds);
        var chatService = new VolcengineChatCompletionService(_config.ApiKey, _config.EndpointId, model, httpClient);
        
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

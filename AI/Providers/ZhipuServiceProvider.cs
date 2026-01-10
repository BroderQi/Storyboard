using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using 分镜大师.AI.Core;
using 分镜大师.AI.Adapters;

namespace 分镜大师.AI.Providers;

/// <summary>
/// 智谱AI服务提供商
/// </summary>
public class ZhipuServiceProvider : BaseAIServiceProvider
{
    private readonly ZhipuConfig _config;

    public ZhipuServiceProvider(IOptions<AIServicesConfiguration> config, ILogger<ZhipuServiceProvider> logger)
        : base(logger)
    {
        _config = config.Value.Zhipu;
    }

    public override AIProviderType ProviderType => AIProviderType.Zhipu;
    public override string DisplayName => "智谱AI";
    
    public override bool IsConfigured => 
        !string.IsNullOrEmpty(_config.ApiKey) && 
        !string.IsNullOrEmpty(_config.DefaultModel);

    public override IReadOnlyList<string> SupportedModels => new[]
    {
        "glm-4",
        "glm-4v",
        "glm-3-turbo"
    };

    protected override Task<Kernel> CreateKernelAsync(string? modelId = null)
    {
        var model = modelId ?? _config.DefaultModel;
        var httpClient = CreateHttpClient(_config.Endpoint, _config.TimeoutSeconds);
        var chatService = new ZhipuChatCompletionService(_config.ApiKey, model, httpClient);
        
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

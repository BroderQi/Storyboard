using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Storyboard.AI.Core;
using Storyboard.AI.Adapters;

namespace Storyboard.AI.Providers;

/// <summary>
/// 通义千问服务提供商
/// </summary>
public class QwenServiceProvider : BaseAIServiceProvider
{
    private readonly QwenConfig _config;

    public QwenServiceProvider(IOptions<AIServicesConfiguration> config, ILogger<QwenServiceProvider> logger)
        : base(logger)
    {
        _config = config.Value.Qwen;
    }

    public override AIProviderType ProviderType => AIProviderType.Qwen;
    public override string DisplayName => "通义千问";
    
    public override bool IsConfigured => 
        !string.IsNullOrEmpty(_config.ApiKey) && 
        !string.IsNullOrEmpty(_config.DefaultModel);

    public override IReadOnlyList<string> SupportedModels => new[]
    {
        "qwen-turbo",
        "qwen-plus",
        "qwen-max",
        "qwen-max-longcontext"
    };

    protected override Task<Kernel> CreateKernelAsync(string? modelId = null)
    {
        var model = modelId ?? _config.DefaultModel;
        var httpClient = CreateHttpClient(_config.Endpoint, _config.TimeoutSeconds);
        var chatService = new QwenChatCompletionService(_config.ApiKey, model, httpClient);
        
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);

        Logger.LogInformation("通义千问 Kernel 已创建，模型: {Model}", model);
        return Task.FromResult(builder.Build());
    }

    public override async Task<bool> ValidateConfigurationAsync()
    {
        if (!IsConfigured)
        {
            Logger.LogWarning("通义千问配置不完整");
            return false;
        }

        try
        {
            var kernel = await GetKernelAsync();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("测试");

            await chatService.GetChatMessageContentsAsync(chatHistory);
            
            Logger.LogInformation("通义千问配置验证成功");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "通义千问配置验证失败");
            return false;
        }
    }
}

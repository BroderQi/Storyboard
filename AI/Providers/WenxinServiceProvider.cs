using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Storyboard.AI.Core;
using Storyboard.AI.Adapters;

namespace Storyboard.AI.Providers;

/// <summary>
/// 文心一言服务提供商
/// </summary>
public class WenxinServiceProvider : BaseAIServiceProvider
{
    private readonly WenxinConfig _config;

    public WenxinServiceProvider(IOptions<AIServicesConfiguration> config, ILogger<WenxinServiceProvider> logger)
        : base(logger)
    {
        _config = config.Value.Wenxin;
    }

    public override AIProviderType ProviderType => AIProviderType.Wenxin;
    public override string DisplayName => "文心一言";
    
    public override bool IsConfigured => 
        !string.IsNullOrEmpty(_config.ApiKey) && 
        !string.IsNullOrEmpty(_config.ApiSecret) &&
        !string.IsNullOrEmpty(_config.DefaultModel);

    public override IReadOnlyList<string> SupportedModels => new[]
    {
        "ERNIE-Bot-4",
        "ERNIE-Bot",
        "ERNIE-Bot-turbo"
    };

    protected override Task<Kernel> CreateKernelAsync(string? modelId = null)
    {
        var model = modelId ?? _config.DefaultModel;
        var httpClient = CreateHttpClient(_config.Endpoint, _config.TimeoutSeconds);
        var chatService = new WenxinChatCompletionService(_config.ApiKey, _config.ApiSecret, model, httpClient);
        
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);

        Logger.LogInformation("文心一言 Kernel 已创建，模型: {Model}", model);
        return Task.FromResult(builder.Build());
    }

    public override async Task<bool> ValidateConfigurationAsync()
    {
        if (!IsConfigured)
        {
            Logger.LogWarning("文心一言配置不完整");
            return false;
        }

        try
        {
            var kernel = await GetKernelAsync();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("测试");

            await chatService.GetChatMessageContentsAsync(chatHistory);
            
            Logger.LogInformation("文心一言配置验证成功");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "文心一言配置验证失败");
            return false;
        }
    }
}

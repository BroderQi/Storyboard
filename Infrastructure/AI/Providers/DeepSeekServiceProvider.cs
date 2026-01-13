using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Storyboard.AI.Core;
using Storyboard.AI.Adapters;

namespace Storyboard.AI.Providers;

/// <summary>
/// DeepSeek 服务提供商
/// </summary>
public sealed class DeepSeekServiceProvider : BaseAIServiceProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public DeepSeekServiceProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor, ILogger<DeepSeekServiceProvider> logger)
        : base(logger)
    {
        _configMonitor = configMonitor;
    }

    private DeepSeekConfig Config => _configMonitor.CurrentValue.DeepSeek;

    public override AIProviderType ProviderType => AIProviderType.DeepSeek;
    public override string DisplayName => "DeepSeek";

    public override bool IsConfigured =>
        Config.Enabled &&
        !string.IsNullOrWhiteSpace(Config.ApiKey) &&
        !string.IsNullOrWhiteSpace(Config.DefaultModel);

    public override IReadOnlyList<string> SupportedModels => new[]
    {
        "deepseek-chat",
        "deepseek-reasoner"
    };

    protected override Task<Kernel> CreateKernelAsync(string? modelId = null)
    {
        var cfg = Config;
        var model = modelId ?? cfg.DefaultModel;
        var httpClient = CreateHttpClient(cfg.Endpoint, cfg.TimeoutSeconds);
        var chatService = new OpenAICompatibleChatCompletionService(cfg.ApiKey, model, httpClient);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);

        Logger.LogInformation("DeepSeek Kernel 已创建，模型: {Model}", model);
        return Task.FromResult(builder.Build());
    }

    public override async Task<bool> ValidateConfigurationAsync()
    {
        if (!IsConfigured)
        {
            Logger.LogWarning("DeepSeek 配置不完整");
            return false;
        }

        try
        {
            var kernel = await GetKernelAsync();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("测试");

            await chatService.GetChatMessageContentsAsync(chatHistory);

            Logger.LogInformation("DeepSeek 配置验证成功");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "DeepSeek 配置验证失败");
            return false;
        }
    }
}

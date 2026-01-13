using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Storyboard.AI.Core;
using Storyboard.AI.Adapters;

namespace Storyboard.AI.Providers;

/// <summary>
/// OpenAI 服务提供商
/// </summary>
public sealed class OpenAIServiceProvider : BaseAIServiceProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public OpenAIServiceProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor, ILogger<OpenAIServiceProvider> logger)
        : base(logger)
    {
        _configMonitor = configMonitor;
    }

    private OpenAIConfig Config => _configMonitor.CurrentValue.OpenAI;

    public override AIProviderType ProviderType => AIProviderType.OpenAI;
    public override string DisplayName => "OpenAI";

    public override bool IsConfigured =>
        Config.Enabled &&
        !string.IsNullOrWhiteSpace(Config.ApiKey) &&
        !string.IsNullOrWhiteSpace(Config.DefaultModel);

    public override IReadOnlyList<string> SupportedModels => new[]
    {
        "gpt-4o",
        "gpt-4o-mini",
        "gpt-4.1",
        "gpt-4.1-mini",
        "o1-mini",
        "o1-preview"
    };

    protected override Task<Kernel> CreateKernelAsync(string? modelId = null)
    {
        var cfg = Config;
        var model = modelId ?? cfg.DefaultModel;
        var httpClient = CreateHttpClient(cfg.Endpoint, cfg.TimeoutSeconds);
        var chatService = new OpenAICompatibleChatCompletionService(cfg.ApiKey, model, httpClient, cfg.Organization);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);

        Logger.LogInformation("OpenAI Kernel 已创建，模型: {Model}", model);
        return Task.FromResult(builder.Build());
    }

    public override async Task<bool> ValidateConfigurationAsync()
    {
        if (!IsConfigured)
        {
            Logger.LogWarning("OpenAI 配置不完整");
            return false;
        }

        try
        {
            var kernel = await GetKernelAsync();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("测试");

            await chatService.GetChatMessageContentsAsync(chatHistory);

            Logger.LogInformation("OpenAI 配置验证成功");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "OpenAI 配置验证失败");
            return false;
        }
    }
}

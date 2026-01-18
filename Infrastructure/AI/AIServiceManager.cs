using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.AI.Prompts;
using System.Linq;

namespace Storyboard.AI;

/// <summary>
/// Central AI service manager for text generation.
/// </summary>
public class AIServiceManager
{
    private readonly ILogger<AIServiceManager> _logger;
    private readonly IEnumerable<IAIServiceProvider> _providers;
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly PromptManagementService _promptService;
    private IAIServiceProvider? _currentProvider;
    private AIProviderType? _overrideProvider;

    public AIServiceManager(
        ILogger<AIServiceManager> logger,
        IEnumerable<IAIServiceProvider> providers,
        IOptionsMonitor<AIServicesConfiguration> configMonitor,
        PromptManagementService promptService)
    {
        _logger = logger;
        _providers = providers;
        _configMonitor = configMonitor;
        _promptService = promptService;
    }

    /// <summary>
    /// Initialize prompt templates.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _promptService.LoadAllTemplatesAsync();
        _logger.LogInformation("AI service manager initialized.");
    }

    public IEnumerable<IAIServiceProvider> GetAvailableProviders()
    {
        return _providers.Where(p => p.IsConfigured);
    }

    public IEnumerable<IAIServiceProvider> GetAvailableProviders(AIProviderCapability capability)
    {
        return _providers.Where(p => p.IsConfigured && p.Capabilities.HasFlag(capability));
    }

    public void SetProvider(AIProviderType providerType)
    {
        _overrideProvider = providerType;
        _currentProvider = _providers.FirstOrDefault(p => p.ProviderType == providerType);
        if (_currentProvider == null)
        {
            throw new InvalidOperationException($"Provider not found: {providerType}");
        }
        _logger.LogInformation("Switched to provider: {Provider}", _currentProvider.DisplayName);
    }

    public IAIServiceProvider GetCurrentProvider()
    {
        var configuredDefault = _overrideProvider ?? _configMonitor.CurrentValue.Defaults.Text.Provider;
        if (_currentProvider == null || _currentProvider.ProviderType != configuredDefault || !_currentProvider.IsConfigured)
        {
            _currentProvider = _providers.FirstOrDefault(p => p.ProviderType == configuredDefault && p.IsConfigured);

            if (_currentProvider == null)
            {
                var firstAvailable = GetAvailableProviders().FirstOrDefault();
                if (firstAvailable == null)
                {
                    throw new InvalidOperationException("No available AI providers.");
                }
                _currentProvider = firstAvailable;
                _logger.LogWarning("Default provider unavailable. Switched to {Provider}", _currentProvider.DisplayName);
            }
        }
        return _currentProvider;
    }

    public async Task<string> ChatAsync(
        string promptTemplateId,
        Dictionary<string, object> parameters,
        string? modelId = null,
        string? creativeGoal = null,
        string? targetAudience = null,
        string? videoTone = null,
        string? keyMessage = null,
        CancellationToken cancellationToken = default)
    {
        var provider = GetCurrentProvider();
        var template = _promptService.GetTemplate(promptTemplateId);

        if (template == null)
        {
            throw new ArgumentException($"Prompt template not found: {promptTemplateId}");
        }

        var userPrompt = _promptService.RenderPromptWithIntent(
            template,
            parameters,
            creativeGoal,
            targetAudience,
            videoTone,
            keyMessage);
        var request = BuildChatRequest(provider.ProviderType, template, userPrompt, modelId);

        _logger.LogInformation("Sending chat request: {Provider}, template: {Template}",
            provider.DisplayName, template.Name);

        var response = await provider.ChatAsync(request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(response))
        {
            _logger.LogWarning(
                "Chat response empty. Provider: {Provider}, template: {Template}, model: {Model}",
                provider.DisplayName,
                template.Name,
                request.Model);
        }

        return response;
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        string promptTemplateId,
        Dictionary<string, object> parameters,
        string? modelId = null,
        string? creativeGoal = null,
        string? targetAudience = null,
        string? videoTone = null,
        string? keyMessage = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var provider = GetCurrentProvider();
        var template = _promptService.GetTemplate(promptTemplateId);

        if (template == null)
        {
            throw new ArgumentException($"Prompt template not found: {promptTemplateId}");
        }

        var userPrompt = _promptService.RenderPromptWithIntent(
            template,
            parameters,
            creativeGoal,
            targetAudience,
            videoTone,
            keyMessage);
        var request = BuildChatRequest(provider.ProviderType, template, userPrompt, modelId);

        _logger.LogInformation("Sending streaming chat request: {Provider}, template: {Template}",
            provider.DisplayName, template.Name);

        await foreach (var chunk in provider.ChatStreamAsync(request, cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk))
            {
                yield return chunk;
            }
        }
    }

    public async Task<string> ChatDirectAsync(
        string userMessage,
        string? systemMessage = null,
        string? modelId = null,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        var provider = GetCurrentProvider();
        var messages = new List<AIChatMessage>();

        if (!string.IsNullOrEmpty(systemMessage))
        {
            messages.Add(new AIChatMessage(AIChatRole.System, systemMessage));
        }

        messages.Add(new AIChatMessage(AIChatRole.User, userMessage));

        var request = new AIChatRequest
        {
            Model = ResolveTextModel(provider.ProviderType, modelId),
            Messages = messages,
            Temperature = temperature
        };

        return await provider.ChatAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ChatWithImageAsync(
        string promptTemplateId,
        string? imagePath,
        string additionalContext,
        string? modelId = null,
        CancellationToken cancellationToken = default)
    {
        var provider = GetCurrentProvider();
        var template = _promptService.GetTemplate(promptTemplateId);

        if (template == null)
        {
            throw new ArgumentException($"Prompt template not found: {promptTemplateId}");
        }

        var messages = new List<AIChatMessage>();

        if (!string.IsNullOrEmpty(template.SystemPrompt))
        {
            messages.Add(new AIChatMessage(AIChatRole.System, template.SystemPrompt));
        }

        // 构建多模态用户消息
        var contentParts = new List<Core.MessageContent>();

        // 添加图片（如果存在）
        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            var imageBase64 = Convert.ToBase64String(File.ReadAllBytes(imagePath));
            contentParts.Add(new Core.MessageContent(
                Core.MessageContentType.ImageBase64,
                ImageBase64: imageBase64));
        }

        // 添加文本提示
        var textPrompt = $"请分析这张分镜素材图片，并结合以下已有信息生成结构化的分镜描述：\n\n{additionalContext}\n\n请输出 JSON 对象。";
        contentParts.Add(new Core.MessageContent(
            Core.MessageContentType.Text,
            Text: textPrompt));

        messages.Add(new AIChatMessage(AIChatRole.User, contentParts));

        var request = new AIChatRequest
        {
            Model = ResolveTextModel(provider.ProviderType, modelId),
            Messages = messages,
            Temperature = template.ExecutionSettings.Temperature,
            TopP = template.ExecutionSettings.TopP,
            MaxTokens = template.ExecutionSettings.MaxTokens
        };

        _logger.LogInformation("Sending chat request with image: {Provider}, template: {Template}, image: {ImagePath}",
            provider.DisplayName, template.Name, imagePath);

        var response = await provider.ChatAsync(request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(response))
        {
            _logger.LogWarning(
                "Chat response empty. Provider: {Provider}, template: {Template}, model: {Model}",
                provider.DisplayName,
                template.Name,
                request.Model);
        }

        return response;
    }

    public async Task<Dictionary<AIProviderType, bool>> ValidateAllProvidersAsync()
    {
        var results = new Dictionary<AIProviderType, bool>();

        foreach (var provider in _providers)
        {
            var isValid = await provider.ValidateConfigurationAsync().ConfigureAwait(false);
            results[provider.ProviderType] = isValid;
        }

        return results;
    }

    public PromptManagementService GetPromptService() => _promptService;

    private AIChatRequest BuildChatRequest(
        AIProviderType providerType,
        PromptTemplate template,
        string userPrompt,
        string? modelId)
    {
        var messages = new List<AIChatMessage>();

        if (!string.IsNullOrEmpty(template.SystemPrompt))
        {
            messages.Add(new AIChatMessage(AIChatRole.System, template.SystemPrompt));
        }

        messages.Add(new AIChatMessage(AIChatRole.User, userPrompt));

        return new AIChatRequest
        {
            Model = ResolveTextModel(providerType, modelId),
            Messages = messages,
            Temperature = template.ExecutionSettings.Temperature,
            TopP = template.ExecutionSettings.TopP,
            MaxTokens = template.ExecutionSettings.MaxTokens
        };
    }

    private string ResolveTextModel(AIProviderType providerType, string? overrideModel)
    {
        if (!string.IsNullOrWhiteSpace(overrideModel))
        {
            return overrideModel;
        }

        var config = _configMonitor.CurrentValue;
        if (config.Defaults.Text.Provider == providerType && !string.IsNullOrWhiteSpace(config.Defaults.Text.Model))
        {
            return config.Defaults.Text.Model;
        }

        var providerConfig = providerType switch
        {
            AIProviderType.Qwen => config.Providers.Qwen,
            AIProviderType.Volcengine => config.Providers.Volcengine,
            _ => null
        };

        if (providerConfig == null || string.IsNullOrWhiteSpace(providerConfig.DefaultModels.Text))
        {
            throw new InvalidOperationException($"No default text model configured for {providerType}.");
        }

        return providerConfig.DefaultModels.Text;
    }
}

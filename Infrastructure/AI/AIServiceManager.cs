using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Storyboard.AI.Core;
using Storyboard.AI.Prompts;
using Storyboard.AI.Functions;

namespace Storyboard.AI;

/// <summary>
/// AI服务管理器 - 统一管理所有AI服务
/// </summary>
public class AIServiceManager
{
    private readonly ILogger<AIServiceManager> _logger;
    private readonly IEnumerable<IAIServiceProvider> _providers;
    private readonly PromptManagementService _promptService;
    private readonly FunctionManagementService _functionService;
    private IAIServiceProvider? _currentProvider;

    public AIServiceManager(
        ILogger<AIServiceManager> logger,
        IEnumerable<IAIServiceProvider> providers,
        PromptManagementService promptService,
        FunctionManagementService functionService)
    {
        _logger = logger;
        _providers = providers;
        _promptService = promptService;
        _functionService = functionService;
    }

    /// <summary>
    /// 初始化服务
    /// </summary>
    public async Task InitializeAsync()
    {
        await _promptService.LoadAllTemplatesAsync();
        _logger.LogInformation("AI服务管理器初始化完成");
    }

    /// <summary>
    /// 获取所有可用的提供商
    /// </summary>
    public IEnumerable<IAIServiceProvider> GetAvailableProviders()
    {
        return _providers.Where(p => p.IsConfigured);
    }

    /// <summary>
    /// 设置当前提供商
    /// </summary>
    public void SetProvider(AIProviderType providerType)
    {
        _currentProvider = _providers.FirstOrDefault(p => p.ProviderType == providerType);
        if (_currentProvider == null)
        {
            throw new InvalidOperationException($"未找到提供商: {providerType}");
        }
        _logger.LogInformation("切换到提供商: {Provider}", _currentProvider.DisplayName);
    }

    /// <summary>
    /// 获取当前提供商
    /// </summary>
    public IAIServiceProvider GetCurrentProvider()
    {
        if (_currentProvider == null)
        {
            var firstAvailable = GetAvailableProviders().FirstOrDefault();
            if (firstAvailable == null)
            {
                throw new InvalidOperationException("没有可用的AI服务提供商");
            }
            _currentProvider = firstAvailable;
        }
        return _currentProvider;
    }

    /// <summary>
    /// 执行聊天 - 使用提示词模板
    /// </summary>
    public async Task<string> ChatAsync(
        string promptTemplateId,
        Dictionary<string, object> parameters,
        string? modelId = null,
        CancellationToken cancellationToken = default)
    {
        var provider = GetCurrentProvider();
        var template = _promptService.GetTemplate(promptTemplateId);
        
        if (template == null)
        {
            throw new ArgumentException($"未找到提示词模板: {promptTemplateId}");
        }

        var kernel = await provider.GetKernelAsync(modelId);
        _functionService.AddPluginsToKernel(kernel);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();

        if (!string.IsNullOrEmpty(template.SystemPrompt))
        {
            chatHistory.AddSystemMessage(template.SystemPrompt);
        }

        var userPrompt = _promptService.RenderPrompt(template, parameters);
        chatHistory.AddUserMessage(userPrompt);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = template.ExecutionSettings.Temperature,
            TopP = template.ExecutionSettings.TopP,
            MaxTokens = template.ExecutionSettings.MaxTokens,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        _logger.LogInformation("发送聊天请求 - 提供商: {Provider}, 模板: {Template}", 
            provider.DisplayName, template.Name);

        var response = await chatService.GetChatMessageContentsAsync(
            chatHistory,
            executionSettings,
            kernel,
            cancellationToken);

        return response[0].Content ?? string.Empty;
    }

    /// <summary>
    /// 执行流式聊天 - 使用提示词模板
    /// </summary>
    public async IAsyncEnumerable<string> ChatStreamAsync(
        string promptTemplateId,
        Dictionary<string, object> parameters,
        string? modelId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var provider = GetCurrentProvider();
        var template = _promptService.GetTemplate(promptTemplateId);
        
        if (template == null)
        {
            throw new ArgumentException($"未找到提示词模板: {promptTemplateId}");
        }

        var kernel = await provider.GetKernelAsync(modelId);
        _functionService.AddPluginsToKernel(kernel);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();

        if (!string.IsNullOrEmpty(template.SystemPrompt))
        {
            chatHistory.AddSystemMessage(template.SystemPrompt);
        }

        var userPrompt = _promptService.RenderPrompt(template, parameters);
        chatHistory.AddUserMessage(userPrompt);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = template.ExecutionSettings.Temperature,
            TopP = template.ExecutionSettings.TopP,
            MaxTokens = template.ExecutionSettings.MaxTokens,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        _logger.LogInformation("发送流式聊天请求 - 提供商: {Provider}, 模板: {Template}", 
            provider.DisplayName, template.Name);

        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
            chatHistory,
            executionSettings,
            kernel,
            cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                yield return chunk.Content;
            }
        }
    }

    /// <summary>
    /// 直接执行聊天 - 不使用模板
    /// </summary>
    public async Task<string> ChatDirectAsync(
        string userMessage,
        string? systemMessage = null,
        string? modelId = null,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        var provider = GetCurrentProvider();
        var kernel = await provider.GetKernelAsync(modelId);
        _functionService.AddPluginsToKernel(kernel);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();

        if (!string.IsNullOrEmpty(systemMessage))
        {
            chatHistory.AddSystemMessage(systemMessage);
        }

        chatHistory.AddUserMessage(userMessage);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = temperature,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var response = await chatService.GetChatMessageContentsAsync(
            chatHistory,
            executionSettings,
            kernel,
            cancellationToken);

        return response[0].Content ?? string.Empty;
    }

    /// <summary>
    /// 验证所有提供商配置
    /// </summary>
    public async Task<Dictionary<AIProviderType, bool>> ValidateAllProvidersAsync()
    {
        var results = new Dictionary<AIProviderType, bool>();

        foreach (var provider in _providers)
        {
            var isValid = await provider.ValidateConfigurationAsync();
            results[provider.ProviderType] = isValid;
        }

        return results;
    }

    /// <summary>
    /// 获取提示词管理服务
    /// </summary>
    public PromptManagementService GetPromptService() => _promptService;

    /// <summary>
    /// 获取函数管理服务
    /// </summary>
    public FunctionManagementService GetFunctionService() => _functionService;
}

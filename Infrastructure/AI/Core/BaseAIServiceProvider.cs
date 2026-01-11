using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace Storyboard.AI.Core;

/// <summary>
/// AI服务提供商基类
/// </summary>
public abstract class BaseAIServiceProvider : IAIServiceProvider
{
    protected readonly ILogger Logger;

    protected BaseAIServiceProvider(ILogger logger)
    {
        Logger = logger;
    }

    public abstract AIProviderType ProviderType { get; }
    public abstract string DisplayName { get; }
    public abstract bool IsConfigured { get; }
    public abstract IReadOnlyList<string> SupportedModels { get; }

    public virtual async Task<Kernel> GetKernelAsync(string? modelId = null)
    {
        // Always create a fresh kernel so runtime config edits (appsettings.json reload)
        // take effect immediately.
        return await CreateKernelAsync(modelId);
    }

    public abstract Task<bool> ValidateConfigurationAsync();

    protected abstract Task<Kernel> CreateKernelAsync(string? modelId = null);

    /// <summary>
    /// 创建HttpClient
    /// </summary>
    protected virtual HttpClient CreateHttpClient(string endpoint, int timeoutSeconds = 120)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(endpoint),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        return httpClient;
    }
}

using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace 分镜大师.AI.Core;

/// <summary>
/// AI服务提供商基类
/// </summary>
public abstract class BaseAIServiceProvider : IAIServiceProvider
{
    protected readonly ILogger Logger;
    protected Kernel? _kernel;

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
        if (_kernel == null)
        {
            _kernel = await CreateKernelAsync(modelId);
        }
        return _kernel;
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

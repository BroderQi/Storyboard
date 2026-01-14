using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace Storyboard.AI.Core;

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
    public virtual AIProviderCapability Capabilities => AIProviderCapability.TextUnderstanding;
    public virtual IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.TextUnderstanding, "MaxTokens: 2000", "text/plain")
    };

    public abstract Task<string> ChatAsync(AIChatRequest request, CancellationToken cancellationToken = default);
    public abstract IAsyncEnumerable<string> ChatStreamAsync(AIChatRequest request, CancellationToken cancellationToken = default);
    public abstract Task<bool> ValidateConfigurationAsync(CancellationToken cancellationToken = default);

    protected virtual HttpClient CreateHttpClient(string endpoint, int timeoutSeconds = 120)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("Endpoint is required.");
        }

        var normalizedEndpoint = endpoint.EndsWith("/", StringComparison.Ordinal)
            ? endpoint
            : $"{endpoint}/";

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(normalizedEndpoint),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        return httpClient;
    }
}

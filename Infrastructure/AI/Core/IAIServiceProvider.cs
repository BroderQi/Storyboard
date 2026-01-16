namespace Storyboard.AI.Core;

/// <summary>
/// AI provider types.
/// </summary>
public enum AIProviderType
{
    /// <summary>Qwen</summary>
    Qwen,
    /// <summary>Volcengine</summary>
    Volcengine
}

[Flags]
public enum AIProviderCapability
{
    TextUnderstanding = 1,
    ImageGeneration = 2,
    VideoGeneration = 4
}

public sealed record ProviderCapabilityDeclaration(
    AIProviderCapability Capability,
    string InputLimit,
    string OutputFormat);

public enum AIChatRole
{
    System,
    User,
    Assistant
}

public enum MessageContentType
{
    Text,
    ImageUrl,
    ImageBase64
}

public sealed record MessageContent(
    MessageContentType Type,
    string? Text = null,
    string? ImageUrl = null,
    string? ImageBase64 = null);

public sealed class AIChatMessage
{
    public AIChatRole Role { get; init; }
    public string? Content { get; init; }
    public IReadOnlyList<MessageContent>? MultimodalContent { get; init; }

    public AIChatMessage(AIChatRole role, string content)
    {
        Role = role;
        Content = content;
    }

    public AIChatMessage(AIChatRole role, IReadOnlyList<MessageContent> multimodalContent)
    {
        Role = role;
        MultimodalContent = multimodalContent;
    }

    public bool IsMultimodal => MultimodalContent != null && MultimodalContent.Count > 0;
}

public sealed class AIChatRequest
{
    public string Model { get; init; } = string.Empty;
    public IReadOnlyList<AIChatMessage> Messages { get; init; } = Array.Empty<AIChatMessage>();
    public double Temperature { get; init; } = 0.7;
    public double TopP { get; init; } = 0.95;
    public int MaxTokens { get; init; } = 2000;
}

/// <summary>
/// AI provider interface.
/// </summary>
public interface IAIServiceProvider
{
    /// <summary>
    /// Provider type.
    /// </summary>
    AIProviderType ProviderType { get; }

    /// <summary>
    /// Provider display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether the provider is configured.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Supported models.
    /// </summary>
    IReadOnlyList<string> SupportedModels { get; }

    /// <summary>
    /// Supported capabilities.
    /// </summary>
    AIProviderCapability Capabilities { get; }

    /// <summary>
    /// Capability declarations.
    /// </summary>
    IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations { get; }

    /// <summary>
    /// Send a chat request.
    /// </summary>
    Task<string> ChatAsync(AIChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream a chat request.
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(AIChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate configuration.
    /// </summary>
    Task<bool> ValidateConfigurationAsync(CancellationToken cancellationToken = default);
}

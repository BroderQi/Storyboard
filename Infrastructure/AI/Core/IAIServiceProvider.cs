using Microsoft.SemanticKernel;

namespace Storyboard.AI.Core;

/// <summary>
/// AI服务提供商类型
/// </summary>
public enum AIProviderType
{
    /// <summary>文心一言</summary>
    Wenxin,
    /// <summary>通义千问</summary>
    Qwen,
    /// <summary>智谱AI</summary>
    Zhipu,
    /// <summary>火山引擎</summary>
    Volcengine,
    /// <summary>OpenAI</summary>
    OpenAI,
    /// <summary>Azure OpenAI</summary>
    AzureOpenAI
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

/// <summary>
/// AI服务提供商接口
/// </summary>
public interface IAIServiceProvider
{
    /// <summary>
    /// 提供商类型
    /// </summary>
    AIProviderType ProviderType { get; }

    /// <summary>
    /// 提供商显示名称
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 是否已配置
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// 支持的模型列表
    /// </summary>
    IReadOnlyList<string> SupportedModels { get; }

    /// <summary>
    /// 提供商支持的能力类型
    /// </summary>
    AIProviderCapability Capabilities { get; }

    /// <summary>
    /// 提供商能力声明
    /// </summary>
    IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations { get; }

    /// <summary>
    /// 获取Kernel实例
    /// </summary>
    Task<Kernel> GetKernelAsync(string? modelId = null);

    /// <summary>
    /// 验证配置
    /// </summary>
    Task<bool> ValidateConfigurationAsync();
}

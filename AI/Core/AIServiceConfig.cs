namespace Storyboard.AI.Core;

/// <summary>
/// AI服务配置基类
/// </summary>
public abstract class AIServiceConfig
{
    /// <summary>
    /// API密钥
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 默认模型ID
    /// </summary>
    public string DefaultModel { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// 文心一言配置
/// </summary>
public class WenxinConfig : AIServiceConfig
{
    public string ApiSecret { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://aip.baidubce.com";
}

/// <summary>
/// 通义千问配置
/// </summary>
public class QwenConfig : AIServiceConfig
{
    public string Endpoint { get; set; } = "https://dashscope.aliyuncs.com/api/v1";
}

/// <summary>
/// 智谱AI配置
/// </summary>
public class ZhipuConfig : AIServiceConfig
{
    public string Endpoint { get; set; } = "https://open.bigmodel.cn/api/paas/v4";
}

/// <summary>
/// 火山引擎配置
/// </summary>
public class VolcengineConfig : AIServiceConfig
{
    public string Endpoint { get; set; } = "https://ark.cn-beijing.volces.com/api/v3";
    public string EndpointId { get; set; } = string.Empty;
}

/// <summary>
/// OpenAI配置
/// </summary>
public class OpenAIConfig : AIServiceConfig
{
    public string Endpoint { get; set; } = "https://api.openai.com/v1";
    public string Organization { get; set; } = string.Empty;
}

/// <summary>
/// Azure OpenAI配置
/// </summary>
public class AzureOpenAIConfig : AIServiceConfig
{
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2024-02-15-preview";
}

/// <summary>
/// AI服务总配置
/// </summary>
public class AIServicesConfiguration
{
    public WenxinConfig Wenxin { get; set; } = new();
    public QwenConfig Qwen { get; set; } = new();
    public ZhipuConfig Zhipu { get; set; } = new();
    public VolcengineConfig Volcengine { get; set; } = new();
    public OpenAIConfig OpenAI { get; set; } = new();
    public AzureOpenAIConfig AzureOpenAI { get; set; } = new();

    /// <summary>
    /// 默认提供商
    /// </summary>
    public AIProviderType DefaultProvider { get; set; } = AIProviderType.Qwen;
}

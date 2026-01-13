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
/// DeepSeek 配置
/// </summary>
public class DeepSeekConfig : AIServiceConfig
{
    public string Endpoint { get; set; } = "https://api.deepseek.com";
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
/// Gemini 配置
/// </summary>
public class GeminiConfig : AIServiceConfig
{
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}

public class LocalImageConfig
{
    public bool Enabled { get; set; } = true;
    public int Width { get; set; } = 1024;
    public int Height { get; set; } = 576;
    public string Style { get; set; } = "Poster";
}

public class OpenAIImageConfig : AIServiceConfig
{
    public string Endpoint { get; set; } = "https://api.openai.com/v1";
    public string Size { get; set; } = "1024x1024";
    public string Quality { get; set; } = "standard";
}

public class GeminiImageConfig : AIServiceConfig
{
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public string ResponseMimeType { get; set; } = "image/png";
}

public class StableDiffusionApiImageConfig : AIServiceConfig
{
    public string Endpoint { get; set; } = "https://stablediffusionapi.com/api/v3";
    public string NegativePrompt { get; set; } = "low quality";
}

public class ImageServicesConfiguration
{
    public ImageProviderType DefaultProvider { get; set; } = ImageProviderType.Local;
    public LocalImageConfig Local { get; set; } = new();
    public OpenAIImageConfig OpenAI { get; set; } = new();
    public GeminiImageConfig Gemini { get; set; } = new();
    public StableDiffusionApiImageConfig StableDiffusionApi { get; set; } = new();
}

public class LocalVideoConfig
{
    public bool Enabled { get; set; } = true;
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public int Fps { get; set; } = 30;
    public int BitrateKbps { get; set; } = 4000;
    public double TransitionSeconds { get; set; } = 0.5;
    public bool UseKenBurns { get; set; } = true;
}

public class VideoServicesConfiguration
{
    public VideoProviderType DefaultProvider { get; set; } = VideoProviderType.Local;
    public LocalVideoConfig Local { get; set; } = new();
    public OpenAIVideoConfig OpenAI { get; set; } = new();
    public GeminiVideoConfig Gemini { get; set; } = new();
    public StableDiffusionApiVideoConfig StableDiffusionApi { get; set; } = new();
}

public class OpenAIVideoConfig : AIServiceConfig
{
    public string Endpoint { get; set; } = "https://api.openai.com/v1";
}

public class GeminiVideoConfig : AIServiceConfig
{
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}

public class StableDiffusionApiVideoConfig : AIServiceConfig
{
    public string Endpoint { get; set; } = "https://stablediffusionapi.com/api/v5";
    public string NegativePrompt { get; set; } = "low quality";
    public string Scheduler { get; set; } = "UniPCMultistepScheduler";
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
    public DeepSeekConfig DeepSeek { get; set; } = new();
    public OpenAIConfig OpenAI { get; set; } = new();
    public AzureOpenAIConfig AzureOpenAI { get; set; } = new();
    public GeminiConfig Gemini { get; set; } = new();
    public ImageServicesConfiguration Image { get; set; } = new();
    public VideoServicesConfiguration Video { get; set; } = new();

    /// <summary>
    /// 默认提供商
    /// </summary>
    public AIProviderType DefaultProvider { get; set; } = AIProviderType.Qwen;
}

namespace Storyboard.AI.Core;

/// <summary>
/// Shared config base for image/video providers.
/// </summary>
public abstract class AIServiceConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 120;
}

public sealed class AIProviderModelDefaults
{
    public string Text { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Video { get; set; } = string.Empty;
}

public sealed class AIProviderConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 120;
    public AIProviderModelDefaults DefaultModels { get; set; } = new();
}

public sealed class AIProvidersConfiguration
{
    public AIProviderConfiguration Qwen { get; set; } = new();
    public AIProviderConfiguration Volcengine { get; set; } = new();
}

public sealed class AIServiceDefaultSelection
{
    public AIProviderType Provider { get; set; } = AIProviderType.Qwen;
    public string Model { get; set; } = string.Empty;
}

public sealed class AIServiceDefaults
{
    public AIServiceDefaultSelection Text { get; set; } = new();
    public AIServiceDefaultSelection Image { get; set; } = new();
    public AIServiceDefaultSelection Video { get; set; } = new();
}

public sealed class AIServicesConfiguration
{
    public AIProvidersConfiguration Providers { get; set; } = new();
    public AIServiceDefaults Defaults { get; set; } = new();

    public ImageServicesConfiguration Image { get; set; } = new();
    public VideoServicesConfiguration Video { get; set; } = new();
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

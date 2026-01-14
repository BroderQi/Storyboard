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

public sealed class VolcengineImageConfig
{
    public string Size { get; set; } = "2K";
    public string ResponseFormat { get; set; } = "b64_json";
    public bool Watermark { get; set; } = false;
    public bool Stream { get; set; } = false;
    public string SequentialImageGeneration { get; set; } = string.Empty;
    public int? SequentialMaxImages { get; set; }
    public string OptimizePromptMode { get; set; } = string.Empty;
}

public class ImageServicesConfiguration
{
    public ImageProviderType DefaultProvider { get; set; } = ImageProviderType.Volcengine;
    public VolcengineImageConfig Volcengine { get; set; } = new();
}

public sealed class VolcengineVideoConfig
{
    public string Resolution { get; set; } = "1080p";
    public string Ratio { get; set; } = string.Empty;
    public double DurationSeconds { get; set; } = 0;
    public int? Frames { get; set; }
    public int? Seed { get; set; }
    public bool? CameraFixed { get; set; }
    public bool Watermark { get; set; } = false;
    public bool ReturnLastFrame { get; set; } = false;
    public string ServiceTier { get; set; } = "default";
    public bool GenerateAudio { get; set; } = false;
    public bool Draft { get; set; } = false;
}

public class VideoServicesConfiguration
{
    public VideoProviderType DefaultProvider { get; set; } = VideoProviderType.Volcengine;
    public VolcengineVideoConfig Volcengine { get; set; } = new();
}

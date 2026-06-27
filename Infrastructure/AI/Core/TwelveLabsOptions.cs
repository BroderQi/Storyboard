namespace Storyboard.AI.Core;

/// <summary>
/// TwelveLabs (Pegasus) 视频理解配置。
/// 该集成默认关闭：仅当 <see cref="ApiKey"/> 非空时才会用 Pegasus 丰富分镜描述，
/// 否则保持原有的本地启发式分析行为不变（非破坏性）。
///
/// TwelveLabs (Pegasus) video-understanding options. This integration is opt-in:
/// Pegasus is only used to enrich shot descriptions when <see cref="ApiKey"/> is set;
/// otherwise the existing local heuristic analysis is used unchanged.
/// </summary>
public sealed class TwelveLabsOptions
{
    /// <summary>环境变量名：用于读取 API Key，避免把密钥写入仓库配置。</summary>
    public const string ApiKeyEnvVar = "TWELVELABS_API_KEY";

    /// <summary>TwelveLabs API Key。留空则禁用集成。优先从环境变量读取。</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>REST 基地址。</summary>
    public string Endpoint { get; set; } = "https://api.twelvelabs.io/v1.3";

    /// <summary>Pegasus 模型名。</summary>
    public string Model { get; set; } = "pegasus1.5";

    /// <summary>单次请求超时（秒）。视频上传 + 分析可能较慢。</summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 直传上传体积上限（字节）。TwelveLabs direct 上传上限为 200MB；
    /// 超过则跳过 Pegasus 丰富、保留启发式结果。
    /// </summary>
    public long MaxUploadBytes { get; set; } = 200L * 1024 * 1024;

    /// <summary>是否启用集成。</summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// 从环境变量构造配置；未设置 <see cref="ApiKeyEnvVar"/> 时返回禁用状态。
    /// </summary>
    public static TwelveLabsOptions FromEnvironment()
    {
        return new TwelveLabsOptions
        {
            ApiKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar)?.Trim() ?? string.Empty
        };
    }
}

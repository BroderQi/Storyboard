namespace Storyboard.AI.Prompts;

/// <summary>
/// 提示词模板
/// </summary>
public class PromptTemplate
{
    /// <summary>
    /// 模板ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 模板名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模板描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 系统提示词
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// 用户提示词模板
    /// </summary>
    public string UserPromptTemplate { get; set; } = string.Empty;

    /// <summary>
    /// 参数定义
    /// </summary>
    public Dictionary<string, PromptParameter> Parameters { get; set; } = new();

    /// <summary>
    /// 执行设置
    /// </summary>
    public PromptExecutionSettings ExecutionSettings { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 提示词参数
/// </summary>
public class PromptParameter
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 参数类型
    /// </summary>
    public string Type { get; set; } = "string";

    /// <summary>
    /// 参数描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// 默认值
    /// </summary>
    public string? DefaultValue { get; set; }
}

/// <summary>
/// 提示词执行设置
/// </summary>
public class PromptExecutionSettings
{
    /// <summary>
    /// 温度
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Top P
    /// </summary>
    public double TopP { get; set; } = 0.95;

    /// <summary>
    /// 最大Token数
    /// </summary>
    public int MaxTokens { get; set; } = 2000;

    /// <summary>
    /// 停止序列
    /// </summary>
    public List<string> StopSequences { get; set; } = new();
}

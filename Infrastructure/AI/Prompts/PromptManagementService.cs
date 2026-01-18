using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Storyboard.AI.Prompts;

/// <summary>
/// 提示词管理服务
/// </summary>
public class PromptManagementService
{
    private readonly ILogger<PromptManagementService> _logger;
    private readonly string _promptsDirectory;
    private readonly Dictionary<string, PromptTemplate> _templates = new();

    public PromptManagementService(ILogger<PromptManagementService> logger)
    {
        _logger = logger;
        _promptsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts");
        
        if (!Directory.Exists(_promptsDirectory))
        {
            Directory.CreateDirectory(_promptsDirectory);
            _logger.LogInformation("创建提示词目录: {Directory}", _promptsDirectory);
        }
    }

    /// <summary>
    /// 加载所有提示词模板
    /// </summary>
    public async Task LoadAllTemplatesAsync()
    {
        _templates.Clear();

        var files = Directory.GetFiles(_promptsDirectory, "*.json");
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var template = JsonSerializer.Deserialize<PromptTemplate>(json);
                if (template != null)
                {
                    _templates[template.Id] = template;
                    _logger.LogInformation("加载提示词模板: {Name} ({Id})", template.Name, template.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载提示词模板失败: {File}", file);
            }
        }

        await EnsureDefaultTemplatesAsync();
    }

    /// <summary>
    /// 获取提示词模板
    /// </summary>
    public PromptTemplate? GetTemplate(string id)
    {
        return _templates.TryGetValue(id, out var template) ? template : null;
    }

    /// <summary>
    /// 获取所有模板
    /// </summary>
    public IReadOnlyList<PromptTemplate> GetAllTemplates()
    {
        return _templates.Values.ToList();
    }

    /// <summary>
    /// 保存或更新模板
    /// </summary>
    public async Task SaveTemplateAsync(PromptTemplate template)
    {
        template.UpdatedAt = DateTime.Now;
        _templates[template.Id] = template;

        var filePath = Path.Combine(_promptsDirectory, $"{template.Id}.json");
        var json = JsonSerializer.Serialize(template, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        
        await File.WriteAllTextAsync(filePath, json);
        _logger.LogInformation("保存提示词模板: {Name} ({Id})", template.Name, template.Id);
    }

    /// <summary>
    /// 删除模板
    /// </summary>
    public async Task DeleteTemplateAsync(string id)
    {
        if (_templates.Remove(id))
        {
            var filePath = Path.Combine(_promptsDirectory, $"{id}.json");
            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
                _logger.LogInformation("删除提示词模板: {Id}", id);
            }
        }
    }

    /// <summary>
    /// 渲染提示词
    /// </summary>
    public string RenderPrompt(PromptTemplate template, Dictionary<string, object> parameters)
    {
        var prompt = template.UserPromptTemplate;

        foreach (var param in template.Parameters)
        {
            var value = parameters.TryGetValue(param.Key, out var v)
                ? v?.ToString()
                : param.Value.DefaultValue;

            if (param.Value.Required && string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"必需参数缺失: {param.Key}");
            }

            prompt = prompt.Replace($"{{{{{param.Key}}}}}", value ?? string.Empty);
        }

        return prompt;
    }

    /// <summary>
    /// 渲染提示词（带创作意图注入）
    /// </summary>
    public string RenderPromptWithIntent(
        PromptTemplate template,
        Dictionary<string, object> parameters,
        string? creativeGoal = null,
        string? targetAudience = null,
        string? videoTone = null,
        string? keyMessage = null)
    {
        var basePrompt = RenderPrompt(template, parameters);

        // 如果没有任何创作意图，直接返回基础 Prompt
        if (string.IsNullOrWhiteSpace(creativeGoal) &&
            string.IsNullOrWhiteSpace(targetAudience) &&
            string.IsNullOrWhiteSpace(videoTone) &&
            string.IsNullOrWhiteSpace(keyMessage))
        {
            return basePrompt;
        }

        // 构建创作意图上下文
        var intentContext = "\n\n【创作意图】\n";

        if (!string.IsNullOrWhiteSpace(creativeGoal))
            intentContext += $"创作目标：{creativeGoal}\n";

        if (!string.IsNullOrWhiteSpace(targetAudience))
            intentContext += $"目标受众：{targetAudience}\n";

        if (!string.IsNullOrWhiteSpace(videoTone))
            intentContext += $"视频基调：{videoTone}\n";

        if (!string.IsNullOrWhiteSpace(keyMessage))
            intentContext += $"核心信息：{keyMessage}\n";

        intentContext += "\n请在生成内容时充分考虑以上创作意图，确保输出与创作目标、受众特征、视频基调和核心信息保持一致。";

        _logger.LogInformation("注入创作意图到 Prompt: {TemplateId}", template.Id);

        return basePrompt + intentContext;
    }

    /// <summary>
    /// 加载默认提示词模板
    /// </summary>
    private async Task EnsureDefaultTemplatesAsync()
    {
        // 视频分析提示词
        if (!_templates.ContainsKey("video_analysis"))
        {
            var videoAnalysisTemplate = new PromptTemplate
            {
                Id = "video_analysis",
                Name = "视频分析",
                Description = "分析视频内容并生成分镜脚本",
                SystemPrompt = @"你是一个专业的视频分析和分镜师。你的任务是：
1. 分析视频的视觉内容和叙事结构
2. 识别关键场景和转换点
3. 为每个镜头提供详细的描述
4. 给出专业的拍摄建议

请以专业、准确、富有创意的方式完成任务。",
                UserPromptTemplate = @"请分析以下视频信息：

视频时长: {{duration}}
视频帧数: {{frames}}
视频分辨率: {{resolution}}

请为这个视频生成详细的分镜脚本，包括：
- 镜头编号
- 时间范围
- 场景描述
- 运镜方式
- 音效建议

返回JSON格式的结果。",
                Parameters = new Dictionary<string, PromptParameter>
                {
                    ["duration"] = new() { Name = "duration", Type = "string", Description = "视频时长", Required = true },
                    ["frames"] = new() { Name = "frames", Type = "number", Description = "视频帧数", Required = true },
                    ["resolution"] = new() { Name = "resolution", Type = "string", Description = "视频分辨率", Required = true }
                },
                ExecutionSettings = new PromptExecutionSettings
                {
                    Temperature = 0.7,
                    TopP = 0.9,
                    MaxTokens = 3000
                }
            };
            await SaveTemplateAsync(videoAnalysisTemplate);
        }

        // 图像生成提示词
        if (!_templates.ContainsKey("image_generation"))
        {
            var imageGenerationTemplate = new PromptTemplate
            {
                Id = "image_generation",
                Name = "图像生成",
                Description = "根据场景描述生成图像提示词",
                SystemPrompt = @"你是一个专业的AI图像生成提示词工程师。你的任务是：
1. 将场景描述转换为详细的图像生成提示词
2. 包含风格、构图、光照、色彩等细节
3. 使用专业的摄影和艺术术语
4. 确保提示词清晰、具体、易于理解

请生成高质量的图像提示词。",
                UserPromptTemplate = @"场景描述: {{scene_description}}
镜头类型: {{shot_type}}
风格要求: {{style}}

请生成详细的图像生成提示词（中英文双语）。",
                Parameters = new Dictionary<string, PromptParameter>
                {
                    ["scene_description"] = new() { Name = "scene_description", Type = "string", Description = "场景描述", Required = true },
                    ["shot_type"] = new() { Name = "shot_type", Type = "string", Description = "镜头类型", DefaultValue = "中景" },
                    ["style"] = new() { Name = "style", Type = "string", Description = "风格要求", DefaultValue = "写实" }
                },
                ExecutionSettings = new PromptExecutionSettings
                {
                    Temperature = 0.8,
                    TopP = 0.95,
                    MaxTokens = 1000
                }
            };
            await SaveTemplateAsync(imageGenerationTemplate);
        }

        // 文案优化提示词
        if (!_templates.ContainsKey("copywriting_optimization"))
        {
            var copywritingTemplate = new PromptTemplate
            {
                Id = "copywriting_optimization",
                Name = "文案优化",
                Description = "优化和润色文案内容",
                SystemPrompt = @"你是一个专业的文案策划和内容优化师。你的任务是：
1. 优化文案的表达和结构
2. 提升文案的吸引力和感染力
3. 保持原意的同时增强表现力
4. 确保语言流畅、准确、生动

请提供优质的文案优化服务。",
                UserPromptTemplate = @"原始文案: {{original_text}}
优化方向: {{optimization_goal}}
目标受众: {{target_audience}}

请优化这段文案，使其更加{{optimization_goal}}。",
                Parameters = new Dictionary<string, PromptParameter>
                {
                    ["original_text"] = new() { Name = "original_text", Type = "string", Description = "原始文案", Required = true },
                    ["optimization_goal"] = new() { Name = "optimization_goal", Type = "string", Description = "优化方向", DefaultValue = "专业且吸引人" },
                    ["target_audience"] = new() { Name = "target_audience", Type = "string", Description = "目标受众", DefaultValue = "普通观众" }
                },
                ExecutionSettings = new PromptExecutionSettings
                {
                    Temperature = 0.75,
                    TopP = 0.9,
                    MaxTokens = 1500
                }
            };
            await SaveTemplateAsync(copywritingTemplate);
        }

        // 镜头解析提示词（图 -> 文本）
        if (!_templates.ContainsKey("shot_analysis"))
        {
            var shotAnalysisTemplate = new PromptTemplate
            {
                Id = "shot_analysis",
                Name = "镜头解析",
                Description = "根据首尾帧特征生成结构化分镜描述",
                SystemPrompt = @"你是专业分镜师。请根据提供的图像特征与已有上下文，生成结构化分镜字段。
输出必须是严格 JSON 对象，仅包含以下字段：
shotType, coreContent, actionCommand, sceneSettings, firstFramePrompt, lastFramePrompt。",
                UserPromptTemplate = @"首帧特征：{{first_frame_features}}
尾帧特征：{{last_frame_features}}
已有信息：{{existing_context}}

请输出 JSON 对象：",
                Parameters = new Dictionary<string, PromptParameter>
                {
                    ["first_frame_features"] = new() { Name = "first_frame_features", Type = "string", Description = "首帧图片特征", Required = true },
                    ["last_frame_features"] = new() { Name = "last_frame_features", Type = "string", Description = "尾帧图片特征", Required = true },
                    ["existing_context"] = new() { Name = "existing_context", Type = "string", Description = "已有分镜上下文", Required = true }
                },
                ExecutionSettings = new PromptExecutionSettings
                {
                    Temperature = 0.6,
                    TopP = 0.9,
                    MaxTokens = 1200
                }
            };
            await SaveTemplateAsync(shotAnalysisTemplate);
        }

        // 文本生成分镜提示词
        if (!_templates.ContainsKey("text_to_shots"))
        {
            var textToShotsTemplate = new PromptTemplate
            {
                Id = "text_to_shots",
                Name = "文本生成分镜",
                Description = "根据故事描述生成分镜列表",
                SystemPrompt = @"你是专业编导与分镜师。请根据用户描述生成分镜列表。
输出必须是严格 JSON 数组，每个元素包含：
shotType, coreContent, actionCommand, sceneSettings, firstFramePrompt, lastFramePrompt, duration（秒）。
请确保分镜数量在 3-12 之间，内容具体可执行。",
                UserPromptTemplate = @"用户描述：{{story_text}}

请输出 JSON 数组：",
                Parameters = new Dictionary<string, PromptParameter>
                {
                    ["story_text"] = new() { Name = "story_text", Type = "string", Description = "用户故事描述", Required = true }
                },
                ExecutionSettings = new PromptExecutionSettings
                {
                    Temperature = 0.7,
                    TopP = 0.9,
                    MaxTokens = 2200
                }
            };
            await SaveTemplateAsync(textToShotsTemplate);
        }

        _logger.LogInformation("已加载默认提示词模板");
    }
}

using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace 分镜大师.AI.Functions;

/// <summary>
/// Function Call管理服务
/// </summary>
public class FunctionManagementService
{
    private readonly ILogger<FunctionManagementService> _logger;
    private readonly Dictionary<string, object> _plugins = new();

    public FunctionManagementService(ILogger<FunctionManagementService> logger)
    {
        _logger = logger;
        InitializeDefaultPlugins();
    }

    /// <summary>
    /// 初始化默认插件
    /// </summary>
    private void InitializeDefaultPlugins()
    {
        RegisterPlugin("VideoAnalysis", new VideoAnalysisFunctions());
        RegisterPlugin("SceneDescription", new SceneDescriptionFunctions());
        RegisterPlugin("ShotType", new ShotTypeFunctions());
        RegisterPlugin("Timecode", new TimecodeFunctions());
        
        _logger.LogInformation("默认插件已注册");
    }

    /// <summary>
    /// 注册插件
    /// </summary>
    public void RegisterPlugin(string name, object plugin)
    {
        _plugins[name] = plugin;
        _logger.LogInformation("注册插件: {PluginName}", name);
    }

    /// <summary>
    /// 移除插件
    /// </summary>
    public bool RemovePlugin(string name)
    {
        if (_plugins.Remove(name))
        {
            _logger.LogInformation("移除插件: {PluginName}", name);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 获取所有插件
    /// </summary>
    public IReadOnlyDictionary<string, object> GetAllPlugins()
    {
        return _plugins;
    }

    /// <summary>
    /// 将插件添加到Kernel
    /// </summary>
    public void AddPluginsToKernel(Kernel kernel)
    {
        foreach (var (name, plugin) in _plugins)
        {
            try
            {
                kernel.ImportPluginFromObject(plugin, name);
                _logger.LogInformation("插件 {PluginName} 已添加到Kernel", name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加插件 {PluginName} 失败", name);
            }
        }
    }

    /// <summary>
    /// 获取插件信息
    /// </summary>
    public List<PluginInfo> GetPluginInfos()
    {
        var infos = new List<PluginInfo>();
        
        foreach (var (name, plugin) in _plugins)
        {
            var methods = plugin.GetType()
                .GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(KernelFunctionAttribute), false).Any())
                .ToList();

            var functions = methods.Select(m =>
            {
                var descAttr = m.GetCustomAttributes(typeof(DescriptionAttribute), false)
                    .FirstOrDefault() as DescriptionAttribute;
                
                var parameters = m.GetParameters().Select(p =>
                {
                    var paramDesc = p.GetCustomAttributes(typeof(DescriptionAttribute), false)
                        .FirstOrDefault() as DescriptionAttribute;
                    
                    return new FunctionParameterInfo
                    {
                        Name = p.Name ?? string.Empty,
                        Type = p.ParameterType.Name,
                        Description = paramDesc?.Description ?? string.Empty,
                        IsOptional = p.IsOptional,
                        DefaultValue = p.DefaultValue?.ToString()
                    };
                }).ToList();

                return new FunctionInfo
                {
                    Name = m.Name,
                    Description = descAttr?.Description ?? string.Empty,
                    Parameters = parameters
                };
            }).ToList();

            infos.Add(new PluginInfo
            {
                Name = name,
                Functions = functions
            });
        }

        return infos;
    }
}

/// <summary>
/// 插件信息
/// </summary>
public class PluginInfo
{
    public string Name { get; set; } = string.Empty;
    public List<FunctionInfo> Functions { get; set; } = new();
}

/// <summary>
/// 函数信息
/// </summary>
public class FunctionInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<FunctionParameterInfo> Parameters { get; set; } = new();
}

/// <summary>
/// 函数参数信息
/// </summary>
public class FunctionParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public string? DefaultValue { get; set; }
}

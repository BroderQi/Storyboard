using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using 分镜大师.AI.Core;
using 分镜大师.AI.Providers;
using 分镜大师.AI.Prompts;
using 分镜大师.AI.Functions;

namespace 分镜大师.AI;

/// <summary>
/// AI服务依赖注入扩展
/// </summary>
public static class AIServiceExtensions
{
    /// <summary>
    /// 添加AI服务
    /// </summary>
    public static IServiceCollection AddAIServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 配置
        services.Configure<AIServicesConfiguration>(
            configuration.GetSection("AIServices"));

        // 注册各个AI服务提供商
        services.AddSingleton<IAIServiceProvider, QwenServiceProvider>();
        services.AddSingleton<IAIServiceProvider, ZhipuServiceProvider>();
        services.AddSingleton<IAIServiceProvider, WenxinServiceProvider>();
        services.AddSingleton<IAIServiceProvider, VolcengineServiceProvider>();

        // 注册提示词管理服务
        services.AddSingleton<PromptManagementService>();

        // 注册函数管理服务
        services.AddSingleton<FunctionManagementService>();

        // 注册AI服务管理器
        services.AddSingleton<AIServiceManager>();

        return services;
    }
}

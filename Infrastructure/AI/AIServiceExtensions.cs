using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Storyboard.AI.Core;
using Storyboard.AI.Prompts;
using Storyboard.AI.Providers;

namespace Storyboard.AI;

public static class AIServiceExtensions
{
    public static IServiceCollection AddAIServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AIServicesConfiguration>(
            configuration.GetSection("AIServices"));

        services.AddSingleton<IAIServiceProvider, QwenServiceProvider>();
        services.AddSingleton<IAIServiceProvider, VolcengineServiceProvider>();
        services.AddSingleton<PromptManagementService>();
        services.AddSingleton<AIServiceManager>();

        return services;
    }
}

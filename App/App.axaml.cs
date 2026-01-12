using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Storyboard.ViewModels;
using Storyboard.Views;
using Storyboard.AI;
using Storyboard.AI.Core;
using Storyboard.Application.Abstractions;
using Storyboard.Application.Services;
using Storyboard.Infrastructure.Configuration;
using Storyboard.Infrastructure.DependencyInjection;
using Storyboard.Infrastructure.Services;
using Storyboard.Infrastructure.Ui;
using System.IO;
using System;

namespace Storyboard;

public partial class App : Avalonia.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 配置依赖注入
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<AIServicesConfiguration>(configuration.GetSection("AIServices"));

        // Persistence (SQLite + EF Core)
        var dbRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StoryboardStudio");
        Directory.CreateDirectory(dbRoot);
        var dbPath = Path.Combine(dbRoot, "storyboard.db");
        services.AddStoryboardPersistence(dbPath);

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
        });

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<ApiKeyViewModel>();

        // Services - 保持现有业务逻辑
        services.AddSingleton<VideoAnalysisService>();
        services.AddSingleton<IVideoAnalysisService>(sp => sp.GetRequiredService<VideoAnalysisService>());
        services.AddSingleton<IVideoMetadataService>(sp => sp.GetRequiredService<VideoAnalysisService>());
        services.AddSingleton<IFrameExtractionService, FrameExtractionService>();
        services.AddSingleton<IAiShotService, AiShotService>();
        services.AddSingleton<IImageGenerationService, ImageGenerationService>();
        services.AddSingleton<IVideoGenerationService, VideoGenerationService>();
        services.AddSingleton<IFinalRenderService, FinalRenderService>();
        services.AddSingleton<AppSettingsStore>();

        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddSingleton<IJobQueueService>(sp =>
            new JobQueueService(sp.GetRequiredService<IUiDispatcher>(), maxConcurrency: 2));

        // AI Services - 保持现有 AI 架构
        services.AddSingleton<AI.Prompts.PromptManagementService>();
        services.AddSingleton<AI.Functions.FunctionManagementService>();
        
        // AI Providers
        services.AddSingleton<AI.Core.IAIServiceProvider, AI.Providers.QwenServiceProvider>();
        services.AddSingleton<AI.Core.IAIServiceProvider, AI.Providers.ZhipuServiceProvider>();
        services.AddSingleton<AI.Core.IAIServiceProvider, AI.Providers.WenxinServiceProvider>();
        services.AddSingleton<AI.Core.IAIServiceProvider, AI.Providers.VolcengineServiceProvider>();
        
        services.AddSingleton<AIServiceManager>();
    }
}

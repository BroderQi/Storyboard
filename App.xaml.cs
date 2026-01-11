using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.IO;
using Storyboard.Services;
using Storyboard.ViewModels;
using Storyboard.AI;
using Storyboard.Views;

namespace Storyboard;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public IServiceProvider Services => _serviceProvider
        ?? throw new InvalidOperationException("ServiceProvider is not initialized.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 初始化AI服务
        var aiManager = _serviceProvider.GetRequiredService<AIServiceManager>();
        _ = aiManager.InitializeAsync();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(ServiceCollection services)
    {
        // Configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        // AI Services
        services.AddAIServices(configuration);

        // Services
        services.AddSingleton<IVideoAnalysisService, VideoAnalysisService>();
        services.AddSingleton<IImageGenerationService, ImageGenerationService>();
        services.AddSingleton<IVideoGenerationService, VideoGenerationService>();
        services.AddSingleton<IFinalRenderService, FinalRenderService>();
        services.AddSingleton<JobQueueService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();

        // Settings + ViewModels for pages
        services.AddSingleton<Storyboard.Services.AppSettingsStore>();
        services.AddTransient<Storyboard.ViewModels.ApiKeyViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}


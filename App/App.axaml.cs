using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaWebView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using Storyboard.ViewModels;
using Storyboard.Views;
using Storyboard.AI;
using Storyboard.AI.Core;
using Storyboard.Application.Abstractions;
using Storyboard.Application.Services;
using Storyboard.Infrastructure.Configuration;
using Storyboard.Infrastructure.DependencyInjection;
using Storyboard.Infrastructure.Media;
using Storyboard.Infrastructure.Media.Providers;
using Storyboard.Infrastructure.Services;
using Storyboard.Infrastructure.Ui;
using System.IO;
using System;
using CommunityToolkit.Mvvm.Messaging;

namespace Storyboard;

public partial class App : Avalonia.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        AvaloniaWebViewBuilder.Initialize(_ => { });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 配置依赖注入
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // 应用数据库迁移
        ApplyDatabaseMigrations().GetAwaiter().GetResult();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += (_, __) => Log.CloseAndFlush();
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async System.Threading.Tasks.Task ApplyDatabaseMigrations()
    {
        try
        {
            using var scope = Services.CreateScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Infrastructure.Persistence.StoryboardDbContext>>();
            var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<App>>();

            await using var context = await contextFactory.CreateDbContextAsync();
            await Infrastructure.Migrations.DatabaseMigrationHelper.ApplyIncrementalMigrationsAsync(context, logger);

            Log.Information("数据库迁移成功完成");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "数据库迁移失败");
            // 不抛出异常，允许应用继续启动
        }
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

        // Messenger for ViewModel communication
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        // Persistence (SQLite + EF Core)
        var dbRoot = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dbRoot);
        var dbPath = Path.Combine(dbRoot, "storyboard.db");
        services.AddStoryboardPersistence(dbPath);

        // Logging
        var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "app-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();
        services.AddLogging(builder =>
        {
            builder.AddSerilog(Log.Logger, dispose: true);
        });

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<ApiKeyViewModel>();

        // 新的子 ViewModels
        services.AddTransient<ViewModels.Project.ProjectManagementViewModel>();
        services.AddTransient<ViewModels.Queue.JobQueueViewModel>();
        services.AddTransient<ViewModels.Import.VideoImportViewModel>();
        services.AddTransient<ViewModels.Import.FrameExtractionViewModel>();
        services.AddTransient<ViewModels.Shot.ShotListViewModel>();
        services.AddTransient<ViewModels.Shot.TimelineViewModel>();
        services.AddTransient<ViewModels.Generation.AiAnalysisViewModel>();
        services.AddTransient<ViewModels.Generation.ImageGenerationViewModel>();
        services.AddTransient<ViewModels.Generation.VideoGenerationViewModel>();
        services.AddTransient<ViewModels.Generation.ExportViewModel>();
        services.AddTransient<ViewModels.Shared.HistoryViewModel>();

        // Services - 保持现有业务逻辑
        services.AddSingleton<VideoAnalysisService>();
        services.AddSingleton<IVideoAnalysisService>(sp => sp.GetRequiredService<VideoAnalysisService>());
        services.AddSingleton<IVideoMetadataService>(sp => sp.GetRequiredService<VideoAnalysisService>());
        services.AddSingleton<IFrameExtractionService, FrameExtractionService>();
        services.AddSingleton<IAiShotService, AiShotService>();
        services.AddSingleton<IImageGenerationProvider, VolcengineImageGenerationProvider>();
        services.AddSingleton<IImageGenerationService, ImageGenerationService>();
        services.AddSingleton<IVideoGenerationProvider, VolcengineVideoGenerationProvider>();
        services.AddSingleton<IVideoGenerationService, VideoGenerationService>();
        services.AddSingleton<IFinalRenderService, FinalRenderService>();
        services.AddSingleton<AppSettingsStore>();

        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddSingleton<IJobQueueService>(sp =>
            new JobQueueService(sp.GetRequiredService<IUiDispatcher>(), maxConcurrency: 2));

        // AI Services
        services.AddSingleton<AI.Prompts.PromptManagementService>();
        services.AddSingleton<AI.Core.IAIServiceProvider, AI.Providers.QwenServiceProvider>();
        services.AddSingleton<AI.Core.IAIServiceProvider, AI.Providers.VolcengineServiceProvider>();
        
        services.AddSingleton<AIServiceManager>();
    }
}

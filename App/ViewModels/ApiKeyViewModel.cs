using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Storyboard.AI;
using Storyboard.AI.Core;
using Storyboard.Models;
using Storyboard.Infrastructure.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels;

public partial class ApiKeyViewModel : ObservableObject
{
    private readonly AppSettingsStore _settingsStore;
    private readonly AIServiceManager _aiManager;

    public ApiKeyViewModel(AppSettingsStore settingsStore, AIServiceManager aiManager)
    {
        _settingsStore = settingsStore;
        _aiManager = aiManager;

        AvailableProviderTypes = Enum.GetValues(typeof(AIProviderType)).Cast<AIProviderType>().ToList();
        AvailableImageProviderTypes = Enum.GetValues(typeof(ImageProviderType)).Cast<ImageProviderType>().ToList();
        AvailableVideoProviderTypes = Enum.GetValues(typeof(VideoProviderType)).Cast<VideoProviderType>().ToList();

        LoadFromFile();
    }

    public IReadOnlyList<AIProviderType> AvailableProviderTypes { get; }
    public IReadOnlyList<ImageProviderType> AvailableImageProviderTypes { get; }
    public IReadOnlyList<VideoProviderType> AvailableVideoProviderTypes { get; }

    public ObservableCollection<ProviderValidationResult> ValidationResults { get; } = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private AIProviderType _defaultProvider = AIProviderType.Qwen;

    [ObservableProperty]
    private ImageProviderType _defaultImageProvider = ImageProviderType.Local;

    [ObservableProperty]
    private VideoProviderType _defaultVideoProvider = VideoProviderType.Local;

    [ObservableProperty]
    private AIProviderType _selectedProvider = AIProviderType.Qwen;

    [ObservableProperty]
    private ImageProviderType _selectedImageProvider = ImageProviderType.Local;

    [ObservableProperty]
    private VideoProviderType _selectedVideoProvider = VideoProviderType.Local;

    // Qwen
    [ObservableProperty] private bool _qwenEnabled;
    [ObservableProperty] private string _qwenApiKey = string.Empty;
    [ObservableProperty] private string _qwenDefaultModel = string.Empty;
    [ObservableProperty] private string _qwenEndpoint = string.Empty;
    [ObservableProperty] private int _qwenTimeoutSeconds = 120;

    // Wenxin
    [ObservableProperty] private bool _wenxinEnabled;
    [ObservableProperty] private string _wenxinApiKey = string.Empty;
    [ObservableProperty] private string _wenxinApiSecret = string.Empty;
    [ObservableProperty] private string _wenxinDefaultModel = string.Empty;
    [ObservableProperty] private string _wenxinEndpoint = string.Empty;
    [ObservableProperty] private int _wenxinTimeoutSeconds = 120;

    // Zhipu
    [ObservableProperty] private bool _zhipuEnabled;
    [ObservableProperty] private string _zhipuApiKey = string.Empty;
    [ObservableProperty] private string _zhipuDefaultModel = string.Empty;
    [ObservableProperty] private string _zhipuEndpoint = string.Empty;
    [ObservableProperty] private int _zhipuTimeoutSeconds = 120;

    // Volcengine
    [ObservableProperty] private bool _volcengineEnabled;
    [ObservableProperty] private string _volcengineApiKey = string.Empty;
    [ObservableProperty] private string _volcengineEndpointId = string.Empty;
    [ObservableProperty] private string _volcengineDefaultModel = string.Empty;
    [ObservableProperty] private string _volcengineEndpoint = string.Empty;
    [ObservableProperty] private int _volcengineTimeoutSeconds = 120;

    // OpenAI
    [ObservableProperty] private bool _openAIEnabled;
    [ObservableProperty] private string _openAIApiKey = string.Empty;
    [ObservableProperty] private string _openAIDefaultModel = string.Empty;
    [ObservableProperty] private string _openAIEndpoint = string.Empty;
    [ObservableProperty] private int _openAITimeoutSeconds = 120;

    // Azure OpenAI
    [ObservableProperty] private bool _azureOpenAIEnabled;
    [ObservableProperty] private string _azureOpenAIApiKey = string.Empty;
    [ObservableProperty] private string _azureOpenAIEndpoint = string.Empty;
    [ObservableProperty] private string _azureOpenAIDeploymentName = string.Empty;
    [ObservableProperty] private string _azureOpenAIDefaultModel = string.Empty;
    [ObservableProperty] private string _azureOpenAIApiVersion = string.Empty;
    [ObservableProperty] private int _azureOpenAITimeoutSeconds = 120;

    // Image - Local
    [ObservableProperty] private bool _imageLocalEnabled = true;
    [ObservableProperty] private int _imageLocalWidth = 1024;
    [ObservableProperty] private int _imageLocalHeight = 576;
    [ObservableProperty] private string _imageLocalStyle = "Poster";

    // Image - OpenAI
    [ObservableProperty] private bool _imageOpenAIEnabled;
    [ObservableProperty] private string _imageOpenAIApiKey = string.Empty;
    [ObservableProperty] private string _imageOpenAIDefaultModel = "gpt-image-1";
    [ObservableProperty] private string _imageOpenAIEndpoint = "https://api.openai.com/v1";
    [ObservableProperty] private string _imageOpenAISize = "1024x1024";
    [ObservableProperty] private string _imageOpenAIQuality = "standard";
    [ObservableProperty] private int _imageOpenAITimeoutSeconds = 120;

    // Video - Local
    [ObservableProperty] private bool _videoLocalEnabled = true;
    [ObservableProperty] private int _videoLocalWidth = 1280;
    [ObservableProperty] private int _videoLocalHeight = 720;
    [ObservableProperty] private int _videoLocalFps = 30;
    [ObservableProperty] private int _videoLocalBitrateKbps = 4000;
    [ObservableProperty] private double _videoLocalTransitionSeconds = 0.5;
    [ObservableProperty] private bool _videoLocalUseKenBurns = true;

    public bool IsQwenSelected => SelectedProvider == AIProviderType.Qwen;
    public bool IsWenxinSelected => SelectedProvider == AIProviderType.Wenxin;
    public bool IsZhipuSelected => SelectedProvider == AIProviderType.Zhipu;
    public bool IsVolcengineSelected => SelectedProvider == AIProviderType.Volcengine;
    public bool IsOpenAISelected => SelectedProvider == AIProviderType.OpenAI;
    public bool IsAzureOpenAISelected => SelectedProvider == AIProviderType.AzureOpenAI;
    public bool IsImageLocalSelected => SelectedImageProvider == ImageProviderType.Local;
    public bool IsImageOpenAISelected => SelectedImageProvider == ImageProviderType.OpenAI;
    public bool IsVideoLocalSelected => SelectedVideoProvider == VideoProviderType.Local;

    private void LoadFromFile()
    {
        var cfg = _settingsStore.LoadAIServices();

        DefaultProvider = cfg.DefaultProvider;
        DefaultImageProvider = cfg.Image.DefaultProvider;
        DefaultVideoProvider = cfg.Video.DefaultProvider;
        SelectedProvider = cfg.DefaultProvider;
        SelectedImageProvider = cfg.Image.DefaultProvider;
        SelectedVideoProvider = cfg.Video.DefaultProvider;

        QwenEnabled = cfg.Qwen.Enabled;
        QwenApiKey = cfg.Qwen.ApiKey;
        QwenDefaultModel = cfg.Qwen.DefaultModel;
        QwenEndpoint = cfg.Qwen.Endpoint;
        QwenTimeoutSeconds = cfg.Qwen.TimeoutSeconds;

        WenxinEnabled = cfg.Wenxin.Enabled;
        WenxinApiKey = cfg.Wenxin.ApiKey;
        WenxinApiSecret = cfg.Wenxin.ApiSecret;
        WenxinDefaultModel = cfg.Wenxin.DefaultModel;
        WenxinEndpoint = cfg.Wenxin.Endpoint;
        WenxinTimeoutSeconds = cfg.Wenxin.TimeoutSeconds;

        ZhipuEnabled = cfg.Zhipu.Enabled;
        ZhipuApiKey = cfg.Zhipu.ApiKey;
        ZhipuDefaultModel = cfg.Zhipu.DefaultModel;
        ZhipuEndpoint = cfg.Zhipu.Endpoint;
        ZhipuTimeoutSeconds = cfg.Zhipu.TimeoutSeconds;

        VolcengineEnabled = cfg.Volcengine.Enabled;
        VolcengineApiKey = cfg.Volcengine.ApiKey;
        VolcengineEndpointId = cfg.Volcengine.EndpointId;
        VolcengineDefaultModel = cfg.Volcengine.DefaultModel;
        VolcengineEndpoint = cfg.Volcengine.Endpoint;
        VolcengineTimeoutSeconds = cfg.Volcengine.TimeoutSeconds;

        OpenAIEnabled = cfg.OpenAI.Enabled;
        OpenAIApiKey = cfg.OpenAI.ApiKey;
        OpenAIDefaultModel = cfg.OpenAI.DefaultModel;
        OpenAIEndpoint = cfg.OpenAI.Endpoint;
        OpenAITimeoutSeconds = cfg.OpenAI.TimeoutSeconds;

        AzureOpenAIEnabled = cfg.AzureOpenAI.Enabled;
        AzureOpenAIApiKey = cfg.AzureOpenAI.ApiKey;
        AzureOpenAIEndpoint = cfg.AzureOpenAI.Endpoint;
        AzureOpenAIDeploymentName = cfg.AzureOpenAI.DeploymentName;
        AzureOpenAIDefaultModel = cfg.AzureOpenAI.DefaultModel;
        AzureOpenAIApiVersion = cfg.AzureOpenAI.ApiVersion;
        AzureOpenAITimeoutSeconds = cfg.AzureOpenAI.TimeoutSeconds;

        ImageLocalEnabled = cfg.Image.Local.Enabled;
        ImageLocalWidth = cfg.Image.Local.Width;
        ImageLocalHeight = cfg.Image.Local.Height;
        ImageLocalStyle = cfg.Image.Local.Style;

        ImageOpenAIEnabled = cfg.Image.OpenAI.Enabled;
        ImageOpenAIApiKey = cfg.Image.OpenAI.ApiKey;
        ImageOpenAIDefaultModel = cfg.Image.OpenAI.DefaultModel;
        ImageOpenAIEndpoint = cfg.Image.OpenAI.Endpoint;
        ImageOpenAISize = cfg.Image.OpenAI.Size;
        ImageOpenAIQuality = cfg.Image.OpenAI.Quality;
        ImageOpenAITimeoutSeconds = cfg.Image.OpenAI.TimeoutSeconds;

        VideoLocalEnabled = cfg.Video.Local.Enabled;
        VideoLocalWidth = cfg.Video.Local.Width;
        VideoLocalHeight = cfg.Video.Local.Height;
        VideoLocalFps = cfg.Video.Local.Fps;
        VideoLocalBitrateKbps = cfg.Video.Local.BitrateKbps;
        VideoLocalTransitionSeconds = cfg.Video.Local.TransitionSeconds;
        VideoLocalUseKenBurns = cfg.Video.Local.UseKenBurns;
    }

    private AIServicesConfiguration BuildConfig()
    {
        return new AIServicesConfiguration
        {
            DefaultProvider = DefaultProvider,
            Qwen = new QwenConfig
            {
                Enabled = QwenEnabled,
                ApiKey = QwenApiKey?.Trim() ?? string.Empty,
                DefaultModel = QwenDefaultModel?.Trim() ?? string.Empty,
                Endpoint = QwenEndpoint?.Trim() ?? string.Empty,
                TimeoutSeconds = QwenTimeoutSeconds
            },
            Wenxin = new WenxinConfig
            {
                Enabled = WenxinEnabled,
                ApiKey = WenxinApiKey?.Trim() ?? string.Empty,
                ApiSecret = WenxinApiSecret?.Trim() ?? string.Empty,
                DefaultModel = WenxinDefaultModel?.Trim() ?? string.Empty,
                Endpoint = WenxinEndpoint?.Trim() ?? string.Empty,
                TimeoutSeconds = WenxinTimeoutSeconds
            },
            Zhipu = new ZhipuConfig
            {
                Enabled = ZhipuEnabled,
                ApiKey = ZhipuApiKey?.Trim() ?? string.Empty,
                DefaultModel = ZhipuDefaultModel?.Trim() ?? string.Empty,
                Endpoint = ZhipuEndpoint?.Trim() ?? string.Empty,
                TimeoutSeconds = ZhipuTimeoutSeconds
            },
            Volcengine = new VolcengineConfig
            {
                Enabled = VolcengineEnabled,
                ApiKey = VolcengineApiKey?.Trim() ?? string.Empty,
                EndpointId = VolcengineEndpointId?.Trim() ?? string.Empty,
                DefaultModel = VolcengineDefaultModel?.Trim() ?? string.Empty,
                Endpoint = VolcengineEndpoint?.Trim() ?? string.Empty,
                TimeoutSeconds = VolcengineTimeoutSeconds
            },
            OpenAI = new OpenAIConfig
            {
                Enabled = OpenAIEnabled,
                ApiKey = OpenAIApiKey?.Trim() ?? string.Empty,
                DefaultModel = OpenAIDefaultModel?.Trim() ?? string.Empty,
                Endpoint = OpenAIEndpoint?.Trim() ?? string.Empty,
                TimeoutSeconds = OpenAITimeoutSeconds
            },
            AzureOpenAI = new AzureOpenAIConfig
            {
                Enabled = AzureOpenAIEnabled,
                ApiKey = AzureOpenAIApiKey?.Trim() ?? string.Empty,
                Endpoint = AzureOpenAIEndpoint?.Trim() ?? string.Empty,
                DeploymentName = AzureOpenAIDeploymentName?.Trim() ?? string.Empty,
                DefaultModel = AzureOpenAIDefaultModel?.Trim() ?? string.Empty,
                ApiVersion = AzureOpenAIApiVersion?.Trim() ?? string.Empty,
                TimeoutSeconds = AzureOpenAITimeoutSeconds
            },
            Image = new ImageServicesConfiguration
            {
                DefaultProvider = DefaultImageProvider,
                Local = new LocalImageConfig
                {
                    Enabled = ImageLocalEnabled,
                    Width = ImageLocalWidth,
                    Height = ImageLocalHeight,
                    Style = ImageLocalStyle?.Trim() ?? string.Empty
                },
                OpenAI = new OpenAIImageConfig
                {
                    Enabled = ImageOpenAIEnabled,
                    ApiKey = ImageOpenAIApiKey?.Trim() ?? string.Empty,
                    DefaultModel = ImageOpenAIDefaultModel?.Trim() ?? string.Empty,
                    Endpoint = ImageOpenAIEndpoint?.Trim() ?? string.Empty,
                    Size = ImageOpenAISize?.Trim() ?? string.Empty,
                    Quality = ImageOpenAIQuality?.Trim() ?? string.Empty,
                    TimeoutSeconds = ImageOpenAITimeoutSeconds
                }
            },
            Video = new VideoServicesConfiguration
            {
                DefaultProvider = DefaultVideoProvider,
                Local = new LocalVideoConfig
                {
                    Enabled = VideoLocalEnabled,
                    Width = VideoLocalWidth,
                    Height = VideoLocalHeight,
                    Fps = VideoLocalFps,
                    BitrateKbps = VideoLocalBitrateKbps,
                    TransitionSeconds = VideoLocalTransitionSeconds,
                    UseKenBurns = VideoLocalUseKenBurns
                }
            }
        };
    }

    partial void OnSelectedProviderChanged(AIProviderType value)
    {
        OnPropertyChanged(nameof(IsQwenSelected));
        OnPropertyChanged(nameof(IsWenxinSelected));
        OnPropertyChanged(nameof(IsZhipuSelected));
        OnPropertyChanged(nameof(IsVolcengineSelected));
        OnPropertyChanged(nameof(IsOpenAISelected));
        OnPropertyChanged(nameof(IsAzureOpenAISelected));
    }

    partial void OnSelectedImageProviderChanged(ImageProviderType value)
    {
        OnPropertyChanged(nameof(IsImageLocalSelected));
        OnPropertyChanged(nameof(IsImageOpenAISelected));
    }

    partial void OnSelectedVideoProviderChanged(VideoProviderType value)
    {
        OnPropertyChanged(nameof(IsVideoLocalSelected));
    }

    [RelayCommand]
    private void Reload()
    {
        LoadFromFile();
        StatusMessage = "已从 appsettings.json 重新加载。";
    }

    [RelayCommand]
    private void Save()
    {
        if (TrySave(out var error))
        {
            StatusMessage = "配置已保存到 appsettings.json。";
            return;
        }

        StatusMessage = $"保存失败: {error}";
    }

    private bool TrySave(out string? error)
    {
        try
        {
            var cfg = BuildConfig();
            _settingsStore.SaveAIServices(cfg);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        ValidationResults.Clear();

        // Ensure file is saved so validation uses the same source of truth.
        if (!TrySave(out var saveError))
        {
            StatusMessage = $"验证前保存失败: {saveError}";
            return;
        }

        try
        {
            StatusMessage = "正在验证配置...";
            var results = await _aiManager.ValidateAllProvidersAsync();

            foreach (var kv in results.OrderBy(kv => kv.Key))
            {
                ValidationResults.Add(new ProviderValidationResult
                {
                    Provider = kv.Key,
                    Success = kv.Value,
                    Message = kv.Value ? "配置有效" : "配置无效",
                    Timestamp = DateTimeOffset.Now
                });
            }

            StatusMessage = "验证完成。";
        }
        catch (Exception ex)
        {
            ValidationResults.Add(new ProviderValidationResult
            {
                Provider = DefaultProvider,
                Success = false,
                Message = $"验证异常: {ex.Message}",
                Timestamp = DateTimeOffset.Now
            });
            StatusMessage = "验证失败（发生异常）。";
        }
    }
}

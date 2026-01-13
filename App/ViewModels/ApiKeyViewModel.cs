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

        ValidationResults.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(HasValidationResults));
            OnPropertyChanged(nameof(HasNoValidationResults));
        };

        LoadFromFile();
    }

    public IReadOnlyList<AIProviderType> AvailableProviderTypes { get; }
    public IReadOnlyList<ImageProviderType> AvailableImageProviderTypes { get; }
    public IReadOnlyList<VideoProviderType> AvailableVideoProviderTypes { get; }

    public ObservableCollection<ProviderValidationResult> ValidationResults { get; } = new();
    public bool HasValidationResults => ValidationResults.Count > 0;
    public bool HasNoValidationResults => ValidationResults.Count == 0;

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

    // DeepSeek
    [ObservableProperty] private bool _deepSeekEnabled;
    [ObservableProperty] private string _deepSeekApiKey = string.Empty;
    [ObservableProperty] private string _deepSeekDefaultModel = string.Empty;
    [ObservableProperty] private string _deepSeekEndpoint = string.Empty;
    [ObservableProperty] private int _deepSeekTimeoutSeconds = 120;

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

    // Gemini
    [ObservableProperty] private bool _geminiEnabled;
    [ObservableProperty] private string _geminiApiKey = string.Empty;
    [ObservableProperty] private string _geminiDefaultModel = string.Empty;
    [ObservableProperty] private string _geminiEndpoint = string.Empty;
    [ObservableProperty] private int _geminiTimeoutSeconds = 120;

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

    // Image - Gemini
    [ObservableProperty] private bool _imageGeminiEnabled;
    [ObservableProperty] private string _imageGeminiApiKey = string.Empty;
    [ObservableProperty] private string _imageGeminiDefaultModel = "imagen-3.0-generate-002";
    [ObservableProperty] private string _imageGeminiEndpoint = "https://generativelanguage.googleapis.com/v1beta";
    [ObservableProperty] private string _imageGeminiResponseMimeType = "image/png";
    [ObservableProperty] private int _imageGeminiTimeoutSeconds = 120;

    // Image - Stable Diffusion API
    [ObservableProperty] private bool _imageStableDiffusionApiEnabled;
    [ObservableProperty] private string _imageStableDiffusionApiApiKey = string.Empty;
    [ObservableProperty] private string _imageStableDiffusionApiDefaultModel = "runwayml/stable-diffusion-v1-5";
    [ObservableProperty] private string _imageStableDiffusionApiEndpoint = "https://stablediffusionapi.com/api/v3";
    [ObservableProperty] private string _imageStableDiffusionApiNegativePrompt = "low quality";
    [ObservableProperty] private int _imageStableDiffusionApiTimeoutSeconds = 120;

    // Video - Local
    [ObservableProperty] private bool _videoLocalEnabled = true;
    [ObservableProperty] private int _videoLocalWidth = 1280;
    [ObservableProperty] private int _videoLocalHeight = 720;
    [ObservableProperty] private int _videoLocalFps = 30;
    [ObservableProperty] private int _videoLocalBitrateKbps = 4000;
    [ObservableProperty] private double _videoLocalTransitionSeconds = 0.5;
    [ObservableProperty] private bool _videoLocalUseKenBurns = true;

    // Video - OpenAI
    [ObservableProperty] private bool _videoOpenAIEnabled;
    [ObservableProperty] private string _videoOpenAIApiKey = string.Empty;
    [ObservableProperty] private string _videoOpenAIDefaultModel = "sora-2";
    [ObservableProperty] private string _videoOpenAIEndpoint = "https://api.openai.com/v1";
    [ObservableProperty] private int _videoOpenAITimeoutSeconds = 120;

    // Video - Gemini
    [ObservableProperty] private bool _videoGeminiEnabled;
    [ObservableProperty] private string _videoGeminiApiKey = string.Empty;
    [ObservableProperty] private string _videoGeminiDefaultModel = "veo-2.0-generate-001";
    [ObservableProperty] private string _videoGeminiEndpoint = "https://generativelanguage.googleapis.com/v1beta";
    [ObservableProperty] private int _videoGeminiTimeoutSeconds = 120;

    // Video - Stable Diffusion API
    [ObservableProperty] private bool _videoStableDiffusionApiEnabled;
    [ObservableProperty] private string _videoStableDiffusionApiApiKey = string.Empty;
    [ObservableProperty] private string _videoStableDiffusionApiDefaultModel = "text2video-v5";
    [ObservableProperty] private string _videoStableDiffusionApiEndpoint = "https://stablediffusionapi.com/api/v5";
    [ObservableProperty] private string _videoStableDiffusionApiNegativePrompt = "low quality";
    [ObservableProperty] private string _videoStableDiffusionApiScheduler = "UniPCMultistepScheduler";
    [ObservableProperty] private int _videoStableDiffusionApiTimeoutSeconds = 120;

    public bool IsQwenSelected => SelectedProvider == AIProviderType.Qwen;
    public bool IsWenxinSelected => SelectedProvider == AIProviderType.Wenxin;
    public bool IsZhipuSelected => SelectedProvider == AIProviderType.Zhipu;
    public bool IsVolcengineSelected => SelectedProvider == AIProviderType.Volcengine;
    public bool IsDeepSeekSelected => SelectedProvider == AIProviderType.DeepSeek;
    public bool IsOpenAISelected => SelectedProvider == AIProviderType.OpenAI;
    public bool IsAzureOpenAISelected => SelectedProvider == AIProviderType.AzureOpenAI;
    public bool IsGeminiSelected => SelectedProvider == AIProviderType.Gemini;
    public bool IsImageLocalSelected => SelectedImageProvider == ImageProviderType.Local;
    public bool IsImageOpenAISelected => SelectedImageProvider == ImageProviderType.OpenAI;
    public bool IsImageGeminiSelected => SelectedImageProvider == ImageProviderType.Gemini;
    public bool IsImageStableDiffusionApiSelected => SelectedImageProvider == ImageProviderType.StableDiffusionApi;
    public bool IsVideoLocalSelected => SelectedVideoProvider == VideoProviderType.Local;
    public bool IsVideoOpenAISelected => SelectedVideoProvider == VideoProviderType.OpenAI;
    public bool IsVideoGeminiSelected => SelectedVideoProvider == VideoProviderType.Gemini;
    public bool IsVideoStableDiffusionApiSelected => SelectedVideoProvider == VideoProviderType.StableDiffusionApi;

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

        DeepSeekEnabled = cfg.DeepSeek.Enabled;
        DeepSeekApiKey = cfg.DeepSeek.ApiKey;
        DeepSeekDefaultModel = cfg.DeepSeek.DefaultModel;
        DeepSeekEndpoint = cfg.DeepSeek.Endpoint;
        DeepSeekTimeoutSeconds = cfg.DeepSeek.TimeoutSeconds;

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

        GeminiEnabled = cfg.Gemini.Enabled;
        GeminiApiKey = cfg.Gemini.ApiKey;
        GeminiDefaultModel = cfg.Gemini.DefaultModel;
        GeminiEndpoint = cfg.Gemini.Endpoint;
        GeminiTimeoutSeconds = cfg.Gemini.TimeoutSeconds;

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

        ImageGeminiEnabled = cfg.Image.Gemini.Enabled;
        ImageGeminiApiKey = cfg.Image.Gemini.ApiKey;
        ImageGeminiDefaultModel = cfg.Image.Gemini.DefaultModel;
        ImageGeminiEndpoint = cfg.Image.Gemini.Endpoint;
        ImageGeminiResponseMimeType = cfg.Image.Gemini.ResponseMimeType;
        ImageGeminiTimeoutSeconds = cfg.Image.Gemini.TimeoutSeconds;

        ImageStableDiffusionApiEnabled = cfg.Image.StableDiffusionApi.Enabled;
        ImageStableDiffusionApiApiKey = cfg.Image.StableDiffusionApi.ApiKey;
        ImageStableDiffusionApiDefaultModel = cfg.Image.StableDiffusionApi.DefaultModel;
        ImageStableDiffusionApiEndpoint = cfg.Image.StableDiffusionApi.Endpoint;
        ImageStableDiffusionApiNegativePrompt = cfg.Image.StableDiffusionApi.NegativePrompt;
        ImageStableDiffusionApiTimeoutSeconds = cfg.Image.StableDiffusionApi.TimeoutSeconds;

        VideoLocalEnabled = cfg.Video.Local.Enabled;
        VideoLocalWidth = cfg.Video.Local.Width;
        VideoLocalHeight = cfg.Video.Local.Height;
        VideoLocalFps = cfg.Video.Local.Fps;
        VideoLocalBitrateKbps = cfg.Video.Local.BitrateKbps;
        VideoLocalTransitionSeconds = cfg.Video.Local.TransitionSeconds;
        VideoLocalUseKenBurns = cfg.Video.Local.UseKenBurns;

        VideoOpenAIEnabled = cfg.Video.OpenAI.Enabled;
        VideoOpenAIApiKey = cfg.Video.OpenAI.ApiKey;
        VideoOpenAIDefaultModel = cfg.Video.OpenAI.DefaultModel;
        VideoOpenAIEndpoint = cfg.Video.OpenAI.Endpoint;
        VideoOpenAITimeoutSeconds = cfg.Video.OpenAI.TimeoutSeconds;

        VideoGeminiEnabled = cfg.Video.Gemini.Enabled;
        VideoGeminiApiKey = cfg.Video.Gemini.ApiKey;
        VideoGeminiDefaultModel = cfg.Video.Gemini.DefaultModel;
        VideoGeminiEndpoint = cfg.Video.Gemini.Endpoint;
        VideoGeminiTimeoutSeconds = cfg.Video.Gemini.TimeoutSeconds;

        VideoStableDiffusionApiEnabled = cfg.Video.StableDiffusionApi.Enabled;
        VideoStableDiffusionApiApiKey = cfg.Video.StableDiffusionApi.ApiKey;
        VideoStableDiffusionApiDefaultModel = cfg.Video.StableDiffusionApi.DefaultModel;
        VideoStableDiffusionApiEndpoint = cfg.Video.StableDiffusionApi.Endpoint;
        VideoStableDiffusionApiNegativePrompt = cfg.Video.StableDiffusionApi.NegativePrompt;
        VideoStableDiffusionApiScheduler = cfg.Video.StableDiffusionApi.Scheduler;
        VideoStableDiffusionApiTimeoutSeconds = cfg.Video.StableDiffusionApi.TimeoutSeconds;
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
            DeepSeek = new DeepSeekConfig
            {
                Enabled = DeepSeekEnabled,
                ApiKey = DeepSeekApiKey?.Trim() ?? string.Empty,
                DefaultModel = DeepSeekDefaultModel?.Trim() ?? string.Empty,
                Endpoint = DeepSeekEndpoint?.Trim() ?? string.Empty,
                TimeoutSeconds = DeepSeekTimeoutSeconds
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
            Gemini = new GeminiConfig
            {
                Enabled = GeminiEnabled,
                ApiKey = GeminiApiKey?.Trim() ?? string.Empty,
                DefaultModel = GeminiDefaultModel?.Trim() ?? string.Empty,
                Endpoint = GeminiEndpoint?.Trim() ?? string.Empty,
                TimeoutSeconds = GeminiTimeoutSeconds
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
                },
                Gemini = new GeminiImageConfig
                {
                    Enabled = ImageGeminiEnabled,
                    ApiKey = ImageGeminiApiKey?.Trim() ?? string.Empty,
                    DefaultModel = ImageGeminiDefaultModel?.Trim() ?? string.Empty,
                    Endpoint = ImageGeminiEndpoint?.Trim() ?? string.Empty,
                    ResponseMimeType = ImageGeminiResponseMimeType?.Trim() ?? string.Empty,
                    TimeoutSeconds = ImageGeminiTimeoutSeconds
                },
                StableDiffusionApi = new StableDiffusionApiImageConfig
                {
                    Enabled = ImageStableDiffusionApiEnabled,
                    ApiKey = ImageStableDiffusionApiApiKey?.Trim() ?? string.Empty,
                    DefaultModel = ImageStableDiffusionApiDefaultModel?.Trim() ?? string.Empty,
                    Endpoint = ImageStableDiffusionApiEndpoint?.Trim() ?? string.Empty,
                    NegativePrompt = ImageStableDiffusionApiNegativePrompt?.Trim() ?? string.Empty,
                    TimeoutSeconds = ImageStableDiffusionApiTimeoutSeconds
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
                },
                OpenAI = new OpenAIVideoConfig
                {
                    Enabled = VideoOpenAIEnabled,
                    ApiKey = VideoOpenAIApiKey?.Trim() ?? string.Empty,
                    DefaultModel = VideoOpenAIDefaultModel?.Trim() ?? string.Empty,
                    Endpoint = VideoOpenAIEndpoint?.Trim() ?? string.Empty,
                    TimeoutSeconds = VideoOpenAITimeoutSeconds
                },
                Gemini = new GeminiVideoConfig
                {
                    Enabled = VideoGeminiEnabled,
                    ApiKey = VideoGeminiApiKey?.Trim() ?? string.Empty,
                    DefaultModel = VideoGeminiDefaultModel?.Trim() ?? string.Empty,
                    Endpoint = VideoGeminiEndpoint?.Trim() ?? string.Empty,
                    TimeoutSeconds = VideoGeminiTimeoutSeconds
                },
                StableDiffusionApi = new StableDiffusionApiVideoConfig
                {
                    Enabled = VideoStableDiffusionApiEnabled,
                    ApiKey = VideoStableDiffusionApiApiKey?.Trim() ?? string.Empty,
                    DefaultModel = VideoStableDiffusionApiDefaultModel?.Trim() ?? string.Empty,
                    Endpoint = VideoStableDiffusionApiEndpoint?.Trim() ?? string.Empty,
                    NegativePrompt = VideoStableDiffusionApiNegativePrompt?.Trim() ?? string.Empty,
                    Scheduler = VideoStableDiffusionApiScheduler?.Trim() ?? string.Empty,
                    TimeoutSeconds = VideoStableDiffusionApiTimeoutSeconds
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
        OnPropertyChanged(nameof(IsDeepSeekSelected));
        OnPropertyChanged(nameof(IsOpenAISelected));
        OnPropertyChanged(nameof(IsAzureOpenAISelected));
        OnPropertyChanged(nameof(IsGeminiSelected));
    }

    partial void OnSelectedImageProviderChanged(ImageProviderType value)
    {
        OnPropertyChanged(nameof(IsImageLocalSelected));
        OnPropertyChanged(nameof(IsImageOpenAISelected));
        OnPropertyChanged(nameof(IsImageGeminiSelected));
        OnPropertyChanged(nameof(IsImageStableDiffusionApiSelected));
    }

    partial void OnSelectedVideoProviderChanged(VideoProviderType value)
    {
        OnPropertyChanged(nameof(IsVideoLocalSelected));
        OnPropertyChanged(nameof(IsVideoOpenAISelected));
        OnPropertyChanged(nameof(IsVideoGeminiSelected));
        OnPropertyChanged(nameof(IsVideoStableDiffusionApiSelected));
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

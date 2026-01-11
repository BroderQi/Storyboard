using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Storyboard.AI;
using Storyboard.AI.Core;
using Storyboard.Models;
using Storyboard.Services;
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

        LoadFromFile();
    }

    public IReadOnlyList<AIProviderType> AvailableProviderTypes { get; }

    public ObservableCollection<ProviderValidationResult> ValidationResults { get; } = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private AIProviderType _defaultProvider = AIProviderType.Qwen;

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

    private void LoadFromFile()
    {
        var cfg = _settingsStore.LoadAIServices();

        DefaultProvider = cfg.DefaultProvider;

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
            }
        };
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

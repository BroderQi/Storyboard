using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Storyboard.AI;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Configuration;
using Storyboard.Models;
using System;
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

        ValidationResults.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(HasValidationResults));
            OnPropertyChanged(nameof(HasNoValidationResults));
        };

        LoadFromFile();
    }

    public IReadOnlyList<AIProviderType> AvailableProviderTypes { get; }

    public ObservableCollection<ProviderValidationResult> ValidationResults { get; } = new();
    public bool HasValidationResults => ValidationResults.Count > 0;
    public bool HasNoValidationResults => ValidationResults.Count == 0;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private AIProviderType _defaultTextProvider = AIProviderType.Qwen;

    [ObservableProperty]
    private string _defaultTextModel = string.Empty;

    [ObservableProperty]
    private AIProviderType _defaultImageProvider = AIProviderType.Qwen;

    [ObservableProperty]
    private string _defaultImageModel = string.Empty;

    [ObservableProperty]
    private AIProviderType _defaultVideoProvider = AIProviderType.Qwen;

    [ObservableProperty]
    private string _defaultVideoModel = string.Empty;

    [ObservableProperty]
    private AIProviderType _selectedProvider = AIProviderType.Qwen;

    [ObservableProperty] private bool _qwenEnabled;
    [ObservableProperty] private string _qwenApiKey = string.Empty;
    [ObservableProperty] private string _qwenEndpoint = string.Empty;
    [ObservableProperty] private int _qwenTimeoutSeconds = 120;
    [ObservableProperty] private string _qwenDefaultTextModel = string.Empty;
    [ObservableProperty] private string _qwenDefaultImageModel = string.Empty;
    [ObservableProperty] private string _qwenDefaultVideoModel = string.Empty;

    [ObservableProperty] private bool _volcengineEnabled;
    [ObservableProperty] private string _volcengineApiKey = string.Empty;
    [ObservableProperty] private string _volcengineEndpoint = string.Empty;
    [ObservableProperty] private int _volcengineTimeoutSeconds = 120;
    [ObservableProperty] private string _volcengineDefaultTextModel = string.Empty;
    [ObservableProperty] private string _volcengineDefaultImageModel = string.Empty;
    [ObservableProperty] private string _volcengineDefaultVideoModel = string.Empty;

    public bool IsQwenSelected => SelectedProvider == AIProviderType.Qwen;
    public bool IsVolcengineSelected => SelectedProvider == AIProviderType.Volcengine;

    private void LoadFromFile()
    {
        var cfg = _settingsStore.LoadAIServices();

        DefaultTextProvider = cfg.Defaults.Text.Provider;
        DefaultTextModel = cfg.Defaults.Text.Model;
        DefaultImageProvider = cfg.Defaults.Image.Provider;
        DefaultImageModel = cfg.Defaults.Image.Model;
        DefaultVideoProvider = cfg.Defaults.Video.Provider;
        DefaultVideoModel = cfg.Defaults.Video.Model;

        SelectedProvider = DefaultTextProvider;

        var qwen = cfg.Providers.Qwen;
        QwenEnabled = qwen.Enabled;
        QwenApiKey = qwen.ApiKey;
        QwenEndpoint = qwen.Endpoint;
        QwenTimeoutSeconds = qwen.TimeoutSeconds;
        QwenDefaultTextModel = qwen.DefaultModels.Text;
        QwenDefaultImageModel = qwen.DefaultModels.Image;
        QwenDefaultVideoModel = qwen.DefaultModels.Video;

        var volc = cfg.Providers.Volcengine;
        VolcengineEnabled = volc.Enabled;
        VolcengineApiKey = volc.ApiKey;
        VolcengineEndpoint = volc.Endpoint;
        VolcengineTimeoutSeconds = volc.TimeoutSeconds;
        VolcengineDefaultTextModel = volc.DefaultModels.Text;
        VolcengineDefaultImageModel = volc.DefaultModels.Image;
        VolcengineDefaultVideoModel = volc.DefaultModels.Video;
    }

    private AIServicesConfiguration BuildConfig()
    {
        return new AIServicesConfiguration
        {
            Providers = new AIProvidersConfiguration
            {
                Qwen = new AIProviderConfiguration
                {
                    Enabled = QwenEnabled,
                    ApiKey = QwenApiKey?.Trim() ?? string.Empty,
                    Endpoint = QwenEndpoint?.Trim() ?? string.Empty,
                    TimeoutSeconds = QwenTimeoutSeconds,
                    DefaultModels = new AIProviderModelDefaults
                    {
                        Text = QwenDefaultTextModel?.Trim() ?? string.Empty,
                        Image = QwenDefaultImageModel?.Trim() ?? string.Empty,
                        Video = QwenDefaultVideoModel?.Trim() ?? string.Empty
                    }
                },
                Volcengine = new AIProviderConfiguration
                {
                    Enabled = VolcengineEnabled,
                    ApiKey = VolcengineApiKey?.Trim() ?? string.Empty,
                    Endpoint = VolcengineEndpoint?.Trim() ?? string.Empty,
                    TimeoutSeconds = VolcengineTimeoutSeconds,
                    DefaultModels = new AIProviderModelDefaults
                    {
                        Text = VolcengineDefaultTextModel?.Trim() ?? string.Empty,
                        Image = VolcengineDefaultImageModel?.Trim() ?? string.Empty,
                        Video = VolcengineDefaultVideoModel?.Trim() ?? string.Empty
                    }
                }
            },
            Defaults = new AIServiceDefaults
            {
                Text = new AIServiceDefaultSelection
                {
                    Provider = DefaultTextProvider,
                    Model = DefaultTextModel?.Trim() ?? string.Empty
                },
                Image = new AIServiceDefaultSelection
                {
                    Provider = DefaultImageProvider,
                    Model = DefaultImageModel?.Trim() ?? string.Empty
                },
                Video = new AIServiceDefaultSelection
                {
                    Provider = DefaultVideoProvider,
                    Model = DefaultVideoModel?.Trim() ?? string.Empty
                }
            }
        };
    }

    partial void OnSelectedProviderChanged(AIProviderType value)
    {
        OnPropertyChanged(nameof(IsQwenSelected));
        OnPropertyChanged(nameof(IsVolcengineSelected));
    }

    [RelayCommand]
    private void Reload()
    {
        LoadFromFile();
        StatusMessage = "Reloaded from appsettings.json.";
    }

    [RelayCommand]
    private void Save()
    {
        if (TrySave(out var error))
        {
            StatusMessage = "Configuration saved to appsettings.json.";
            return;
        }

        StatusMessage = $"Save failed: {error}";
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

        if (!TrySave(out var saveError))
        {
            StatusMessage = $"Save before validation failed: {saveError}";
            return;
        }

        try
        {
            StatusMessage = "Validating providers...";
            var results = await _aiManager.ValidateAllProvidersAsync().ConfigureAwait(false);

            foreach (var kv in results.OrderBy(kv => kv.Key))
            {
                ValidationResults.Add(new ProviderValidationResult
                {
                    Provider = kv.Key,
                    Success = kv.Value,
                    Message = kv.Value ? "Configuration valid" : "Configuration invalid",
                    Timestamp = DateTimeOffset.Now
                });
            }

            StatusMessage = "Validation completed.";
        }
        catch (Exception ex)
        {
            ValidationResults.Add(new ProviderValidationResult
            {
                Provider = DefaultTextProvider,
                Success = false,
                Message = $"Validation error: {ex.Message}",
                Timestamp = DateTimeOffset.Now
            });
            StatusMessage = "Validation failed.";
        }
    }
}

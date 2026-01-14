using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using System.Net.Http.Headers;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace Storyboard.AI.Providers;

public class VolcengineServiceProvider : BaseAIServiceProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public VolcengineServiceProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor, ILogger<VolcengineServiceProvider> logger)
        : base(logger)
    {
        _configMonitor = configMonitor;
    }

    private AIProviderConfiguration Config => _configMonitor.CurrentValue.Providers.Volcengine;

    public override AIProviderType ProviderType => AIProviderType.Volcengine;
    public override string DisplayName => "Volcengine";

    public override bool IsConfigured =>
        Config.Enabled &&
        !string.IsNullOrWhiteSpace(Config.ApiKey) &&
        !string.IsNullOrWhiteSpace(Config.Endpoint);

    public override IReadOnlyList<string> SupportedModels => new[]
    {
        "doubao-pro-4k",
        "doubao-pro-32k",
        "doubao-lite-4k"
    };

    public override async Task<string> ChatAsync(AIChatRequest request, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        EnsureModel(request.Model);
        var payload = BuildRequestPayload(request, stream: false);
        using var httpClient = CreateHttpClient(Config.Endpoint, Config.TimeoutSeconds);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Config.ApiKey);

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("chat/completions", content, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Volcengine request failed: {responseBody}");
        }

        var result = JsonSerializer.Deserialize<VolcengineResponse>(responseBody);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    public override async IAsyncEnumerable<string> ChatStreamAsync(
        AIChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        EnsureModel(request.Model);
        var payload = BuildRequestPayload(request, stream: true);
        using var httpClient = CreateHttpClient(Config.Endpoint, Config.TimeoutSeconds);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Config.ApiKey);

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = content
        };

        var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:"))
                continue;

            var data = line.Substring(5).Trim();
            if (data == "[DONE]")
                break;

            var chunk = JsonSerializer.Deserialize<VolcengineStreamResponse>(data);
            var contentDelta = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(contentDelta))
            {
                yield return contentDelta!;
            }
        }
    }

    public override async Task<bool> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            Logger.LogWarning("Volcengine configuration incomplete.");
            return false;
        }

        try
        {
            var model = string.IsNullOrWhiteSpace(Config.DefaultModels.Text) ? "doubao-pro-4k" : Config.DefaultModels.Text;
            var request = new AIChatRequest
            {
                Model = model,
                Messages = new[] { new AIChatMessage(AIChatRole.User, "test") },
                MaxTokens = 16
            };

            _ = await ChatAsync(request, cancellationToken).ConfigureAwait(false);
            Logger.LogInformation("Volcengine configuration validated.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Volcengine configuration validation failed.");
            return false;
        }
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Volcengine is not configured.");
        }
    }

    private static void EnsureModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("Model is required for Volcengine requests.");
        }
    }

    private object BuildRequestPayload(AIChatRequest request, bool stream)
    {
        return new
        {
            model = request.Model,
            messages = request.Messages.Select(m => new
            {
                role = MapRole(m.Role),
                content = m.Content
            }).ToArray(),
            temperature = request.Temperature,
            top_p = request.TopP,
            max_tokens = request.MaxTokens,
            stream = stream
        };
    }

    private static string MapRole(AIChatRole role)
    {
        return role switch
        {
            AIChatRole.System => "system",
            AIChatRole.User => "user",
            AIChatRole.Assistant => "assistant",
            _ => "user"
        };
    }

    private class VolcengineResponse
    {
        public VolcengineChoice[]? Choices { get; set; }
    }

    private class VolcengineChoice
    {
        public VolcengineMessage? Message { get; set; }
    }

    private class VolcengineMessage
    {
        public string? Content { get; set; }
    }

    private class VolcengineStreamResponse
    {
        public VolcengineStreamChoice[]? Choices { get; set; }
    }

    private class VolcengineStreamChoice
    {
        public VolcengineDelta? Delta { get; set; }
    }

    private class VolcengineDelta
    {
        public string? Content { get; set; }
    }
}

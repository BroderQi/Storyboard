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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

        // Log request payload (avoid logging headers / API key)
        try
        {
            var payloadJson = JsonSerializer.Serialize(payload);
            Logger.LogDebug("Volcengine request payload: {Payload}", payloadJson);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to serialize Volcengine payload for logging.");
        }

        var response = await httpClient.PostAsync("chat/completions", content, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // Log response body for debugging empty/invalid responses
        Logger.LogDebug("Volcengine response body: {ResponseBody}", responseBody);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError("Volcengine request failed (status {Status}): {ResponseBody}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Volcengine request failed: {responseBody}");
        }

        var result = JsonSerializer.Deserialize<VolcengineResponse>(responseBody, JsonOptions);
        var contentText = result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        if (string.IsNullOrWhiteSpace(contentText))
        {
            Logger.LogWarning(
                "Volcengine response content empty. Body: {Body}",
                Truncate(responseBody, 800));
        }

        return contentText;
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

            // Log raw stream data for debugging
            Logger.LogDebug("Volcengine stream raw: {Data}", data);

            if (data == "[DONE]")
                break;

            var chunk = JsonSerializer.Deserialize<VolcengineStreamResponse>(data, JsonOptions);
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
                content = BuildMessageContent(m)
            }).ToArray(),
            temperature = request.Temperature,
            top_p = request.TopP,
            max_tokens = request.MaxTokens,
            stream = stream
        };
    }

    private static object BuildMessageContent(AIChatMessage message)
    {
        // 如果是多模态消息
        if (message.IsMultimodal && message.MultimodalContent != null)
        {
            var contentArray = new List<object>();
            foreach (var part in message.MultimodalContent)
            {
                if (part.Type == Core.MessageContentType.Text && !string.IsNullOrWhiteSpace(part.Text))
                {
                    contentArray.Add(new { type = "text", text = part.Text });
                }
                else if (part.Type == Core.MessageContentType.ImageBase64 && !string.IsNullOrWhiteSpace(part.ImageBase64))
                {
                    contentArray.Add(new
                    {
                        type = "image_url",
                        image_url = new { url = $"data:image/jpeg;base64,{part.ImageBase64}" }
                    });
                }
                else if (part.Type == Core.MessageContentType.ImageUrl && !string.IsNullOrWhiteSpace(part.ImageUrl))
                {
                    contentArray.Add(new
                    {
                        type = "image_url",
                        image_url = new { url = part.ImageUrl }
                    });
                }
            }
            return contentArray;
        }

        // 普通文本消息
        return message.Content ?? string.Empty;
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

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength) + "...(truncated)";
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

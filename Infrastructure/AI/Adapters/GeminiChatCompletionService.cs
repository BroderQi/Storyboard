using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace Storyboard.AI.Adapters;

/// <summary>
/// Gemini API 聊天适配器（generateContent）
/// </summary>
public sealed class GeminiChatCompletionService : IChatCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _modelId;
    private readonly Dictionary<string, object?> _attributes = new();

    public GeminiChatCompletionService(string apiKey, string modelId, HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _modelId = modelId;
        _httpClient = httpClient ?? new HttpClient();
    }

    public IReadOnlyDictionary<string, object?> Attributes => _attributes;

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildRequest(chatHistory, executionSettings);
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"/models/{_modelId}:generateContent?key={_apiKey}",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var text = ExtractText(responseBody);

        return new[] { new ChatMessageContent(AuthorRole.Assistant, text) };
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = BuildRequest(chatHistory, executionSettings);
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/models/{_modelId}:streamGenerateContent?alt=sse&key={_apiKey}")
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:"))
                continue;

            var data = line.Substring(5).Trim();
            if (data == "[DONE]")
                break;

            var text = ExtractText(data);
            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return new StreamingChatMessageContent(AuthorRole.Assistant, text);
            }
        }
    }

    private static object BuildRequest(ChatHistory chatHistory, PromptExecutionSettings? executionSettings)
    {
        var system = chatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
        var messages = chatHistory.Where(m => m.Role != AuthorRole.System);

        var contents = messages.Select(m => new
        {
            role = ToGeminiRole(m.Role),
            parts = new[] { new { text = m.Content ?? string.Empty } }
        }).ToArray();

        var settings = executionSettings as OpenAIPromptExecutionSettings;
        var payload = new Dictionary<string, object?>
        {
            ["contents"] = contents
        };

        if (system != null && !string.IsNullOrWhiteSpace(system.Content))
        {
            payload["system_instruction"] = new
            {
                parts = new[] { new { text = system.Content } }
            };
        }

        if (settings != null)
        {
            payload["generationConfig"] = new
            {
                temperature = settings.Temperature,
                topP = settings.TopP,
                maxOutputTokens = settings.MaxTokens
            };
        }

        return payload;
    }

    private static string ExtractText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return string.Empty;

        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var content))
            return string.Empty;

        if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            {
                sb.Append(text.GetString());
            }
        }

        return sb.ToString();
    }

    private static string ToGeminiRole(AuthorRole role)
    {
        if (role == AuthorRole.Assistant)
            return "model";
        return "user";
    }
}

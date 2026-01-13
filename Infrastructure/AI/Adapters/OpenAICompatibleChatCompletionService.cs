using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Linq;

namespace Storyboard.AI.Adapters;

/// <summary>
/// OpenAI 兼容接口适配器（DeepSeek/OpenAI/火山等）
/// </summary>
public sealed class OpenAICompatibleChatCompletionService : IChatCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;
    private readonly Dictionary<string, object?> _attributes = new();

    public OpenAICompatibleChatCompletionService(
        string apiKey,
        string modelId,
        HttpClient? httpClient = null,
        string? organization = null)
    {
        _modelId = modelId;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        if (!string.IsNullOrWhiteSpace(organization))
        {
            _httpClient.DefaultRequestHeaders.Add("OpenAI-Organization", organization.Trim());
        }
    }

    public IReadOnlyDictionary<string, object?> Attributes => _attributes;

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(chatHistory, executionSettings, stream: false);

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            "/chat/completions",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<OpenAIResponse>(responseBody);

        var messageContent = new ChatMessageContent(
            AuthorRole.Assistant,
            result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty);

        return new[] { messageContent };
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(chatHistory, executionSettings, stream: true);

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/chat/completions")
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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

            var chunk = JsonSerializer.Deserialize<OpenAIStreamResponse>(data);
            var delta = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(delta))
            {
                yield return new StreamingChatMessageContent(AuthorRole.Assistant, delta);
            }
        }
    }

    private object BuildRequest(ChatHistory chatHistory, PromptExecutionSettings? executionSettings, bool stream)
    {
        var settings = executionSettings as OpenAIPromptExecutionSettings;
        return new
        {
            model = _modelId,
            messages = chatHistory.Select(m => new
            {
                role = m.Role.Label,
                content = m.Content
            }).ToArray(),
            temperature = settings?.Temperature ?? 0.7,
            top_p = settings?.TopP ?? 0.95,
            max_tokens = settings?.MaxTokens ?? 2000,
            stream
        };
    }

    private sealed class OpenAIResponse
    {
        public OpenAIChoice[]? Choices { get; set; }
    }

    private sealed class OpenAIChoice
    {
        public OpenAIMessage? Message { get; set; }
    }

    private sealed class OpenAIMessage
    {
        public string? Content { get; set; }
    }

    private sealed class OpenAIStreamResponse
    {
        public OpenAIStreamChoice[]? Choices { get; set; }
    }

    private sealed class OpenAIStreamChoice
    {
        public OpenAIStreamDelta? Delta { get; set; }
    }

    private sealed class OpenAIStreamDelta
    {
        public string? Content { get; set; }
    }
}

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.IO;

namespace Storyboard.AI.Adapters;

/// <summary>
/// 智谱AI服务适配器
/// </summary>
public class ZhipuChatCompletionService : IChatCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _modelId;
    private readonly Dictionary<string, object?> _attributes = new();

    public ZhipuChatCompletionService(string apiKey, string modelId, HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _modelId = modelId;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public IReadOnlyDictionary<string, object?> Attributes => _attributes;

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _modelId,
            messages = chatHistory.Select(m => new
            {
                role = m.Role.Label,
                content = m.Content
            }).ToArray(),
            temperature = (executionSettings as OpenAIPromptExecutionSettings)?.Temperature ?? 0.7,
            top_p = (executionSettings as OpenAIPromptExecutionSettings)?.TopP ?? 0.95,
            max_tokens = (executionSettings as OpenAIPromptExecutionSettings)?.MaxTokens ?? 2000
        };

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
        var result = JsonSerializer.Deserialize<ZhipuResponse>(responseBody);

        var messageContent = new ChatMessageContent(
            AuthorRole.Assistant,
            result?.Choices?[0]?.Message?.Content ?? string.Empty);

        return new[] { messageContent };
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _modelId,
            messages = chatHistory.Select(m => new
            {
                role = m.Role.Label,
                content = m.Content
            }).ToArray(),
            temperature = (executionSettings as OpenAIPromptExecutionSettings)?.Temperature ?? 0.7,
            top_p = (executionSettings as OpenAIPromptExecutionSettings)?.TopP ?? 0.95,
            max_tokens = (executionSettings as OpenAIPromptExecutionSettings)?.MaxTokens ?? 2000,
            stream = true
        };

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

            var chunk = JsonSerializer.Deserialize<ZhipuStreamResponse>(data);
            if (chunk?.Choices?[0]?.Delta?.Content != null)
            {
                yield return new StreamingChatMessageContent(AuthorRole.Assistant, chunk.Choices[0].Delta.Content);
            }
        }
    }

    private class ZhipuResponse
    {
        public ZhipuChoice[]? Choices { get; set; }
    }

    private class ZhipuChoice
    {
        public ZhipuMessage? Message { get; set; }
    }

    private class ZhipuMessage
    {
        public string? Content { get; set; }
    }

    private class ZhipuStreamResponse
    {
        public ZhipuStreamChoice[]? Choices { get; set; }
    }

    private class ZhipuStreamChoice
    {
        public ZhipuDelta? Delta { get; set; }
    }

    private class ZhipuDelta
    {
        public string? Content { get; set; }
    }
}

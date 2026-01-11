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
/// 火山引擎服务适配器
/// </summary>
public class VolcengineChatCompletionService : IChatCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _endpointId;
    private readonly string _modelId;
    private readonly Dictionary<string, object?> _attributes = new();

    public VolcengineChatCompletionService(string apiKey, string endpointId, string modelId, HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _endpointId = endpointId;
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
            model = _endpointId, // 火山使用endpoint_id作为model参数
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
        var result = JsonSerializer.Deserialize<VolcengineResponse>(responseBody);

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
            model = _endpointId,
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

            var chunk = JsonSerializer.Deserialize<VolcengineStreamResponse>(data);
            if (chunk?.Choices?[0]?.Delta?.Content != null)
            {
                yield return new StreamingChatMessageContent(AuthorRole.Assistant, chunk.Choices[0].Delta.Content);
            }
        }
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

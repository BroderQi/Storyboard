using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

namespace Storyboard.AI.Adapters;

/// <summary>
/// 文心一言服务适配器
/// </summary>
public class WenxinChatCompletionService : IChatCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly string _modelId;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly Dictionary<string, object?> _attributes = new();

    public WenxinChatCompletionService(string apiKey, string apiSecret, string modelId, HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _modelId = modelId;
        _httpClient = httpClient ?? new HttpClient();
    }

    public IReadOnlyDictionary<string, object?> Attributes => _attributes;

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiry)
        {
            return _accessToken;
        }

        var url = $"/oauth/2.0/token?grant_type=client_credentials&client_id={_apiKey}&client_secret={_apiSecret}";
        var response = await _httpClient.PostAsync(url, null, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<TokenResponse>(responseBody);

        _accessToken = result?.AccessToken ?? throw new InvalidOperationException("Failed to get access token");
        _tokenExpiry = DateTime.Now.AddSeconds(result.ExpiresIn - 300); // 提前5分钟刷新

        return _accessToken;
    }

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var request = new
        {
            messages = chatHistory.Select(m => new
            {
                role = MapRole(m.Role),
                content = m.Content
            }).ToArray(),
            temperature = (executionSettings as OpenAIPromptExecutionSettings)?.Temperature ?? 0.7,
            top_p = (executionSettings as OpenAIPromptExecutionSettings)?.TopP ?? 0.95,
            max_output_tokens = (executionSettings as OpenAIPromptExecutionSettings)?.MaxTokens ?? 2000
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var endpoint = GetModelEndpoint(_modelId);
        var response = await _httpClient.PostAsync(
            $"{endpoint}?access_token={token}",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<WenxinResponse>(responseBody);

        var messageContent = new ChatMessageContent(
            AuthorRole.Assistant,
            result?.Result ?? string.Empty);

        return new[] { messageContent };
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var request = new
        {
            messages = chatHistory.Select(m => new
            {
                role = MapRole(m.Role),
                content = m.Content
            }).ToArray(),
            temperature = (executionSettings as OpenAIPromptExecutionSettings)?.Temperature ?? 0.7,
            top_p = (executionSettings as OpenAIPromptExecutionSettings)?.TopP ?? 0.95,
            max_output_tokens = (executionSettings as OpenAIPromptExecutionSettings)?.MaxTokens ?? 2000,
            stream = true
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var endpoint = GetModelEndpoint(_modelId);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}?access_token={token}")
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
            var chunk = JsonSerializer.Deserialize<WenxinResponse>(data);
            if (chunk?.Result != null)
            {
                yield return new StreamingChatMessageContent(AuthorRole.Assistant, chunk.Result);
            }
        }
    }

    private string MapRole(AuthorRole role)
    {
        return role.Label.ToLower() switch
        {
            "system" => "user", // 文心不支持system角色，转为user
            "user" => "user",
            "assistant" => "assistant",
            _ => "user"
        };
    }

    private string GetModelEndpoint(string modelId)
    {
        return modelId.ToLower() switch
        {
            "ernie-bot-4" => "/rpc/2.0/ai_custom/v1/wenxinworkshop/chat/completions_pro",
            "ernie-bot" => "/rpc/2.0/ai_custom/v1/wenxinworkshop/chat/completions",
            "ernie-bot-turbo" => "/rpc/2.0/ai_custom/v1/wenxinworkshop/chat/eb-instant",
            _ => "/rpc/2.0/ai_custom/v1/wenxinworkshop/chat/completions"
        };
    }

    private class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    private class WenxinResponse
    {
        public string? Result { get; set; }
    }
}

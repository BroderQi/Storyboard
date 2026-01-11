using System;
using Storyboard.AI.Core;

namespace Storyboard.Models;

public sealed class ProviderValidationResult
{
    public AIProviderType Provider { get; init; }
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public string ResultText => Success ? "有效" : "无效";
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Media;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class LocalImageGenerationProvider : IImageGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public LocalImageGenerationProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor)
    {
        _configMonitor = configMonitor;
    }

    public ImageProviderType ProviderType => ImageProviderType.Local;
    public string DisplayName => "本地渲染";
    public bool IsConfigured => _configMonitor.CurrentValue.Image.Local.Enabled;
    public IReadOnlyList<string> SupportedModels => new[] { "local" };
    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.ImageGeneration, "MaxResolution: 4096x4096", "image/png")
    };

    public async Task<ImageGenerationResult> GenerateAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();

        var width = Math.Max(320, request.Width);
        var height = Math.Max(240, request.Height);
        var prompt = request.Prompt ?? string.Empty;
        cancellationToken.ThrowIfCancellationRequested();

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        var palette = BuildPalette(prompt);
        using var gradientPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, height),
                new[] { palette.Primary, palette.Secondary },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(new SKRect(0, 0, width, height), gradientPaint);

        using var overlayPaint = new SKPaint
        {
            Color = palette.Overlay,
            IsAntialias = true
        };
        canvas.DrawRect(new SKRect(0, height * 0.65f, width, height), overlayPaint);

        using var titlePaint = new SKPaint
        {
            Color = palette.TextPrimary,
            IsAntialias = true,
            TextSize = Math.Max(18, width / 32f),
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };

        using var bodyPaint = new SKPaint
        {
            Color = palette.TextSecondary,
            IsAntialias = true,
            TextSize = Math.Max(14, width / 40f),
            Typeface = SKTypeface.FromFamilyName("Segoe UI")
        };

        var title = string.IsNullOrWhiteSpace(request.Style) ? "Storyboard Render" : $"{request.Style} Render";
        canvas.DrawText(title, 32, height * 0.72f, titlePaint);

        var lines = WrapText(prompt, 48).Take(5).ToArray();
        var y = height * 0.78f;
        foreach (var line in lines)
        {
            canvas.DrawText(line, 32, y, bodyPaint);
            y += bodyPaint.TextSize + 6;
        }

        using var modelPaint = new SKPaint
        {
            Color = palette.TextMuted,
            IsAntialias = true,
            TextSize = Math.Max(12, width / 50f),
            Typeface = SKTypeface.FromFamilyName("Segoe UI")
        };
        canvas.DrawText($"Model: {request.Model}", 32, height - 24, modelPaint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();

        return new ImageGenerationResult(bytes, ".png", request.Model);
    }

    private static (SKColor Primary, SKColor Secondary, SKColor Overlay, SKColor TextPrimary, SKColor TextSecondary, SKColor TextMuted) BuildPalette(string prompt)
    {
        var hash = prompt.GetHashCode();
        var baseHue = Math.Abs(hash % 360);
        var primary = HslToColor(baseHue, 0.6f, 0.35f);
        var secondary = HslToColor((baseHue + 40) % 360, 0.65f, 0.25f);
        var overlay = new SKColor(0, 0, 0, 160);
        return (primary, secondary, overlay, new SKColor(245, 245, 245), new SKColor(203, 213, 225), new SKColor(148, 163, 184));
    }

    private static SKColor HslToColor(int hue, float saturation, float lightness)
    {
        var h = hue / 360f;
        var q = lightness < 0.5f
            ? lightness * (1 + saturation)
            : lightness + saturation - lightness * saturation;
        var p = 2 * lightness - q;

        var r = HueToRgb(p, q, h + 1f / 3f);
        var g = HueToRgb(p, q, h);
        var b = HueToRgb(p, q, h - 1f / 3f);
        return new SKColor((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }

    private static IEnumerable<string> WrapText(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        for (var i = 0; i < text.Length; i += maxChars)
        {
            var len = Math.Min(maxChars, text.Length - i);
            yield return text.Substring(i, len);
        }
    }
}

using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Storyboard.Converters;

public sealed class AiParseButtonTextConverter : IValueConverter
{
    public static readonly AiParseButtonTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isAiParsing && isAiParsing ? "AI 解析中..." : "AI 解析";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

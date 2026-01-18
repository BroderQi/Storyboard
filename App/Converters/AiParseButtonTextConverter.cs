using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Storyboard.Converters;

public sealed class AiParseButtonTextConverter : IValueConverter
{
    public static readonly AiParseButtonTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isAiParsing && isAiParsing ? "AI单镜解析中..." : "AI单镜解析";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

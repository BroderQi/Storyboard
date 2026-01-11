using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Storyboard.Converters;

public sealed class BooleanNotConverter : IValueConverter
{
    public static readonly BooleanNotConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : null;
    }
}

using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Storyboard.Converters;

public sealed class SelectedBorderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var selected = value is bool b && b;
        return selected
            ? new SolidColorBrush(Color.Parse("#8b5cf6"))
            : new SolidColorBrush(Color.Parse("#3f3f46"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

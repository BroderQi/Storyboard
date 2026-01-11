using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Storyboard.Converters;

public sealed class TimelineShotBorderBrushConverter : IMultiValueConverter
{
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#3f3f46")); // zinc-700
    private static readonly IBrush HoverBrush = new SolidColorBrush(Color.Parse("#a78bfa")); // violet-400
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#8b5cf6")); // violet-500

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = values.Count > 0 && values[0] is bool b0 && b0;
        var isHovered = values.Count > 1 && values[1] is bool b1 && b1;

        if (isSelected)
            return SelectedBrush;
        if (isHovered)
            return HoverBrush;

        return DefaultBrush;
    }
}

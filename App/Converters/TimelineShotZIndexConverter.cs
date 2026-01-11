using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Storyboard.Converters;

public sealed class TimelineShotZIndexConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = values.Count > 0 && values[0] is bool b0 && b0;
        var isHovered = values.Count > 1 && values[1] is bool b1 && b1;

        if (isSelected)
            return 20;
        if (isHovered)
            return 10;
        return 0;
    }
}

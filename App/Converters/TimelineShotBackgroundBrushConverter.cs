using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Storyboard.Converters;

public sealed class TimelineShotBackgroundBrushConverter : IMultiValueConverter
{
    // React reference:
    // - default: bg-zinc-800/50
    // - hovered: bg-violet-400/10
    // - selected: bg-violet-500/20
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#18181b"));
    private static readonly IBrush HoverBrush = new SolidColorBrush(Color.Parse("#1AA78BFA"));
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#338B5CF6"));

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

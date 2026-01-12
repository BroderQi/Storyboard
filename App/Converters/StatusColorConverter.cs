using Avalonia.Data.Converters;
using Avalonia.Media;
using Storyboard.Models;
using System;
using System.Globalization;

namespace Storyboard.Converters;

public sealed class StatusColorConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = values.Count > 0 && values[0] is GenerationJobStatus s ? s : GenerationJobStatus.Queued;

        return status switch
        {
            GenerationJobStatus.Succeeded => new SolidColorBrush(Color.Parse("#22c55e")),
            GenerationJobStatus.Failed => new SolidColorBrush(Color.Parse("#ef4444")),
            GenerationJobStatus.Canceled => new SolidColorBrush(Color.Parse("#f59e0b")),
            GenerationJobStatus.Retrying => new SolidColorBrush(Color.Parse("#f59e0b")),
            GenerationJobStatus.Running => new SolidColorBrush(Color.Parse("#3b82f6")),
            GenerationJobStatus.Queued => new SolidColorBrush(Color.Parse("#71717a")),
            _ => new SolidColorBrush(Color.Parse("#71717a"))
        };
    }
}

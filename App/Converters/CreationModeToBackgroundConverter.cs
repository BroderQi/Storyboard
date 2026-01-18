using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Storyboard.Converters;

public sealed class CreationModeToBackgroundConverter : IValueConverter
{
    public static readonly CreationModeToBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string currentMode && parameter is string targetMode)
        {
            // 如果当前模式匹配目标模式，返回激活状态的背景色
            if (currentMode == targetMode)
            {
                return new SolidColorBrush(Color.Parse("#27272a"));
            }
            // 否则返回透明背景
            return new SolidColorBrush(Colors.Transparent);
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

using Avalonia.Data.Converters;
using System;

namespace Storyboard.Converters;

public static class StringConverters
{
    public static readonly IValueConverter IsNullOrEmpty =
        new FuncValueConverter<string?, bool>(string.IsNullOrWhiteSpace);

    public static readonly IValueConverter IsNotNullOrEmpty =
        new FuncValueConverter<string?, bool>(value => !string.IsNullOrWhiteSpace(value));
}

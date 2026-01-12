using Avalonia.Data.Converters;
using System;

namespace Storyboard.Converters;

public static class IntConverters
{
    public static readonly IValueConverter IsZero =
        new FuncValueConverter<int, bool>(value => value == 0);

    public static readonly IValueConverter IsNotZero =
        new FuncValueConverter<int, bool>(value => value != 0);
}

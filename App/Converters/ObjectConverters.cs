using Avalonia.Data.Converters;
using System;

namespace Storyboard.Converters;

public static class ObjectConverters
{
    public static readonly IValueConverter IsNull =
        new FuncValueConverter<object?, bool>(value => value == null);

    public static readonly IValueConverter IsNotNull =
        new FuncValueConverter<object?, bool>(value => value != null);
}

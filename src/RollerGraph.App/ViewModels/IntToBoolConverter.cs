using System.Globalization;
using Avalonia.Data.Converters;

namespace RollerGraph.App.ViewModels;

/// <summary>
/// Returns true when the bound int is &gt; 0. Pass <c>"invert"</c> as the
/// converter parameter to flip the result (true when value is 0).
/// </summary>
public sealed class IntToBoolConverter : IValueConverter
{
    public static readonly IntToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool result;
        if (value is int i) result = i > 0;
        else if (value is null) result = false;
        else result = System.Convert.ToInt32(value, culture) > 0;

        if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase))
            return !result;
        return result;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

using System.Globalization;
using Avalonia.Data.Converters;

namespace RollerGraph.App.ViewModels;

/// <summary>
/// Returns true when the bound int is &gt; 0; useful for enabling buttons
/// when a counter (e.g. SampleCount) is non-zero.
/// </summary>
public sealed class IntToBoolConverter : IValueConverter
{
    public static readonly IntToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i) return i > 0;
        if (value is null) return false;
        return System.Convert.ToInt32(value, culture) > 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

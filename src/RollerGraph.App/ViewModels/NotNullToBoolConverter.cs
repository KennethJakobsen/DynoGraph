using System.Globalization;
using Avalonia.Data.Converters;

namespace RollerGraph.App.ViewModels;

/// <summary>Returns true when the bound value is non-null and (if string) non-empty.</summary>
public sealed class NotNullToBoolConverter : IValueConverter
{
    public static readonly NotNullToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return false;
        if (value is string s) return !string.IsNullOrEmpty(s);
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RollerGraph.App.ViewModels;

/// <summary>Converts an "#RRGGBB" hex string into a SolidColorBrush.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && Color.TryParse(s, out var c))
            return new SolidColorBrush(c);
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

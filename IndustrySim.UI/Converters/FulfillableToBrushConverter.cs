using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace IndustrySim.UI.Converters;

public class FulfillableToBrushConverter : IValueConverter
{
    public static readonly FulfillableToBrushConverter Instance = new();

    private static readonly IBrush Green = new SolidColorBrush(Color.FromArgb(70, 0, 180, 0));
    private static readonly IBrush Red   = new SolidColorBrush(Color.FromArgb(70, 200, 0, 0));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Green : Red;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

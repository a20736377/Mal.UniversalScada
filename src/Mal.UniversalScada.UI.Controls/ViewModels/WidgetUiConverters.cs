using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Mal.UniversalScada.UI.Controls.ViewModels;

public static class WidgetUiConverters
{
    public static IValueConverter BoolToVisibility { get; } = new BoolToVisibilityConverterImpl();
    public static IValueConverter ColorHexToBrush { get; } = new ColorHexToBrushConverterImpl();

    private class BoolToVisibilityConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility v && v == Visibility.Visible;
    }

    private class ColorHexToBrushConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    return new SolidColorBrush(color);
                }
                catch
                {
                    // ignored
                }
            }
            return new SolidColorBrush(Color.FromRgb(2, 132, 199));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => "#0284C7";
    }
}

public class TankHeightConverter : IMultiValueConverter
{
    public static TankHeightConverter Instance { get; } = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 &&
            values[0] is double ratio &&
            values[1] is double actualHeight &&
            actualHeight > 0)
        {
            var calculated = ratio * actualHeight;
            return Math.Max(0, Math.Min(actualHeight, calculated));
        }
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class BitStatusConverter : IValueConverter
{
    public static BitStatusConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not null && int.TryParse(parameter?.ToString(), out var bitIndex))
        {
            long rawLong = 0;
            if (value is bool b) rawLong = b ? 1 : 0;
            else if (long.TryParse(value.ToString(), out var l)) rawLong = l;

            bool isBitSet = (rawLong & (1L << bitIndex)) != 0;
            return isBitSet ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(51, 65, 85));
        }
        return new SolidColorBrush(Color.FromRgb(51, 65, 85));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

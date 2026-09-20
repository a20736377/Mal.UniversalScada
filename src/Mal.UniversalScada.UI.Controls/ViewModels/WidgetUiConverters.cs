using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Mal.UniversalScada.UI.Controls.ViewModels;

public static class WidgetUiConverters
{
    public static IValueConverter BoolToVisibility { get; } = new BoolToVisibilityConverterImpl();
    public static IValueConverter InverseBoolToVisibility { get; } = new InverseBoolToVisibilityConverterImpl();
    public static IValueConverter ColorHexToBrush { get; } = new ColorHexToBrushConverterImpl();
    public static IValueConverter ColorHexToColor { get; } = new ColorHexToColorConverterImpl();
    public static IValueConverter BoolToFontWeight { get; } = new BoolToFontWeightConverterImpl();
    public static IValueConverter StringToHorizontalAlignment { get; } = new StringToHorizontalAlignmentConverterImpl();
    public static IValueConverter HalfValueConverter { get; } = new HalfValueConverterImpl();
    public static IValueConverter StringEqualsToVisibility { get; } = new StringEqualsToVisibilityConverterImpl();
    public static IValueConverter MinChannelsToVisibility { get; } = new MinChannelsToVisibilityConverterImpl();

    private class InverseBoolToVisibilityConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility.Collapsed;
    }

    private class ColorHexToColorConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    return (Color)ColorConverter.ConvertFromString(hex);
                }
                catch { }
            }
            return Color.FromRgb(16, 185, 129);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Color c ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : "#10B981";
    }

    private class BoolToFontWeightConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? FontWeights.Bold : FontWeights.Normal;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is FontWeight fw && fw == FontWeights.Bold;
    }

    private class StringToHorizontalAlignmentConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                if (s.Equals("Center", StringComparison.OrdinalIgnoreCase)) return HorizontalAlignment.Center;
                if (s.Equals("Right", StringComparison.OrdinalIgnoreCase)) return HorizontalAlignment.Right;
            }
            return HorizontalAlignment.Left;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value?.ToString() ?? "Left";
    }

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

public class TankDimensionConverter : IMultiValueConverter
{
    public static TankDimensionConverter Instance { get; } = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 &&
            values[0] is double ratio &&
            values[1] is double actualDimension &&
            actualDimension > 0)
        {
            var calculated = ratio * actualDimension;
            return Math.Max(0, Math.Min(actualDimension, calculated));
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

public class IoBitStatusConverter : IMultiValueConverter
{
    public static IoBitStatusConverter Instance { get; } = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 3 &&
            values[0] is not null &&
            int.TryParse(parameter?.ToString(), out var bitIndex))
        {
            long rawLong = 0;
            if (values[0] is bool b) rawLong = b ? 1 : 0;
            else if (long.TryParse(values[0].ToString(), out var l)) rawLong = l;

            bool isBitSet = (rawLong & (1L << bitIndex)) != 0;
            var activeHex = values[1] as string ?? "#10B981";
            var inactiveHex = values[2] as string ?? "#334155";

            try
            {
                var colorStr = isBitSet ? activeHex : inactiveHex;
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorStr));
            }
            catch
            {
                return isBitSet ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(51, 65, 85));
            }
        }
        return new SolidColorBrush(Color.FromRgb(51, 65, 85));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class HalfValueConverterImpl : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d) return d / 2.0;
        if (value is float f) return f / 2.0;
        if (value is int i) return i / 2.0;
        return 12.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class StringEqualsToVisibilityConverterImpl : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? v = value?.ToString();
        string? p = parameter?.ToString();
        return string.Equals(v, p, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class MinChannelsToVisibilityConverterImpl : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value != null && int.TryParse(value.ToString(), out var channels) &&
            parameter != null && int.TryParse(parameter.ToString(), out var minRequired))
        {
            return channels >= minRequired ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}




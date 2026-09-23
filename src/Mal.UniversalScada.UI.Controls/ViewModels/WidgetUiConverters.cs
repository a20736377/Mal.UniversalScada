using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mal.UniversalScada.UI.Controls.ViewModels;

public static class WidgetUiConverters
{
    public static IValueConverter BoolToVisibility { get; } = new BoolToVisibilityConverterImpl();
    public static IValueConverter InverseBoolToVisibility { get; } = new InverseBoolToVisibilityConverterImpl();
    public static IValueConverter ColorHexToBrush { get; } = new ColorHexToBrushConverterImpl();
    public static IValueConverter ColorHexToColor { get; } = new ColorHexToColorConverterImpl();
    public static IValueConverter BoolToColorBrush { get; } = new BoolToColorBrushConverterImpl();
    public static IValueConverter BoolToFontWeight { get; } = new BoolToFontWeightConverterImpl();
    public static IValueConverter StringToHorizontalAlignment { get; } = new StringToHorizontalAlignmentConverterImpl();
    public static IValueConverter HalfValueConverter { get; } = new HalfValueConverterImpl();
    public static IValueConverter StringEqualsToVisibility { get; } = new StringEqualsToVisibilityConverterImpl();
    public static IValueConverter MinChannelsToVisibility { get; } = new MinChannelsToVisibilityConverterImpl();
    public static IValueConverter AlarmToBorderBrush { get; } = new AlarmToBorderBrushConverterImpl();
    public static IValueConverter StringNotEmptyToVisibility { get; } = new StringNotEmptyToVisibilityConverterImpl();
    public static IValueConverter StringToStretch { get; } = new StringToStretchConverterImpl();
    public static IValueConverter StringToImageSource { get; } = new StringToImageSourceConverterImpl();

    private class AlarmToBorderBrushConverterImpl : IValueConverter
    {
        private static readonly SolidColorBrush AlarmBrush = new(Color.FromRgb(239, 68, 68)); // #EF4444
        private static readonly SolidColorBrush NormalBrush = new(Color.FromRgb(30, 41, 59));  // #1E293B

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? AlarmBrush : NormalBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private class StringNotEmptyToVisibilityConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNotEmpty = !string.IsNullOrWhiteSpace(value as string);
            bool isInverse = parameter is string p && string.Equals(p, "Inverse", StringComparison.OrdinalIgnoreCase);
            if (isInverse)
            {
                return isNotEmpty ? Visibility.Collapsed : Visibility.Visible;
            }
            return isNotEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private class BoolToColorBrushConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isTrue = value is bool b && b;
            string trueColorHex = "#10B981";
            string falseColorHex = "#64748B";

            if (parameter is string paramStr && paramStr.Contains('|'))
            {
                var parts = paramStr.Split('|');
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])) trueColorHex = parts[0];
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])) falseColorHex = parts[1];
            }

            var chosenHex = isTrue ? trueColorHex : falseColorHex;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(chosenHex);
                if (targetType == typeof(Color))
                {
                    return color;
                }
                return new SolidColorBrush(color);
            }
            catch
            {
                return isTrue ? Brushes.LimeGreen : Brushes.Gray;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

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

public class StringToStretchConverterImpl : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Stretch s) return s;
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            if (Enum.TryParse<Stretch>(str, true, out var parsed))
            {
                return parsed;
            }
        }
        return Stretch.Uniform;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Stretch s ? s.ToString() : "Uniform";
}

public class StringToImageSourceConverterImpl : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ImageSource src) return src;
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            // 支持 URI 协议 (http, https, pack)
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "pack"))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }

            // 支持本地绝对路径或相对路径
            string fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // 避免占用文件锁
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }
        catch
        {
            // 忽略图像加载解析异常，回退呈现空占位
        }

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}




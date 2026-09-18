using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Configurator.Wpf.ViewModels;

public static class DesignerUiConverters
{
    public static IValueConverter NullToCollapsed { get; } = new NullToVisibilityConverter(Visibility.Visible, Visibility.Collapsed);
    public static IValueConverter NullToVisible { get; } = new NullToVisibilityConverter(Visibility.Collapsed, Visibility.Visible);
    public static IValueConverter WidgetTypeVisibility { get; } = new WidgetTypeToVisibilityConverter();
    public static IValueConverter BoolToVisibility { get; } = new BooleanToVisibilityConverter(Visibility.Visible, Visibility.Collapsed);
    public static IValueConverter InverseBoolToVisibility { get; } = new BooleanToVisibilityConverter(Visibility.Collapsed, Visibility.Visible);
    public static IValueConverter InverseBool { get; } = new InverseBooleanConverter();
    public static IValueConverter DataTypeVisibility { get; } = new DataTypeToVisibilityConverter();
    public static IValueConverter CountToVisibility { get; } = new CountToVisibilityConverterImpl();

    private class CountToVisibilityConverterImpl : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count && count > 0) return Visibility.Visible;
            if (value is System.Collections.ICollection coll && coll.Count > 0) return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private class NullToVisibilityConverter : IValueConverter
    {
        private readonly Visibility _notNullVisibility;
        private readonly Visibility _nullVisibility;

        public NullToVisibilityConverter(Visibility notNullVisibility, Visibility nullVisibility)
        {
            _notNullVisibility = notNullVisibility;
            _nullVisibility = nullVisibility;
        }

        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? _notNullVisibility : _nullVisibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private class BooleanToVisibilityConverter : IValueConverter
    {
        private readonly Visibility _trueVisibility;
        private readonly Visibility _falseVisibility;

        public BooleanToVisibilityConverter(Visibility trueVisibility, Visibility falseVisibility)
        {
            _trueVisibility = trueVisibility;
            _falseVisibility = falseVisibility;
        }

        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is true) ? _trueVisibility : _falseVisibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is not true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is not true;
        }
    }

    private class WidgetTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WidgetType currentType && parameter is string allowedTypesStr)
            {
                var allowed = allowedTypesStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (var t in allowed)
                {
                    if (string.Equals(t, currentType.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        return Visibility.Visible;
                    }
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private class DataTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not string targetCategory)
                return Visibility.Visible;

            if (value == null)
            {
                // 未绑定点位时，默认呈现所有基础配置
                return Visibility.Visible;
            }

            if (value is TagDataType dataType)
            {
                bool isNumeric = dataType is TagDataType.Float or TagDataType.Double
                    or TagDataType.Int8 or TagDataType.UInt8
                    or TagDataType.Int16 or TagDataType.UInt16
                    or TagDataType.Int32 or TagDataType.UInt32
                    or TagDataType.Int64 or TagDataType.UInt64;

                bool isFloat = dataType is TagDataType.Float or TagDataType.Double;
                bool isInteger = isNumeric && !isFloat;
                bool isBool = dataType == TagDataType.Bool;

                bool match = targetCategory.ToUpperInvariant() switch
                {
                    "NUMERIC" => isNumeric,
                    "FLOAT" => isFloat,
                    "INTEGER" or "INT" => isInteger,
                    "BOOL" or "BOOLEAN" => isBool,
                    _ => true
                };

                return match ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}

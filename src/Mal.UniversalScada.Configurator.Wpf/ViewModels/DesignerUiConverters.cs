using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Configurator.Wpf.ViewModels;

public static class DesignerUiConverters
{
    public static IValueConverter NullToCollapsed { get; } = new NullToVisibilityConverter(Visibility.Visible, Visibility.Collapsed);
    public static IValueConverter NullToVisible { get; } = new NullToVisibilityConverter(Visibility.Collapsed, Visibility.Visible);
    public static IValueConverter WidgetTypeVisibility { get; } = new WidgetTypeToVisibilityConverter();

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
}

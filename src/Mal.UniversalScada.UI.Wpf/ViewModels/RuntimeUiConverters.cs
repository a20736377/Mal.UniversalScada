using System;
using System.Globalization;
using System.Windows.Data;

namespace Mal.UniversalScada.UI.Wpf.ViewModels;

public static class RuntimeUiConverters
{
    public static IValueConverter EngineStateText { get; } = new EngineStateTextConverter();

    private class EngineStateTextConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
            {
                return "🟢 采集运行中";
            }
            return "⏸️ 采集已暂停";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => true;
    }
}

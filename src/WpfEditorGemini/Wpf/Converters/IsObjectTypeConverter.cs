using RuntimeConfig.Core.Dom;
using System;
using System.Globalization;
using System.Windows.Data;

namespace JsonConfigEditor.Wpf.Converters
{
    public class IsObjectTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is ObjectNode;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 
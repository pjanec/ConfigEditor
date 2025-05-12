using System;
using System.Globalization;
using System.Windows.Data;
using WpfUI.Models;

namespace WpfUI.Converters
{
    public class ArrayItemNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ArrayNode arrayNode)
            {
                var index = arrayNode.Items.IndexOf(parameter as DomNode);
                if (index >= 0)
                {
                    return $"#{index + 1}";
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 
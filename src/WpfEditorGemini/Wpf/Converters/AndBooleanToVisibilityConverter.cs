using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace JsonConfigEditor.Wpf.Converters
{
    /// <summary>
    /// A MultiValueConverter that performs a logical AND operation on its boolean inputs.
    /// Returns Visibility.Visible if all inputs are true, otherwise returns Visibility.Collapsed.
    /// </summary>
    public class AndBooleanToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // If all values are true, return Visible.
            if (values.All(v => v is bool b && b))
            {
                return Visibility.Visible;
            }

            // Otherwise, collapse the element.
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // ConvertBack is not needed for this one-way converter.
            throw new NotImplementedException();
        }
    }
}
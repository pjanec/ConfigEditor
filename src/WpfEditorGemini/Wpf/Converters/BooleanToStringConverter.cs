using System;
using System.Globalization;
using System.Windows.Data;

namespace JsonConfigEditor.Wpf.Converters
{
    public class BooleanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                if (string.Equals(stringValue, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                // Any other string (including "false", "null", or garbage) will result in false.
                // This is a common interpretation for CheckBox IsChecked when bound to non-strictly-boolean strings.
                return false; 
            }
            // If the source isn't a string, or if it's null, treat as false.
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return "false"; // Or perhaps "null" if your ViewModel expects that for unset booleans.
            }
            if (value is bool boolValue)
            {
                return boolValue.ToString().ToLowerInvariant(); // "true" or "false"
            }
            return "false"; // Default for unexpected types
        }
    }
} 
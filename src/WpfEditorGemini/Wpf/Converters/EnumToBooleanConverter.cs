using System;
using System.Globalization;
using System.Windows.Data;

namespace JsonConfigEditor.Wpf.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string checkValue = value.ToString();
            string targetValue = parameter.ToString();
            return checkValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return null;

            bool useValue = (bool)value;
            if (useValue)
            {
                var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                return Enum.Parse(enumType, parameter.ToString());
            }

            return Binding.DoNothing;
        }
    }
} 
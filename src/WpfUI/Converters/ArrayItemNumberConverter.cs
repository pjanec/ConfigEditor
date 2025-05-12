using System;
using System.Globalization;
using System.Windows.Data;
using WpfUI.Models;

namespace WpfUI.Converters
{
    /// <summary>
    /// Converts array item index to a numbered display format (e.g., "Item 1", "Item 2").
    /// </summary>
    public class ArrayItemNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DomNode node && node.Parent is ArrayNode arrayNode)
            {
                var index = arrayNode.Items.IndexOf(node);
                return $"Item {index + 1}";
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 
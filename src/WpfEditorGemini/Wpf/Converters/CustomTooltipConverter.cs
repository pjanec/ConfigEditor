using JsonConfigEditor.ViewModels;
using JsonConfigEditor.Wpf.Services;
using System;
using System.Globalization;
using System.Windows.Data;
using RuntimeConfig.Core.Dom;

namespace JsonConfigEditor.Wpf.Converters
{
    public class CustomTooltipConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
                return null;

            if (values[0] is not DataGridRowItemViewModel vm || values[1] is not CustomUIRegistryService uiRegistry)
                return null;

            if (vm.SchemaContextNode?.ClrType == null)
                return null;

            var tooltipProvider = uiRegistry.GetTooltipProvider(vm.SchemaContextNode.ClrType);
            if (tooltipProvider != null)
            {
                return tooltipProvider.GetTooltipContent(vm);
            }

            // Default tooltip behavior if no custom provider
            // You could return vm.ValidationErrorMessage if !vm.IsValid, or other default tooltips here.
            if (!vm.IsValid && !string.IsNullOrEmpty(vm.ValidationErrorMessage))
            {
                return vm.ValidationErrorMessage;
            }
            
            // For RefNodes, show full path by default if no custom provider (as per specs 2.3.4)
            if (vm.DomNode is RuntimeConfig.Core.Dom.RefNode refNode)
            {
                return refNode.ReferencePath;
            }

            return null; // No tooltip
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 
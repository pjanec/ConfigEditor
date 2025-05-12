using System.Windows;
using System.Windows.Controls;
using WpfUI.ViewModels;

namespace WpfUI.Controls;

/// <summary>
/// Selects the appropriate template for rendering or editing a DOM node.
/// </summary>
public class EditorTemplateSelector : DataTemplateSelector
{
    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is DomNodeViewModel vm)
        {
            if (vm.IsEditing)
            {
                // Return editor template based on type
                var key = vm.ValueClrType.Name + "EditorTemplate";
                return Application.Current.Resources[key] as DataTemplate;
            }
            else
            {
                // Return renderer template based on type
                var key = vm.ValueClrType.Name + "RendererTemplate";
                return Application.Current.Resources[key] as DataTemplate;
            }
        }

        return null;
    }
} 
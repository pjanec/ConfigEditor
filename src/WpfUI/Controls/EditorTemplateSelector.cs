using System.Windows;
using System.Windows.Controls;
using WpfUI.ViewModels;

namespace WpfUI.Controls;

public class EditorTemplateSelector : DataTemplateSelector
{
    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is not DomNodeViewModel vm)
            return null!;

        var resourceKey = vm.IsEditing
            ? $"{vm.ValueClrType.Name}EditorTemplate"
            : $"{vm.ValueClrType.Name}RendererTemplate";

        return Application.Current.Resources[resourceKey] as DataTemplate
            ?? Application.Current.Resources["DefaultRendererTemplate"] as DataTemplate
            ?? null!;
    }
} 
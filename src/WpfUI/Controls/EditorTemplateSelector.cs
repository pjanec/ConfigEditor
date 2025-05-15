using System.Windows;
using System.Windows.Controls;
using WpfUI.ViewModels;

namespace WpfUI.Controls;

/// <summary>
/// Selects the appropriate template for rendering or editing a DOM node.
/// </summary>
public class EditorTemplateSelector : DataTemplateSelector
{
	public override DataTemplate SelectTemplate( object item, DependencyObject container )
	{
		if( item is DomNodeViewModel vm )
		{
			if( vm.ValueClrType == null ) // container nodes
				return null;

			// Traverse up the visual tree to find the MainWindow
			var mainWindow = Window.GetWindow( container );
			if( mainWindow == null )
				return null;

			if( vm.IsEditing )
			{
				// Look for editor template in MainWindow resources
				var key = vm.ValueClrType.Name + "EditorTemplate";
				return mainWindow.Resources[key] as DataTemplate;
			}
			else
			{
				// Look for renderer template in MainWindow resources
				var key = vm.ValueClrType.Name + "RendererTemplate";
				return mainWindow.Resources[key] as DataTemplate;
			}
		}

		return null;
	}
} 
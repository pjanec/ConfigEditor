// --- File: JsonConfigEditor.Contracts/Rendering/IValueRenderer.cs ---
using System.Windows; // For FrameworkElement or DataTemplate
using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.Core.Schema;

namespace JsonConfigEditor.Contracts.Rendering
{
    /// <summary>
    /// Defines the contract for a component that provides a custom visual representation (renderer)
    /// for a DomNode's value when it's NOT in edit mode in the DataGrid.
    /// Implementations of this interface will be discovered via [ValueRendererAttribute].
    /// (From specification document, Section 2.3.2)
    /// </summary>
    public interface IValueRenderer
    {
        /// <summary>
        /// Creates or provides a FrameworkElement to display the value of the DomNode.
        /// This control will be hosted within the DataGrid cell.
        /// The FrameworkElement should ideally bind to properties of the provided viewModel.
        /// </summary>
        /// <param name="viewModel">The ViewModel of the DataGrid row item, providing access to DomNode, SchemaNode, and other states.</param>
        /// <returns>A FrameworkElement to be used for displaying the value, or null to use default rendering.</returns>
        FrameworkElement? CreateDisplayControl(object viewModel); // viewModel will be DataGridRowItemViewModel
    }
} 
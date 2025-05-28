using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.Core.Schema;

namespace JsonConfigEditor.Contracts.Tooltips
{
    /// <summary>
    /// Defines the contract for a component that provides custom tooltip content
    /// for a DomNode when hovered over in the DataGrid.
    /// Implementations of this interface will be discovered via [TooltipProviderAttribute].
    /// (From specification document, Section 2.3.4)
    /// </summary>
    public interface ITooltipProvider
    {
        /// <summary>
        /// Gets the content to be displayed in a tooltip for the given DomNode.
        /// This can be a simple string, or a more complex UIElement.
        /// </summary>
        /// <param name="viewModel">The ViewModel of the DataGrid row item, providing access to DomNode, SchemaNode, etc.</param>
        /// <returns>An object to be used as the tooltip content. Return null or empty if no custom tooltip should be shown by this provider.</returns>
        object? GetTooltipContent(object viewModel); // viewModel will be DataGridRowItemViewModel
    }
} 
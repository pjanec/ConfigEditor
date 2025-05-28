using System.Windows;
using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.Core.Schema;

namespace JsonConfigEditor.Contracts.Editors
{
    /// <summary>
    /// Defines the contract for a component that provides a custom editing control
    /// for a DomNode's value when it IS in edit mode in the DataGrid.
    /// Implementations of this interface will be discovered via [ValueEditorAttribute].
    /// (From specification document, Section 2.4)
    /// </summary>
    public interface IValueEditor
    {
        /// <summary>
        /// Gets a DataTemplate to edit the value of the DomNode.
        /// This template will be used for the DataGrid cell content when in edit mode (unless RequiresModal is true).
        /// The DataTemplate should bind to properties like 'EditValue' on the provided viewModel.
        /// </summary>
        /// <param name="viewModel">The ViewModel of the DataGrid row item, providing access to DomNode, SchemaNode, EditValue, etc.</param>
        /// <returns>A DataTemplate to be used for editing the value, or null to use a default editor.</returns>
        DataTemplate? GetEditTemplate(object viewModel); // viewModel will be DataGridRowItemViewModel

        /// <summary>
        /// Gets a value indicating whether this custom editor requires a modal dialog for editing,
        /// rather than being hosted in-place in the DataGrid cell.
        /// (From specification document, Section 2.4)
        /// </summary>
        bool RequiresModal { get; }

        // Optional: Methods to signal when editing is complete or cancelled from within the custom editor,
        // if the editor needs more control than standard focus loss or Enter/Esc keys.
        // event EventHandler EditCommitted;
        // event EventHandler EditCancelled;
    }
} 
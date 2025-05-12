using System.Collections.Generic;

namespace WpfUI.Models
{
    /// <summary>
    /// Serializable state object that captures the editor's UI state for persistence.
    /// Stores expanded nodes, selection, filter text and last edited node.
    /// </summary>
    public class DomEditorUiState
    {
        /// <summary>
        /// List of node paths that were expanded in the tree view.
        /// Used to restore the tree's expansion state.
        /// </summary>
        public List<string> ExpandedNodePaths { get; set; } = new();

        /// <summary>
        /// Current filter text applied to the tree view.
        /// Used to restore the filter state.
        /// </summary>
        public string FilterText { get; set; } = string.Empty;

        /// <summary>
        /// List of node paths that were selected in the tree view.
        /// Used to restore the selection state.
        /// </summary>
        public List<string> SelectedNodePaths { get; set; } = new();

        /// <summary>
        /// Path of the last node that was being edited.
        /// Used to restore the edit state.
        /// </summary>
        public string? LastEditNodePath { get; set; }
    }
} 
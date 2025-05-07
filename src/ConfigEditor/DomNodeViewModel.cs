using System;
using System.Collections.Generic;

namespace ConfigDom
{
    /// <summary>
    /// ViewModel wrapper for a DomNode in the editor context.
    /// Adds metadata needed for interactive editing, schema display, and validation.
    /// </summary>
    public class DomNodeViewModel
    {
        /// <summary>
        /// The wrapped DOM node that this viewmodel represents.
        /// </summary>
        public DomNode Node { get; }

        /// <summary>
        /// Indicates whether the node has unsaved changes.
        /// </summary>
        public bool IsDirty { get; private set; }

        /// <summary>
        /// Indicates whether the node can be edited (based on provider).
        /// </summary>
        public bool IsEditable { get; }

        /// <summary>
        /// Optional schema metadata associated with this node.
        /// </summary>
        public ISchemaNode? AttachedSchema { get; set; }

        /// <summary>
        /// Optional error status provider, set after validation.
        /// </summary>
        public IErrorStatusProvider? ValidationStatus { get; set; }

        /// <summary>
        /// Children ViewModels for object or array nodes.
        /// </summary>
        public List<DomNodeViewModel> Children { get; } = new();

        /// <summary>
        /// Constructs a new viewmodel wrapper for the given node.
        /// </summary>
        /// <param name="node">The node being wrapped.</param>
        /// <param name="isEditable">Whether the node is editable based on context.</param>
        public DomNodeViewModel(DomNode node, bool isEditable)
        {
            Node = node;
            IsEditable = isEditable;
        }

        /// <summary>
        /// Marks this node as dirty (changed by user edit).
        /// </summary>
        public void MarkDirty()
        {
            IsDirty = true;
        }

        /// <summary>
        /// Clears the dirty flag (usually after save).
        /// </summary>
        public void ClearDirty()
        {
            IsDirty = false;
        }
    }
}

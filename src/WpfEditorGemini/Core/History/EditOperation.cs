using RuntimeConfig.Core.Dom;
using JsonConfigEditor.ViewModels;

namespace JsonConfigEditor.Core.History
{
    /// <summary>
    /// Abstract base class for a single, reversible edit action.
    /// Each operation knows how to apply its change and how to reverse it.
    /// It also stores the layer index where the change occurred.
    /// </summary>
    public abstract class EditOperation
    {
        /// <summary>
        /// The index of the cascade layer this operation applies to.
        /// </summary>
        public int LayerIndex { get; }

        /// <summary>
        /// Gets a value indicating if this operation changes the structure of the tree (add/remove)
        /// and thus requires a full refresh of the displayed list.
        /// </summary>
        public virtual bool RequiresFullRefresh => false;

        /// <summary>
        /// Gets the unique path of the primary DomNode affected by this operation.
        /// Can be null if the operation is not node-specific (e.g., global changes).
        /// </summary>
        public abstract string? NodePath { get; }

        protected EditOperation(int layerIndex)
        {
            LayerIndex = layerIndex;
        }

        /// <summary>
        /// Applies the "forward" action (Redo).
        /// </summary>
        public abstract void Redo(MainViewModel vm);

        /// <summary>
        /// Applies the "reverse" action (Undo).
        /// </summary>
        public abstract void Undo(MainViewModel vm);
    }
} 
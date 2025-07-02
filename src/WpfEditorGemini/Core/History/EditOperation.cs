using JsonConfigEditor.Core.Dom;
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
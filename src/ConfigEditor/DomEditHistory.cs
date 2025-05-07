using System.Collections.Generic;

namespace ConfigDom
{
    /// <summary>
    /// Manages an in-memory stack of edit actions to support undo and redo.
    /// Used by editable DOM contexts such as FlatJsonEditorContext or Json5CascadeEditorContext.
    /// </summary>
    public class DomEditHistory
    {
        private readonly Stack<DomEditAction> _undoStack = new();
        private readonly Stack<DomEditAction> _redoStack = new();

        /// <summary>
        /// Applies a new edit action and pushes it onto the undo stack.
        /// Clears the redo stack.
        /// </summary>
        /// <param name="action">The edit action just performed.</param>
        public void Apply(DomEditAction action)
        {
            _undoStack.Push(action);
            _redoStack.Clear();
        }

        /// <summary>
        /// Undoes the last action and returns its inverse for reapplication.
        /// </summary>
        /// <returns>The inverse edit action to apply, or null if none.</returns>
        public DomEditAction? Undo()
        {
            if (_undoStack.Count == 0) return null;
            var action = _undoStack.Pop();
            var inverse = action.GetInverse();
            _redoStack.Push(inverse);
            return inverse;
        }

        /// <summary>
        /// Redoes the last undone action and returns its inverse.
        /// </summary>
        /// <returns>The inverse edit action to apply, or null if none.</returns>
        public DomEditAction? Redo()
        {
            if (_redoStack.Count == 0) return null;
            var action = _redoStack.Pop();
            var inverse = action.GetInverse();
            _undoStack.Push(inverse);
            return inverse;
        }

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
    }
}

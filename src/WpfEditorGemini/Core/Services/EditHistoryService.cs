using JsonConfigEditor.Core.History;
using JsonConfigEditor.ViewModels;
using System;
using System.Collections.Generic;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// Manages the in-memory stacks of edit actions to support undo and redo.
    /// This service centralizes all history logic, decoupling it from the MainViewModel.
    /// </summary>
    public class EditHistoryService
    {
        private readonly Stack<EditOperation> _undoStack = new();
        private readonly Stack<EditOperation> _redoStack = new();
        private readonly MainViewModel _mainViewModel;

        /// <summary>
        /// This event is fired whenever the data model is changed by an undo or redo operation,
        /// signaling to the MainViewModel that it needs to refresh its views.
        /// </summary>
        public event Action<EditOperation>? ModelChanged;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public EditHistoryService(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }

        /// <summary>
        /// Records a new edit action, pushing it onto the undo stack and clearing the redo stack.
        /// </summary>
        public void Record(EditOperation operation)
        {
            _undoStack.Push(operation);
            _redoStack.Clear();
            // The Redo call is now made by the MainViewModel after this returns.
            // This ensures the model isn't changed until after the DataGrid commit completes.
            ModelChanged?.Invoke(operation);
        }

        /// <summary>
        /// Undoes the last action.
        /// </summary>
        public void Undo()
        {
            if (!CanUndo) return;

            var operation = _undoStack.Pop();
            operation.Undo(_mainViewModel); // The operation itself knows how to reverse
            _redoStack.Push(operation);

            ModelChanged?.Invoke(operation); // Notify that the model has changed
        }

        /// <summary>
        /// Redoes the last undone action.
        /// </summary>
        public void Redo()
        {
            if (!CanRedo) return;

            var operation = _redoStack.Pop();
            operation.Redo(_mainViewModel); // The operation knows how to re-apply
            _undoStack.Push(operation);

            ModelChanged?.Invoke(operation); // Notify that the model has changed
        }

        /// <summary>
        /// Clears all history. Called when a new file is loaded.
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
} 
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Represents a single edit operation in the DOM.
    /// Stores both old and new values and the path being modified.
    /// Enables undo/redo and change tracking.
    /// </summary>
    public class DomEditAction
    {
        /// <summary>
        /// The absolute path of the value being edited.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The previous value at the specified path.
        /// </summary>
        public JsonElement OldValue { get; }

        /// <summary>
        /// The updated value at the specified path.
        /// </summary>
        public JsonElement NewValue { get; }

        /// <summary>
        /// Initializes an edit action to represent a change at a given path.
        /// </summary>
        /// <param name="path">Absolute DOM path of the affected value.</param>
        /// <param name="oldValue">Original JSON value before edit.</param>
        /// <param name="newValue">Updated JSON value after edit.</param>
        public DomEditAction(string path, JsonElement oldValue, JsonElement newValue)
        {
            Path = path;
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>
        /// Produces an inverse of this edit action, suitable for undo.
        /// </summary>
        /// <returns>A new DomEditAction that reverses this one.</returns>
        public DomEditAction GetInverse()
        {
            return new DomEditAction(Path, NewValue, OldValue);
        }

        /// <summary>
        /// Applies this edit to the given DOM root.
        /// Requires a mutable DOM tree and a working path resolution helper.
        /// </summary>
        /// <param name="root">The DOM root where the change should be applied.</param>
        public void Apply(DomNode root)
        {
            // Assumes a helper exists to locate and modify target node by path
            DomTreePathHelper.SetValueAtPath(root, Path, NewValue);
        }
    }
}

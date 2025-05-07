using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Factory helper to construct DomEditAction by capturing the current DOM value automatically.
    /// </summary>
    public static class DomEditActionFactory
    {
        /// <summary>
        /// Creates a DomEditAction from a given path and new value, automatically capturing the existing value.
        /// </summary>
        /// <param name="root">Root of the DOM subtree.</param>
        /// <param name="path">Absolute path to the target node.</param>
        /// <param name="newValue">The value to apply.</param>
        /// <returns>A complete DomEditAction with old/new state.</returns>
        public static DomEditAction CreateWithSnapshot(DomNode root, string path, JsonElement newValue)
        {
            var oldValue = ExportJsonSubtree.Get(root, path) ?? new JsonElement();
            return new DomEditAction(path, newValue, oldValue);
        }
    }
}

using System;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Utility class to extract and export a resolved subtree from a given root node.
    /// Can be used in runtime or editor to extract a specific JSON subtree.
    /// </summary>
    public static class ExportJsonSubtree
    {
        /// <summary>
        /// Finds a node by absolute path and returns its JSON representation.
        /// </summary>
        /// <param name="root">The root node of the DOM tree.</param>
        /// <param name="path">The absolute slash-separated path (e.g. "config/env1/network/ip").</param>
        /// <returns>The JsonElement representing the subtree, or null if not found.</returns>
        public static JsonElement? Get(DomNode root, string path)
        {
            var node = DomTreePathHelper.FindNodeAtPath(root, path);
            return node?.ExportJson();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Provides utilities to flatten a hierarchical DOM tree into path-value pairs
    /// and to rebuild a tree from such a flat map. Supports indexed array paths.
    /// </summary>
    public static class DomFlatteningService
    {
        /// <summary>
        /// Flattens a DOM subtree rooted at the given node into a dictionary of path → value.
        /// </summary>
        /// <param name="root">The root DOM node to flatten.</param>
        /// <returns>A dictionary mapping string paths to exported JsonElement values.</returns>
        public static Dictionary<string, JsonElement> Flatten(DomNode root)
        {
            var result = new Dictionary<string, JsonElement>();
            FlattenRecursive(root, root.Name, result);
            return result;
        }

        private static void FlattenRecursive(DomNode node, string path, Dictionary<string, JsonElement> result)
        {
            switch (node)
            {
                case LeafNode leaf:
                    result[path] = leaf.Value;
                    break;
                case RefNode refNode:
                    result[path] = refNode.ExportJson();
                    break;
                case ObjectNode obj:
                    foreach (var child in obj.Children)
                    {
                        FlattenRecursive(child.Value, path + "/" + child.Key, result);
                    }
                    break;
                case ArrayNode arr:
                    for (int i = 0; i < arr.Items.Count; i++)
                    {
                        FlattenRecursive(arr.Items[i], path + "/" + i, result);
                    }
                    break;
            }
        }

        /// <summary>
        /// Rebuilds a tree from a flattened dictionary of path → JsonElement.
        /// </summary>
        /// <param name="flatMap">The flat map to rebuild from.</param>
        /// <param name="rootName">The name of the root node.</param>
        /// <returns>The root node of the rebuilt tree.</returns>
        public static ObjectNode Rebuild(Dictionary<string, JsonElement> flatMap, string rootName)
        {
            var root = new ObjectNode(rootName);
            foreach (var (path, value) in flatMap)
            {
                DomTreePathHelper.SetValueAtPath(root, path, value);
            }
            return root;
        }
    }
}

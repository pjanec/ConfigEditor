using System.Collections.Generic;

namespace ConfigDom
{
    /// <summary>
    /// Provides functionality to merge multiple JSON object trees from different cascade levels.
    /// Performs deep object merging, with arrays replaced completely.
    /// Used to build the effective DOM for cascaded configurations.
    /// </summary>
    public static class JsonMergeService
    {
        /// <summary>
        /// Merges a list of object-node maps from cascade layers.
        /// Each dictionary maps relative paths to partial object trees.
        /// </summary>
        /// <param name="layers">A list of layer maps from lowest (base) to highest (most specific).</param>
        /// <returns>A merged root ObjectNode representing the combined DOM.</returns>
        public static ObjectNode MergeCascade(List<Dictionary<string, ObjectNode>> layers)
        {
            var root = new ObjectNode("root");

            foreach (var layer in layers)
            {
                foreach (var (path, node) in layer)
                {
                    ApplyObjectNode(root, path.Split('/'), node);
                }
            }

            return root;
        }

        private static void ApplyObjectNode(ObjectNode targetRoot, string[] pathParts, ObjectNode source)
        {
            ObjectNode current = targetRoot;
            for (int i = 0; i < pathParts.Length - 1; i++)
            {
                string part = pathParts[i];
                if (!current.TryGetChild(part, out var child))
                {
                    child = new ObjectNode(part, current);
                    current.AddChild(child);
                }
                current = (ObjectNode)child;
            }

            string leafKey = pathParts[^1];
            if (current.TryGetChild(leafKey, out var existing))
            {
                if (existing is ObjectNode existingObj && source is ObjectNode sourceObj)
                {
                    foreach (var child in sourceObj.Children)
                    {
                        existingObj.AddChild(child.Value); // override or add
                    }
                }
                else
                {
                    current.RemoveChild(leafKey);
                    current.AddChild(source);
                }
            }
            else
            {
                current.AddChild(source);
            }
        }
    }
}

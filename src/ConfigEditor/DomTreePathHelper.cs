using System;
using System.Linq;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Utility class to resolve and manipulate DOM paths.
    /// Supports deep lookup and value replacement based on string paths.
    /// </summary>
    public static class DomTreePathHelper
    {
        /// <summary>
        /// Resolves a node at the specified slash-separated path.
        /// </summary>
        /// <param name="root">Root node to begin search from.</param>
        /// <param name="path">Slash-separated absolute path.</param>
        /// <returns>The node if found; otherwise null.</returns>
        public static DomNode? FindNodeAtPath(DomNode root, string path)
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            DomNode? current = root;

            foreach (var part in parts.Skip(1)) // skip the root name
            {
                switch (current)
                {
                    case ObjectNode obj when obj.TryGetChild(part, out var child):
                        current = child;
                        break;
                    case ArrayNode arr when int.TryParse(part, out int index) && index < arr.Items.Count:
                        current = arr.Items[index];
                        break;
                    default:
                        return null;
                }
            }
            return current;
        }

        /// <summary>
        /// Assigns a new value to a specific path in the DOM tree.
        /// Replaces or creates leaf nodes along the path.
        /// </summary>
        /// <param name="root">Root node to modify.</param>
        /// <param name="path">Slash-separated absolute path.</param>
        /// <param name="value">Value to set.</param>
        public static void SetValueAtPath(DomNode root, string path, JsonElement value)
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            DomNode? current = root;

            for (int i = 1; i < parts.Length - 1; i++) // skip root, leave last
            {
                var part = parts[i];
                if (current is ObjectNode obj)
                {
                    if (!obj.TryGetChild(part, out var child))
                    {
                        child = new ObjectNode(part, obj);
                        obj.AddChild(child);
                    }
                    current = child;
                }
                else if (current is ArrayNode arr && int.TryParse(part, out var index))
                {
                    while (arr.Items.Count <= index)
                        arr.Items.Add(new ObjectNode(index.ToString(), arr));
                    current = arr.Items[index];
                }
            }

            var leafName = parts[^1];
            if (current is ObjectNode finalObj)
            {
                if (finalObj.TryGetChild(leafName, out _))
                    finalObj.RemoveChild(leafName);
                finalObj.AddChild(new LeafNode(leafName, value, finalObj));
            }
        }

        /// <summary>
        /// Ensures the path exists in the tree, creating object nodes as needed.
        /// Returns the node at the path.
        /// </summary>
        public static DomNode EnsurePathExists(DomNode root, string path)
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            DomNode current = root;

            for (int i = 1; i < parts.Length; i++) // skip root name
            {
                var part = parts[i];

                if (current is ObjectNode obj)
                {
                    if (!obj.TryGetChild(part, out var child))
                    {
                        child = new ObjectNode(part, obj);
                        obj.AddChild(child);
                    }
                    current = child!;
                }
                else if (current is ArrayNode arr && int.TryParse(part, out var index))
                {
                    while (arr.Items.Count <= index)
                        arr.Items.Add(new ObjectNode(index.ToString(), arr));
                    current = arr.Items[index];
                }
                else
                {
                    throw new InvalidOperationException($"Cannot descend into {current?.GetType().Name} at {part}");
                }
            }

            return current;
        }
    }
}

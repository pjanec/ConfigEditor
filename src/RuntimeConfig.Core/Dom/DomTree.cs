using System;

namespace RuntimeConfig.Core.Dom
{
    /// <summary>
    /// Provides utility methods for working with DOM trees.
    /// </summary>
    public static class DomTree
    {
        /// <summary>
        /// Performs a deep clone of a DomNode, correctly setting the parent for all descendants.
        /// </summary>
        /// <param name="node">The node to clone.</param>
        /// <param name="newParent">The parent to assign to the top-level cloned node.</param>
        /// <returns>A new, deep-cloned DomNode instance with a correct parent hierarchy.</returns>
        public static DomNode CloneNode(DomNode node, DomNode? newParent)
        {
            if (node is ValueNode valueNode)
            {
                return new ValueNode(valueNode.Name, newParent, valueNode.Value);
            }
            if (node is RefNode refNode)
            {
                return new RefNode(refNode.Name, newParent, refNode.ReferencePath, refNode.OriginalValue);
            }
            if (node is ArrayNode arrayNode)
            {
                var newArray = new ArrayNode(arrayNode.Name, newParent);
                foreach (var item in arrayNode.Items)
                {
                    // Pass the newArray as the parent for each cloned item.
                    newArray.AddItem(CloneNode(item, newArray));
                }
                return newArray;
            }
            if (node is ObjectNode objectNode)
            {
                var newObject = new ObjectNode(objectNode.Name, newParent);
                foreach (var (key, child) in objectNode.Children)
                {
                    // Pass the newObject as the parent for each cloned child.
                    newObject.AddChild(key, CloneNode(child, newObject));
                }
                return newObject;
            }
            throw new NotSupportedException($"Unsupported node type for cloning: {node.GetType().Name}");
        }

        /// <summary>
        /// Finds a node in the DOM tree by its path.
        /// </summary>
        /// <param name="rootNode">The root node to search from.</param>
        /// <param name="path">The path to the node (e.g., "section/subsection/property").</param>
        /// <returns>The found node, or null if not found.</returns>
        public static DomNode? FindNodeByPath(DomNode? rootNode, string path)
        {
            if (rootNode == null || string.IsNullOrEmpty(path))
                return null;

            // First, check if the path is an exact match for the root node's path.
            // This correctly handles the case where path is "$root".
            if (rootNode.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                return rootNode;
            }

            // Normalize the path by stripping the root's path prefix if it exists.
            // This is the crucial logic from the old implementation.
            string normalizedPath = path.StartsWith(rootNode.Path + "/", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(rootNode.Path.Length + 1)
                : path;

            var pathSegments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentNode = rootNode;

            foreach (var segment in pathSegments)
            {
                if (currentNode is ObjectNode objectNode)
                {
                    currentNode = objectNode.GetChild(segment);
                }
                else if (currentNode is ArrayNode arrayNode)
                {
                    if (int.TryParse(segment, out int index))
                    {
                        currentNode = arrayNode.GetItem(index);
                    }
                    else
                    {
                        return null; // Invalid array index
                    }
                }
                else
                {
                    return null; // Can't traverse further
                }

                if (currentNode == null)
                    return null;
            }

            return currentNode;
        }
    }
} 
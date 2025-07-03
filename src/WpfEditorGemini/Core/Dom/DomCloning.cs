using System;

namespace JsonConfigEditor.Core.Dom
{
    /// <summary>
    /// Provides a centralized, shared utility for performing a deep clone of a DomNode tree.
    /// This ensures that cloning logic is consistent across the application, particularly
    /// for services like merging and saving that need to create new node instances.
    /// </summary>
    public static class DomCloning
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
    }
} 
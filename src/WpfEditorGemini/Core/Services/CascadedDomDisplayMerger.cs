
using JsonConfigEditor.Core.Cascade;
using JsonConfigEditor.Core.Dom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// Represents the complete result of merging multiple layers for display.
    /// </summary>
    /// <param name="MergedRoot">The final, merged DomNode tree.</param>
    /// <param name="ValueOrigins">A map from a DOM path to the index of the layer that provided its final, effective value.</param>
    /// <param name="OverrideSources">A map from a DOM path to a list of ALL layer indices that define a value for that path.</param>
    public record DisplayMergeResult(
        ObjectNode MergedRoot,
        Dictionary<string, int> ValueOrigins,
        Dictionary<string, List<int>> OverrideSources
    );

    /// <summary>
    /// Merges multiple cascade layers into a single DOM tree for display purposes.
    /// It also generates the origin tracking maps required by the UI.
    /// </summary>
    public class CascadedDomDisplayMerger
    {
        /// <summary>
        /// Merges a list of layers, starting with schema defaults, into a single result for UI display.
        /// </summary>
        /// <param name="layersToMerge">The ordered list of cascade layers to merge (lowest to highest priority).</param>
        /// <param name="schemaDefaultsRoot">The root node containing schema defaults, acting as Layer 0.</param>
        /// <returns>A DisplayMergeResult containing the merged tree and origin maps.</returns>
        public DisplayMergeResult MergeForDisplay(IReadOnlyList<CascadeLayer> layersToMerge, ObjectNode schemaDefaultsRoot)
        {
            var valueOrigins = new Dictionary<string, int>();
            var overrideSources = new Dictionary<string, List<int>>();

            // 1. Start with a clone of the schema defaults as our base
            var mergedRoot = (ObjectNode)CloneNode(schemaDefaultsRoot);
            
            // 2. Initialize origin maps with Layer 0 (Schema Defaults)
            TrackOriginsRecursive(mergedRoot, 0, valueOrigins, overrideSources);

            // 3. Merge each subsequent layer on top of the result
            foreach (var layer in layersToMerge)
            {
                // Layer index must be offset by 1 because Layer 0 is the schema defaults.
                int effectiveLayerIndex = layer.LayerIndex + 1;
                MergeLayerIntoRecursive(mergedRoot, layer.LayerConfigRootNode, effectiveLayerIndex, valueOrigins, overrideSources);
            }

            return new DisplayMergeResult(mergedRoot, valueOrigins, overrideSources);
        }

        /// <summary>
        /// Recursively merges a source layer's tree into the target merged tree.
        /// </summary>
        private void MergeLayerIntoRecursive(ObjectNode targetParent, ObjectNode sourceParent, int layerIndex, Dictionary<string, int> valueOrigins, Dictionary<string, List<int>> overrideSources)
        {
            foreach (var (childKey, sourceChild) in sourceParent.Children)
            {
                if (targetParent.Children.TryGetValue(childKey, out var existingChild))
                {
                    // A node with this key already exists.
                    if (existingChild is ObjectNode existingObject && sourceChild is ObjectNode sourceObject)
                    {
                        // Both are objects, so recurse into them.
                        MergeLayerIntoRecursive(existingObject, sourceObject, layerIndex, valueOrigins, overrideSources);
                    }
                    else
                    {
                        // Conflict or replacement. The source node wins and replaces the target.
                        // Example: Base has an object, Override has a value. The value replaces the object.
                        var clonedSourceChild = CloneNode(sourceChild);
                        targetParent.AddChild(clonedSourceChild.Name, clonedSourceChild); // This sets the new parent and path
                        TrackOriginsRecursive(clonedSourceChild, layerIndex, valueOrigins, overrideSources);
                    }
                }
                else
                {
                    // Node doesn't exist in the target, so we add it.
                    var clonedSourceChild = CloneNode(sourceChild);
                    targetParent.AddChild(clonedSourceChild.Name, clonedSourceChild);
                    TrackOriginsRecursive(clonedSourceChild, layerIndex, valueOrigins, overrideSources);
                }
            }
        }
        
        /// <summary>
        /// Recursively populates the origin maps for a node and all of its children.
        /// </summary>
        private void TrackOriginsRecursive(DomNode node, int layerIndex, Dictionary<string, int> valueOrigins, Dictionary<string, List<int>> overrideSources)
        {
            // This layer is now the final, winning source for this path.
            valueOrigins[node.Path] = layerIndex;

            // Add this layer to the list of all sources for this path.
            if (!overrideSources.TryGetValue(node.Path, out var sources))
            {
                sources = new List<int>();
                overrideSources[node.Path] = sources;
            }
            if (!sources.Contains(layerIndex))
            {
                sources.Add(layerIndex);
            }

            // Recurse to children
            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    TrackOriginsRecursive(child, layerIndex, valueOrigins, overrideSources);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.Items)
                {
                    TrackOriginsRecursive(item, layerIndex, valueOrigins, overrideSources);
                }
            }
        }
        
        /// <summary>
        /// Performs a deep clone of a DomNode.
        /// </summary>
        private DomNode CloneNode(DomNode node)
        {
            if (node is ValueNode valueNode)
            {
                return new ValueNode(valueNode.Name, null, valueNode.Value);
            }
            if (node is RefNode refNode)
            {
                return new RefNode(refNode.Name, null, refNode.ReferencePath, refNode.OriginalValue);
            }
            if (node is ArrayNode arrayNode)
            {
                var newArray = new ArrayNode(arrayNode.Name, null);
                foreach (var item in arrayNode.Items)
                {
                    newArray.AddItem(CloneNode(item));
                }
                return newArray;
            }
            if (node is ObjectNode objectNode)
            {
                var newObject = new ObjectNode(objectNode.Name, null);
                foreach (var child in objectNode.GetChildren())
                {
                    newObject.AddChild(child.Name, CloneNode(child));
                }
                return newObject;
            }
            throw new NotSupportedException($"Unsupported node type for cloning: {node.GetType().Name}");
        }
    }
}

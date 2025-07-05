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
        /// Merges a list of source files, starting with schema defaults, into a single result for UI display.
        /// </summary>
        /// <param name="allSourceFiles">The ordered list of source files to merge (lowest to highest priority).</param>
        /// <param name="schemaDefaultsRoot">The root node containing schema defaults, acting as Layer -1.</param>
        /// <returns>A DisplayMergeResult containing the merged tree and origin maps.</returns>
        public DisplayMergeResult MergeForDisplay(IReadOnlyList<CascadeLayer> allLayers, ObjectNode schemaDefaultsRoot)
        {
            var valueOrigins = new Dictionary<string, int>();
            var overrideSources = new Dictionary<string, List<int>>();

            // 1. Start with a clone of the schema defaults as our base
            var mergedRoot = (ObjectNode)DomCloning.CloneNode(schemaDefaultsRoot, null);
            // 2. Initialize origin maps with Layer -1 (Schema Defaults)
            TrackOriginsRecursive(mergedRoot, -1, valueOrigins, overrideSources);

            // 3. The core logic now iterates over the pre-processed layers
            foreach (var layer in allLayers)
            {
                // Merge the layer's already-structured root node into the final merged tree
                MergeNodeIntoRecursive(mergedRoot, layer.LayerConfigRootNode, layer.LayerIndex, valueOrigins, overrideSources);
            }

            return new DisplayMergeResult(mergedRoot, valueOrigins, overrideSources);
        }


        /// <summary>
        /// Recursively merges a source node's tree into the target merged tree.
        /// </summary>
        private void MergeNodeIntoRecursive(ObjectNode targetParent, ObjectNode sourceParent, int layerIndex, Dictionary<string, int> valueOrigins, Dictionary<string, List<int>> overrideSources)
        {
            foreach (var (childKey, sourceChild) in sourceParent.Children)
            {
                if (targetParent.Children.TryGetValue(childKey, out var existingChild))
                {
                    // A node with this key already exists.
                    if (existingChild is ObjectNode existingObject && sourceChild is ObjectNode sourceObject)
                    {
                        // Both are objects, so recurse into them.
                        MergeNodeIntoRecursive(existingObject, sourceObject, layerIndex, valueOrigins, overrideSources);
                    }
                    else
                    {
                        // Conflict or replacement. The source node wins and replaces the target.
                        var clonedSourceChild = DomCloning.CloneNode(sourceChild, targetParent);
                        targetParent.ReplaceChild(clonedSourceChild.Name, clonedSourceChild);
                        TrackOriginsRecursive(clonedSourceChild, layerIndex, valueOrigins, overrideSources);
                    }
                }
                else
                {
                    // Node doesn't exist in the target, so we add it.
                    var clonedSourceChild = DomCloning.CloneNode(sourceChild, targetParent);
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
    }
}

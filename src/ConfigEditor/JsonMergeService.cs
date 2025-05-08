using System;
using System.Collections.Generic;
using System.Linq;

namespace ConfigDom
{

    /// <summary>
    /// Provides services for merging JSON DOM trees, with support for cascading layers.
    /// </summary>
    public static class JsonMergeService
    {
        /// <summary>
        /// Merges multiple cascade layers into a single DOM tree.
        /// Later layers override earlier ones.
        /// </summary>
        public static ObjectNode MergeCascade(List<CascadeLayer> layers, MergeOriginTracker tracker)
        {
            if (layers.Count == 0)
                throw new ArgumentException("At least one layer is required", nameof(layers));

            // Start with the first layer's root
            var result = layers[0].Files[0].DomRoot as ObjectNode 
                ?? throw new InvalidOperationException("First layer must have an object root");

            // Track origins for the first layer
            foreach (var file in layers[0].Files)
            {
                TrackOriginsRecursive(file.DomRoot, file, 0, tracker);
            }

            // Merge subsequent layers
            for (int i = 1; i < layers.Count; i++)
            {
                foreach (var file in layers[i].Files)
                {
                    if (file.DomRoot is ObjectNode sourceRoot)
                    {
                        MergeObjectInto(result, sourceRoot, file, i, tracker);
                    }
                }
            }

            return result;
        }

        private static void MergeObjectInto(ObjectNode target, ObjectNode source, Json5SourceFile sourceFile, int layerIndex, MergeOriginTracker tracker)
        {
            foreach (var kvp in source.Children)
            {
                var key = kvp.Key;
                var sourceNode = kvp.Value;

                if (target.Children.TryGetValue(key, out var existingNode))
                {
                    if (sourceNode is ObjectNode sourceObj && existingNode is ObjectNode targetObj)
                    {
                        // Recursively merge objects
                        MergeObjectInto(targetObj, sourceObj, sourceFile, layerIndex, tracker);
                    }
                    else
                    {
                        // Replace non-object nodes
                        target.Children[key] = sourceNode;
                        TrackOriginsRecursive(sourceNode, sourceFile, layerIndex, tracker);
                    }
                }
                else
                {
                    // Add new nodes
                    target.Children[key] = sourceNode;
                    TrackOriginsRecursive(sourceNode, sourceFile, layerIndex, tracker);
                }
            }
        }

        private static void TrackOriginsRecursive(DomNode node, Json5SourceFile file, int layerIndex, MergeOriginTracker tracker)
        {
            tracker.TrackOrigin(node.GetAbsolutePath(), file, layerIndex);

            if (node is ObjectNode obj)
            {
                foreach (var child in obj.Children.Values)
                {
                    TrackOriginsRecursive(child, file, layerIndex, tracker);
                }
            }
            else if (node is ArrayNode arr)
            {
                foreach (var item in arr.Items)
                {
                    TrackOriginsRecursive(item, file, layerIndex, tracker);
                }
            }
        }
    }
}


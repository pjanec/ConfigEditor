using JsonConfigEditor.Core.Cascade;
using JsonConfigEditor.Core.Dom;
using System.Collections.Generic;
using System.Linq;

namespace JsonConfigEditor.Core.Services
{
    public record DisplayMergeResult(
        ObjectNode MergedRoot,
        Dictionary<string, int> ValueOrigins,
        Dictionary<string, List<int>> OverrideSources
    );

    public class CascadedDomDisplayMerger
    {
        public DisplayMergeResult MergeForDisplay(IReadOnlyList<CascadeLayer> allLayers, ObjectNode schemaDefaultsRoot)
        {
            var valueOrigins = new Dictionary<string, int>();
            var overrideSources = new Dictionary<string, List<int>>();
            
            // Pass 1: Build the complete overrideSources map by iterating through each layer's original data.
            // This ensures we know every layer that defines a given path.
            foreach (var layer in allLayers)
            {
                var pathsInThisLayer = new List<string>();
                GetAllPaths(layer.LayerConfigRootNode, pathsInThisLayer);
                foreach (var path in pathsInThisLayer)
                {
                    if (!overrideSources.TryGetValue(path, out var sources))
                    {
                        sources = new List<int>();
                        overrideSources[path] = sources;
                    }
                    sources.Add(layer.LayerIndex);
                }
            }

            // Pass 2: Build the final merged tree by combining the layers.
            var mergedRoot = (ObjectNode)DomCloning.CloneNode(schemaDefaultsRoot, null);
            foreach (var layer in allLayers)
            {
                MergeNodeIntoRecursive(mergedRoot, layer.LayerConfigRootNode);
            }
            
            // Pass 3: Now that the override list is complete, determine the final "winning" layer for each path.
            foreach (var path in overrideSources.Keys)
            {
                // The winner is the one from the list with the highest index.
                valueOrigins[path] = overrideSources[path].Max();
            }

            return new DisplayMergeResult(mergedRoot, valueOrigins, overrideSources);
        }

        private void GetAllPaths(DomNode node, List<string> paths)
        {
            paths.Add(node.Path);
            if (node is ObjectNode o)
            {
                foreach (var child in o.GetChildren()) GetAllPaths(child, paths);
            }
            else if (node is ArrayNode a)
            {
                foreach (var item in a.GetItems()) GetAllPaths(item, paths);
            }
        }
        
        private void MergeNodeIntoRecursive(ObjectNode targetParent, ObjectNode sourceParent)
        {
            foreach (var (childKey, sourceChild) in sourceParent.Children)
            {
                if (targetParent.Children.TryGetValue(childKey, out var existingChild))
                {
                    if (existingChild is ObjectNode existingObject && sourceChild is ObjectNode sourceObject)
                    {
                        MergeNodeIntoRecursive(existingObject, sourceObject);
                    }
                    else
                    {
                        var clonedSourceChild = DomCloning.CloneNode(sourceChild, targetParent);
                        targetParent.ReplaceChild(clonedSourceChild.Name, clonedSourceChild);
                    }
                }
                else
                {
                    var clonedSourceChild = DomCloning.CloneNode(sourceChild, targetParent);
                    targetParent.AddChild(clonedSourceChild.Name, clonedSourceChild);
                }
            }
        }
    }
}

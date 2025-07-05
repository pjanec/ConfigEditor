using JsonConfigEditor.Core.Cascade;
using JsonConfigEditor.Core.Dom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// Represents the result of an intra-layer merge, including the final merged tree,
    /// its origin map, and any errors that were detected during the process.
    /// </summary>
    public record IntraLayerMergeResult(
        ObjectNode LayerConfigRootNode,
        Dictionary<string, string> IntraLayerValueOrigins,
        List<string> Errors
    );

    /// <summary>
    /// Merges all source files within a single layer into a unified DOM tree.
    /// It also builds the origin map and detects overlapping definition errors.
    /// </summary>
    public class IntraLayerMerger
    {
        /// <summary>
        /// Merges the source files from a LayerLoadResult into a single unified result.
        /// </summary>
        /// <param name="layerLoadResult">The raw loaded data for the layer.</param>
        /// <returns>An IntraLayerMergeResult containing the merged tree, origin map, and errors.</returns>
        public IntraLayerMergeResult Merge(LayerLoadResult layerLoadResult)
        {
            var rootNode = new ObjectNode("$root", null);
            var origins = new Dictionary<string, string>();
            var errors = new List<string>();

            foreach (var sourceFile in layerLoadResult.SourceFiles.OrderBy(f => f.RelativePath))
            {
                // --- MODIFICATION START ---
                // Enforce that the root of the source file must be an object.
                if (sourceFile.DomRoot is not ObjectNode sourceFileRoot)
                {
                    // If the file content is not a JSON object (e.g., it's an array `[]` or a value `"hello"`),
                    // it violates our structural rule. Log an error and skip this file.
                    errors.Add($"Source file '{sourceFile.RelativePath}' does not have a root JSON object and will be ignored.");
                    continue; // Skip to the next file
                }
                // --- MODIFICATION END ---

                var targetNode = EnsurePathAndGetTarget(rootNode, sourceFile.RelativePath);
                MergeNodeRecursive(targetNode, sourceFileRoot, sourceFile, origins, errors);
            }

            return new IntraLayerMergeResult(rootNode, origins, errors);
        }

        /// <summary>
        /// Creates the nested ObjectNode structure that mirrors the file path.
        /// </summary>
        /// <param name="layerRoot">The root node of the layer.</param>
        /// <param name="relativePath">The relative path of the file.</param>
        /// <returns>The target node where the file content should be merged.</returns>
        private ObjectNode EnsurePathAndGetTarget(ObjectNode layerRoot, string relativePath)
        {
            // Remove the .json extension and split the path by directory separators
            var pathWithoutExtension = relativePath.Replace(".json", "", StringComparison.OrdinalIgnoreCase);
            var segments = pathWithoutExtension.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            ObjectNode currentParent = layerRoot;

            // Walk the path, creating parent nodes as needed
            foreach (var segment in segments)
            {
                var childNode = currentParent.GetChild(segment);
                if (childNode is ObjectNode existingObject)
                {
                    // If a node for this path segment already exists, use it
                    currentParent = existingObject;
                }
                else
                {
                    // If it doesn't exist, create it and add it to the tree
                    var newNode = new ObjectNode(segment, currentParent);
                    currentParent.AddChild(segment, newNode);
                    currentParent = newNode;
                }
            }
            return currentParent;
        }

        /// <summary>
        /// Recursively merges a source node into a target node.
        /// </summary>
        /// <param name="targetParent">The parent node in the unified tree we are merging into.</param>
        /// <param name="sourceNode">The node from the source file being merged.</param>
        /// <param name="sourceFile">The source file providing the data.</param>
        /// <param name="origins">The origin map to populate.</param>
        /// <param name="errors">The list to which any errors will be added.</param>
        private void MergeNodeRecursive(ObjectNode targetParent, ObjectNode sourceNode, SourceFileInfo sourceFile, Dictionary<string, string> origins, List<string> errors)
        {
            // Go through each property (child) in the source file's node
            foreach (var (childKey, childNode) in sourceNode.Children)
            {
                if (targetParent.Children.TryGetValue(childKey, out var existingNode))
                {
                    // A node with this key already exists. Check for conflicts.
                    if (existingNode is ObjectNode existingObject && childNode is ObjectNode childObject)
                    {
                        // Both are objects, so we can safely recurse and merge their children.
                        MergeNodeRecursive(existingObject, childObject, sourceFile, origins, errors);
                    }
                    else
                    {
                        // CONFLICT! The property is defined in two places, and at least one is not an object.
                        // This violates the "no overlaps" rule for a layer.
                        var originalSourcePath = origins.GetValueOrDefault(existingNode.Path, "unknown file");
                        errors.Add($"Overlap detected for property '{existingNode.Path}'. It is defined in both '{originalSourcePath}' and '{sourceFile.RelativePath}'.");
                    }
                }
                else
                {
                    // No conflict. This is a new property for the unified tree.
                    // The logic here is correct, but we ensure the key is passed to AddChild
                    var clonedNode = DomCloning.CloneNode(childNode, targetParent);
                    targetParent.AddChild(clonedNode.Name, clonedNode); // This is correct
                    TrackOriginsRecursive(clonedNode, sourceFile.RelativePath, origins);
                }
            }
        }
        
        /// <summary>
        /// Recursively populates the origin map for a newly added node and all of its children.
        /// </summary>
        private void TrackOriginsRecursive(DomNode node, string relativeSourcePath, Dictionary<string, string> origins)
        {
            // Map this node's path to its source file.
            origins[node.Path] = relativeSourcePath;

            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    TrackOriginsRecursive(child, relativeSourcePath, origins);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.Items)
                {
                    TrackOriginsRecursive(item, relativeSourcePath, origins);
                }
            }
        }


    }
}
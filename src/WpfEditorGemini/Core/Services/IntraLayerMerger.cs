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
            var rootNode = new ObjectNode("$root", null); // The unified tree for the entire layer
            var origins = new Dictionary<string, string>(); // The origin map for the layer
            var errors = new List<string>();

            // Iterate through each file parsed from the layer's folder
            foreach (var sourceFile in layerLoadResult.SourceFiles)
            {
                if (sourceFile.DomRoot is ObjectNode sourceFileRoot)
                {
                    // Recursively merge the file's content into the layer's root node
                    MergeNodeRecursive(rootNode, sourceFileRoot, sourceFile, origins, errors);
                }
                else
                {
                    errors.Add($"Source file '{sourceFile.RelativePath}' does not have a root JSON object and will be ignored.");
                }
            }

            return new IntraLayerMergeResult(rootNode, origins, errors);
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
                    // We must clone the node to avoid shared instances and ensure it gets a new parent.
                    var clonedNode = CloneNode(childNode);
                    targetParent.AddChild(clonedNode.Name, clonedNode); // AddChild now correctly sets parent and updates paths

                    // Now that the node is part of the tree and has a correct path, track its origin.
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

        /// <summary>
        /// Performs a deep clone of a DomNode.
        /// This is essential to ensure that nodes added to the merged tree are new instances,
        /// preventing issues with multiple parents or shared state.
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
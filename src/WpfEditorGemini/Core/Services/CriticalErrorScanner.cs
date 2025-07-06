using JsonConfigEditor.Core.Cascade;
using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.Core.Validation;
using System.Collections.Generic;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// Scans for critical errors in loaded cascade layers, particularly "True Overlaps"
    /// where the same property is defined in multiple files within the same layer.
    /// </summary>
    public class CriticalErrorScanner
    {
        /// <summary>
        /// Scans all loaded layers for critical errors.
        /// </summary>
        /// <param name="loadedLayers">The list of loaded layer results</param>
        /// <returns>A list of integrity issues found</returns>
        public List<IntegrityIssue> Scan(List<LayerLoadResult> loadedLayers)
        {
            var issues = new List<IntegrityIssue>();

            foreach (var layerData in loadedLayers)
            {
                var leafNodeOrigins = new Dictionary<string, string>(); // Key: DOM Path, Value: Relative File Path

                foreach (var sourceFile in layerData.SourceFiles)
                {
                    TraverseForLeafNodes(sourceFile.DomRoot, sourceFile.RelativePath, leafNodeOrigins, issues, layerData.Definition.Name);
                }
            }
            return issues;
        }

        /// <summary>
        /// Traverses the DOM tree to find leaf nodes and detect overlaps.
        /// </summary>
        private void TraverseForLeafNodes(DomNode node, string filePath, Dictionary<string, string> origins, List<IntegrityIssue> issues, string layerName)
        {
            // A "leaf" is a ValueNode or an ArrayNode.
            if (node is ValueNode || node is ArrayNode)
            {
                if (origins.TryGetValue(node.Path, out var originalFile))
                {
                    // A true overlap has been found! Create an IntegrityIssue which contains all necessary info for the UI.
                    var issue = new IntegrityIssue(
                        ValidationSeverity.Error,
                        $"Property '{node.Path}' is defined in both '{originalFile}' and '{filePath}'.",
                        layerName,
                        node.Path,
                        filePath
                    );
                    issues.Add(issue);
                }
                else
                {
                    origins[node.Path] = filePath;
                }
            }

            // Recurse into children of ObjectNodes.
            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    TraverseForLeafNodes(child, filePath, origins, issues, layerName);
                }
            }
        }
    }
}

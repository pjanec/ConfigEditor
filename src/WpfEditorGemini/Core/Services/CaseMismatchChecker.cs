using JsonConfigEditor.Core.Cascade;
using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.Core.Schema;
using JsonConfigEditor.Core.SchemaLoading;
using JsonConfigEditor.Core.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JsonConfigEditor.Core.Services
{
    public class CaseMismatchChecker
    {
        /// <summary>
        /// Scans all layers for various types of case mismatches.
        /// </summary>
        public List<IntegrityIssue> Scan(
            IReadOnlyList<CascadeLayer> layers,
            ISchemaLoaderService schemaLoader)
        {
            var issues = new List<IntegrityIssue>();
            var canonicalPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var layer in layers)
            {
                // Traverse the layer's DOM to perform all checks simultaneously
                TraverseAndCheckNode(
                    layer.LayerConfigRootNode,
                    layer,
                    schemaLoader,
                    canonicalPaths,
                    issues);
            }

            return issues;
        }

        /// <summary>
        /// Recursive worker method to perform all checks on a node and its descendants.
        /// </summary>
        private void TraverseAndCheckNode(
            DomNode node,
            CascadeLayer layer,
            ISchemaLoaderService schemaLoader,
            Dictionary<string, string> canonicalPaths,
            List<IntegrityIssue> issues)
        {
            // Check 1: JSON Key vs. Schema Casing
            var schema = schemaLoader.FindSchemaForPath(node.Path);
    
            if (schema != null && node.Parent != null && !string.Equals(node.Name, schema.Name, StringComparison.Ordinal))
            {
                issues.Add(new IntegrityIssue(
                    ValidationSeverity.Warning,
                    $"Property '{node.Path}' is cased as '{node.Name}' in JSON but as '{schema.Name}' in the schema.",
                    layer.Name,
                    node.Path,
                    layer.IntraLayerValueOrigins.GetValueOrDefault(node.Path)
                ));
            }

            // Check 2: Cross-Layer Key Casing
            if (canonicalPaths.TryGetValue(node.Path, out var canonicalPath))
            {
                if (!string.Equals(node.Path, canonicalPath, StringComparison.Ordinal))
                {
                    issues.Add(new IntegrityIssue(
                        ValidationSeverity.Warning,
                        $"Property '{node.Path}' has inconsistent casing. Canonical casing is '{canonicalPath}'.",
                        layer.Name,
                        node.Path,
                        layer.IntraLayerValueOrigins.GetValueOrDefault(node.Path)
                    ));
                }
            }
            else
            {
                // First time we've seen this path, establish its casing as canonical.
                canonicalPaths[node.Path] = node.Path;
            }

            // Recurse into children
            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    TraverseAndCheckNode(child, layer, schemaLoader, canonicalPaths, issues);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    TraverseAndCheckNode(item, layer, schemaLoader, canonicalPaths, issues);
                }
            }
        }
    }
} 
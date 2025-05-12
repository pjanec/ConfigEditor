using ConfigEditor.Dom;
using ConfigEditor.EditCtx;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConfigEditor.Services
{
    public static class DomSearchService
    {
        /// <summary>
        /// Recursively searches the merged DOM tree up to the specified layer for nodes whose
        /// final path segment or value contains the query.
        /// </summary>
        public static IEnumerable<(string Path, string Value)> Search(CascadeEditorContext context, string query, int inclusiveLayerIndex)
        {
            var mergedRoot = context.GetMergedDomUpToLayer(inclusiveLayerIndex);
            return SearchNodeRecursive(mergedRoot, query);
        }

        public static IEnumerable<(string Path, string Value)> SearchNodeRecursive(DomNode node, string query)
        {
            var results = new List<(string Path, string Value)>();

            string pathSegment = node.Name ?? "";
            string nodePath = node.GetAbsolutePath();

            if (pathSegment.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add((nodePath, "[Path Match]"));
            }

            switch (node)
            {
                case ValueNode valueNode:
                    if (valueNode.Value.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
                        results.Add((nodePath, valueNode.Value.ToString()));
                    break;

                case ObjectNode objNode:
                    foreach (var child in objNode.Children.Values)
                        results.AddRange(SearchNodeRecursive(child, query));
                    break;

                case ArrayNode arrNode:
                    foreach (var item in arrNode.Items)
                        results.AddRange(SearchNodeRecursive(item, query));
                    break;

                case RefNode refNode:
                    var refJson = refNode.ExportJson().ToString();
                    if (refJson.Contains(query, StringComparison.OrdinalIgnoreCase))
                        results.Add((nodePath, refJson));
                    break;
            }

            return results;
        }
    }
}

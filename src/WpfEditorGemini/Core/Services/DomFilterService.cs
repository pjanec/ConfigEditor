using RuntimeConfig.Core.Dom;
using System.Collections.Generic;
using System.Linq;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// Implements the "Search and Reveal" filter functionality.
    /// Its purpose is to take a root DomNode and a filter query, and return the
    /// precise set of node paths that should be visible in the UI.
    /// </summary>
    public class DomFilterService
    {
        /// <summary>
        /// Gets the set of all node paths that should be visible based on a filter text.
        /// This includes the nodes that directly match the filter and all of their ancestors.
        /// </summary>
        /// <param name="rootNode">The root of the DOM tree to search.</param>
        /// <param name="filterText">The user's filter query.</param>
        /// <returns>A HashSet containing the unique paths of all nodes to be displayed.</returns>
        public HashSet<string> GetVisibleNodePaths(DomNode rootNode, string filterText)
        {
            if (string.IsNullOrEmpty(filterText))
            {
                // If there's no filter, return an empty set, indicating all nodes should be visible.
                // The MainViewModel will interpret an empty set as "no filter applied".
                return new HashSet<string>();
            }

            // Pass 1: Find all nodes that are a direct match for the filter.
            var directMatches = new List<DomNode>();
            FindMatchingNodesRecursive(rootNode, filterText.ToLowerInvariant(), directMatches);

            // Pass 2: Collect all ancestors of the matched nodes to build the final visible set.
            var visibleNodePaths = new HashSet<string>();
            foreach (var match in directMatches)
            {
                var currentNode = match;
                // Walk up the parent chain from the match to the root.
                while (currentNode != null)
                {
                    visibleNodePaths.Add(currentNode.Path);
                    currentNode = currentNode.Parent;
                }
            }

            return visibleNodePaths;
        }

        /// <summary>
        /// A recursive, depth-first traversal to find all nodes that match the filter text.
        /// </summary>
        /// <param name="node">The current node to inspect.</param>
        /// <param name="lowerCaseFilter">The filter text, pre-converted to lowercase for efficiency.</param>
        /// <param name="matchingNodes">The list to add matching nodes to.</param>
        private void FindMatchingNodesRecursive(DomNode node, string lowerCaseFilter, List<DomNode> matchingNodes)
        {
            // Check if the node's name or value contains the filter text.
            bool isMatch = node.Name.ToLowerInvariant().Contains(lowerCaseFilter);

            if (!isMatch && node is ValueNode valueNode)
            {
                isMatch = valueNode.Value.ToString().ToLowerInvariant().Contains(lowerCaseFilter);
            }

            if (isMatch)
            {
                matchingNodes.Add(node);
            }

            // Recurse into children.
            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    FindMatchingNodesRecursive(child, lowerCaseFilter, matchingNodes);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    FindMatchingNodesRecursive(item, lowerCaseFilter, matchingNodes);
                }
            }
        }
    }
} 
using JsonConfigEditor.Core.Dom;
using System.Collections.Generic;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// A stateless service that finds all nodes within a DOM tree that match a search query.
    /// It is used by the MainViewModel to get a list of results for both standard search
    /// and the "Search All Layers" feature.
    /// </summary>
    public class DomSearchService
    {
        /// <summary>
        /// Finds all nodes within a given root that match the search text.
        /// The search is case-insensitive and checks both node names and values.
        /// </summary>
        /// <param name="rootNode">The root of the DOM tree to search.</param>
        /// <param name="searchText">The user's search query.</param>
        /// <returns>An ordered list of matching DomNode instances.</returns>
        public List<DomNode> FindAllMatches(DomNode rootNode, string searchText)
        {
            var matches = new List<DomNode>();
            if (string.IsNullOrEmpty(searchText))
            {
                return matches;
            }

            SearchDomNodeRecursive(rootNode, searchText.ToLowerInvariant(), matches);
            return matches;
        }

        /// <summary>
        /// A recursive, depth-first traversal to find all nodes that match the search text.
        /// </summary>
        /// <param name="node">The current node to inspect.</param>
        /// <param name="lowerSearchText">The search text, pre-converted to lowercase.</param>
        /// <param name="matches">The list to add matching nodes to.</param>
        private void SearchDomNodeRecursive(DomNode node, string lowerSearchText, List<DomNode> matches)
        {
            // Check if the node's name or value contains the search text.
            if (node.Name.ToLowerInvariant().Contains(lowerSearchText))
            {
                matches.Add(node);
            }
            else if (node is ValueNode valueNode)
            {
                if (valueNode.Value.ToString().ToLowerInvariant().Contains(lowerSearchText))
                {
                    matches.Add(node);
                }
            }

            // Recurse into children.
            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    SearchDomNodeRecursive(child, lowerSearchText, matches);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    SearchDomNodeRecursive(item, lowerSearchText, matches);
                }
            }
        }
    }
} 
using System;
using System.Collections.Generic;

namespace ConfigDom
{
    /// <summary>
    /// Resolves RefNode targets based on their RefPath.
    /// Supports cycle detection and recursive reference resolution.
    /// </summary>
    public static class RefNodeResolver
    {
        /// <summary>
        /// Resolves the final target of a RefNode, following chained references if needed.
        /// </summary>
        /// <param name="refNode">The RefNode to resolve.</param>
        /// <param name="domRoot">The root of the DOM tree to resolve against.</param>
        /// <returns>The resolved target node, or null if invalid or cyclic.</returns>
        public static DomNode? Resolve(RefNode refNode, DomNode domRoot)
        {
            return ResolveRecursive(refNode, domRoot, new HashSet<string>());
        }

        private static DomNode? ResolveRecursive(RefNode refNode, DomNode domRoot, HashSet<string> visitedPaths)
        {
            var path = refNode.RefPath;
            if (visitedPaths.Contains(path)) return null;
            visitedPaths.Add(path);

            var target = DomTreePathHelper.FindNodeAtPath(domRoot, path);
            if (target is RefNode nestedRef)
                return ResolveRecursive(nestedRef, domRoot, visitedPaths);

            return target;
        }
    }
}

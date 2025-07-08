using System;
using System.Collections.Generic;

namespace RuntimeConfig.Core.Dom
{
    public class RefResolver
    {
        private readonly ObjectNode _rawRoot;

        public RefResolver(ObjectNode rawRoot)
        {
            _rawRoot = rawRoot;
        }

        public ObjectNode Resolve()
        {
            var resolvedClone = (ObjectNode)DomTree.CloneNode(_rawRoot, null);
            ResolveNodeRecursive(resolvedClone, new HashSet<string>());
            return resolvedClone;
        }

        private void ResolveNodeRecursive(DomNode node, HashSet<string> visitedPaths)
        {
            if (node is ObjectNode obj)
            {
                var childrenCopy = new Dictionary<string, DomNode>(obj.Children);
                foreach (var (key, child) in childrenCopy)
                {
                    if (child is RefNode refNode)
                    {
                        var resolved = ResolveSingleRef(refNode, visitedPaths);
                        obj.ReplaceChild(key, DomTree.CloneNode(resolved, obj));
                    }
                    else
                    {
                        ResolveNodeRecursive(child, visitedPaths);
                    }
                }
            }
            else if (node is ArrayNode arr)
            {
                for (int i = 0; i < arr.Items.Count; i++)
                {
                    var item = arr.Items[i];
                    if (item is RefNode refNode)
                    {
                        var resolved = ResolveSingleRef(refNode, visitedPaths);
                        arr.ReplaceItem(i, DomTree.CloneNode(resolved, arr));
                    }
                    else
                    {
                        ResolveNodeRecursive(item, visitedPaths);
                    }
                }
            }
        }

        private DomNode ResolveSingleRef(RefNode refNode, HashSet<string> visitedPaths)
        {
            if (!visitedPaths.Add(refNode.Path))
            {
                throw new InvalidOperationException($"Cyclic reference detected at path: {refNode.Path}");
            }

            var targetNode = DomTree.FindNodeByPath(_rawRoot, refNode.ReferencePath);
            if (targetNode == null)
            {
                throw new KeyNotFoundException($"Reference path '{refNode.ReferencePath}' not found in the DOM tree.");
            }

            // If the target is itself a ref, resolve it recursively.
            if (targetNode is RefNode nestedRef)
            {
                targetNode = ResolveSingleRef(nestedRef, visitedPaths);
            }

            visitedPaths.Remove(refNode.Path);
            return targetNode;
        }
    }
} 
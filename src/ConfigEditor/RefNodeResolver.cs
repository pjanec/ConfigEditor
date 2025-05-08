using System.Collections.Generic;
using System.Linq;
using System;

namespace ConfigDom;

public static class RefNodeResolver
{
    /// <summary>
    /// Resolves and replaces all $ref nodes within the DOM tree in-place.
    /// Throws on unresolved or cyclic references.
    /// </summary>
    public static void ResolveAllInPlace(DomNode root)
    {
        ResolveRecursive(root, root, new HashSet<DomNode>());
    }

    private static void ResolveRecursive(DomNode current, DomNode root, HashSet<DomNode> visited)
    {
        if (visited.Contains(current))
            throw new InvalidOperationException("Reference cycle detected");

        visited.Add(current);

        if (current is ObjectNode obj)
        {
            var keys = obj.Children.Keys.ToList();
            foreach (var key in keys)
            {
                var child = obj.Children[key];
                if (child is RefNode refNode)
                {
                    var resolved = Resolve(refNode, root);
                    obj.Children[key] = DeepCopy(resolved);
                }
                else
                {
                    ResolveRecursive(child, root, visited);
                }
            }
        }
        else if (current is ArrayNode arr)
        {
            for (int i = 0; i < arr.Items.Count; i++)
            {
                if (arr.Items[i] is RefNode refNode)
                {
                    var resolved = Resolve(refNode, root);
                    arr.Items[i] = DeepCopy(resolved);
                }
                else
                {
                    ResolveRecursive(arr.Items[i], root, visited);
                }
            }
        }

        visited.Remove(current);
    }

    /// <summary>
    /// Resolves a single $ref node against the given root.
    /// </summary>
    public static DomNode Resolve(RefNode refNode, DomNode root)
    {
        var parts = refNode.RefPath.Split("/", StringSplitOptions.RemoveEmptyEntries);
        DomNode? node = root;
        foreach (var part in parts)
        {
            if (node is ObjectNode obj && obj.Children.TryGetValue(part, out var next))
                node = next;
            else if (node is ArrayNode arr && int.TryParse(part, out var index) && index >= 0 && index < arr.Items.Count)
                node = arr.Items[index];
            else
                throw new InvalidOperationException($"Invalid $ref path: {refNode.RefPath}");
        }
        return node;
    }

    private static DomNode DeepCopy(DomNode node)
    {
        var json = node.ExportJson();
        return JsonDomBuilder.BuildFromJsonElement(node.Name ?? "", json, node.Parent);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace ConfigDom;

/// <summary>
/// Provides functionality for merging DOM trees from different layers.
/// </summary>
public static class DomMerger
{
    /// <summary>
    /// Merges multiple DOM trees in order (lower priority to higher).
    /// </summary>
    public static DomNode Merge(IEnumerable<DomNode> layeredRoots)
    {
        var roots = layeredRoots.ToList();
        if (roots.Count == 0)
            throw new ArgumentException("At least one root node is required", nameof(layeredRoots));

        // Start with a clone of the first (lowest priority) root
        var result = roots[0].Clone();

        // Merge subsequent layers
        for (int i = 1; i < roots.Count; i++)
        {
            if (result is ObjectNode resultObj && roots[i] is ObjectNode sourceObj)
            {
                MergeObjectInto(resultObj, sourceObj);
            }
            else
            {
                // If root types don't match, replace with higher priority
                result = roots[i].Clone();
            }
        }

        return result;
    }

    private static void MergeObjectInto(ObjectNode target, ObjectNode source)
    {
        foreach (var (key, sourceNode) in source.Children)
        {
            if (target.Children.TryGetValue(key, out var existingNode))
            {
                if (sourceNode is ObjectNode sourceObj && existingNode is ObjectNode targetObj)
                {
                    // Recursively merge objects
                    MergeObjectInto(targetObj, sourceObj);
                }
                else
                {
                    // Replace non-object nodes
                    target.Children[key] = sourceNode.Clone();
                    target.Children[key].SetParent(target);
                }
            }
            else
            {
                // Add new nodes
                target.Children[key] = sourceNode.Clone();
                target.Children[key].SetParent(target);
            }
        }
    }
} 
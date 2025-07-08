using System;

namespace RuntimeConfig.Core.Dom
{
    /// <summary>
    /// Provides utility methods for merging DOM trees.
    /// </summary>
    public static class DomMerger
    {
        /// <summary>
        /// Merges the source object node into the target object node recursively.
        /// </summary>
        /// <param name="target">The target object node to merge into.</param>
        /// <param name="source">The source object node to merge from.</param>
        public static void MergeInto(ObjectNode target, ObjectNode source)
        {
            foreach (var (key, sourceChild) in source.Children)
            {
                if (target.HasProperty(key))
                {
                    var targetChild = target.GetChild(key);
                    if (targetChild is ObjectNode targetObject && sourceChild is ObjectNode sourceObject)
                    {
                        // Recursively merge object nodes
                        MergeInto(targetObject, sourceObject);
                    }
                    else
                    {
                        // Replace non-object nodes
                        target.ReplaceChild(key, DomTree.CloneNode(sourceChild, target));
                    }
                }
                else
                {
                    // Add new property
                    target.AddChild(key, DomTree.CloneNode(sourceChild, target));
                }
            }
        }
    }
} 
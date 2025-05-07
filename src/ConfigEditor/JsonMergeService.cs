using System;
using System.Collections.Generic;
using System.Linq;

namespace ConfigDom
{
    /// <summary>
    /// Provides cascade-level-aware merging of ObjectNode trees.
    /// </summary>
    public static class JsonMergeService
    {
        /// <summary>
        /// Deep merges multiple ObjectNode roots from lowest to highest priority.
        /// Fields from later nodes override earlier ones.
        /// </summary>
        public static ObjectNode MergeCascade(List<ObjectNode> roots)
        {
            var result = new ObjectNode("root");

            foreach (var source in roots)
            {
                MergeObjectInto(result, source);
            }

            return result;
        }

        /// <summary>
        /// Overload: extracts roots from source files before merging.
        /// </summary>
        public static ObjectNode MergeCascade(List<Json5SourceFile> sources)
        {
            var roots = sources.Select(f => f.DomRoot).OfType<ObjectNode>().ToList();
            return MergeCascade(roots);
        }

        private static void MergeObjectInto(ObjectNode target, ObjectNode source)
        {
            foreach (var (key, value) in source.Children)
            {
                if (target.Children.TryGetValue(key, out var existing))
                {
                    if (existing is ObjectNode existingObj && value is ObjectNode sourceObj)
                    {
                        MergeObjectInto(existingObj, sourceObj);
                    }
                    else
                    {
                        target.AddChild(value); // Override
                    }
                }
                else
                {
                    target.AddChild(value);
                }
            }
        }
    }
}

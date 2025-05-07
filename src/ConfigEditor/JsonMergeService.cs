using System;
using System.Collections.Generic;
using System.Linq;

namespace ConfigDom
{
    /// <summary>
    /// Tracks the origin of each node in the merged DOM tree.
    /// Records which source file and cascade level provided each value.
    /// </summary>
    public class MergeOriginTracker
    {
        private readonly Dictionary<string, (Json5SourceFile? file, int level)> _originMap = new();

        public void TrackOrigin(string path, Json5SourceFile? file, int level)
        {
            _originMap[path] = (file, level);
        }

        public (Json5SourceFile? file, int level)? GetOrigin(string path)
        {
            return _originMap.TryGetValue(path, out var origin) ? origin : null;
        }

        public string GetLevelName(int level) => level switch
        {
            0 => "base",
            1 => "site",
            2 => "local",
            _ => $"level{level}"
        };
    }

    /// <summary>
    /// Provides cascade-level-aware merging of ObjectNode trees.
    /// Supports deep structural merging with proper array replacement and source tracking.
    /// </summary>
    public static class JsonMergeService
    {
        /// <summary>
        /// Deep merges multiple ObjectNode roots from lowest to highest priority.
        /// Fields from later nodes override earlier ones.
        /// </summary>
        public static ObjectNode MergeCascade(List<ObjectNode> roots, MergeOriginTracker? tracker = null)
        {
            var result = new ObjectNode("root");

            for (int level = 0; level < roots.Count; level++)
            {
                var source = roots[level];
                MergeObjectInto(result, source, level, tracker);
            }

            return result;
        }

        /// <summary>
        /// Overload: extracts roots from source files before merging.
        /// Tracks the origin of each value in the merged result.
        /// </summary>
        public static ObjectNode MergeCascade(List<Json5SourceFile> sources, MergeOriginTracker? tracker = null)
        {
            var roots = sources.Select(f => f.DomRoot).OfType<ObjectNode>().ToList();
            return MergeCascade(roots, tracker);
        }

        private static void MergeObjectInto(ObjectNode target, ObjectNode source, int level, MergeOriginTracker? tracker)
        {
            foreach (var (key, value) in source.Children)
            {
                var path = target.GetAbsolutePath() + "/" + key;
                
                if (target.Children.TryGetValue(key, out var existing))
                {
                    if (value is ObjectNode sourceObj && existing is ObjectNode targetObj)
                    {
                        // Recursive merge for objects
                        MergeObjectInto(targetObj, sourceObj, level, tracker);
                    }
                    else if (value is ArrayNode sourceArr && existing is ArrayNode targetArr)
                    {
                        // Replace entire array (not merge)
                        target.AddChild(value);
                        tracker?.TrackOrigin(path, source.File, level);
                    }
                    else
                    {
                        // Override leaf value or different types
                        target.AddChild(value);
                        tracker?.TrackOrigin(path, source.File, level);
                    }
                }
                else
                {
                    // New value
                    target.AddChild(value);
                    tracker?.TrackOrigin(path, source.File, level);
                }
            }
        }
    }
}

using RuntimeConfig.Core.Dom;
using System.Collections.Generic;

namespace RuntimeConfig.Core.Models
{
    /// <summary>
    /// Contains the complete, detailed result of processing a single configuration layer.
    /// </summary>
    public record LayerProcessingResult
    {
        public ObjectNode MergedRootNode { get; init; }
        public IReadOnlyList<SourceFileInfo> LoadedSourceFiles { get; init; }
        public IReadOnlyDictionary<string, string> ValueOrigins { get; init; }
        public IReadOnlyList<string> Errors { get; init; }
    }
} 
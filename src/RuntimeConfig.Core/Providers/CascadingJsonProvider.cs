using RuntimeConfig.Core.Dom;
using RuntimeConfig.Core.Models;
using RuntimeConfig.Core.Services;
using System.Collections.Generic;
using System.Linq;

namespace RuntimeConfig.Core.Providers
{
    public class CascadingJsonProvider : IRuntimeDomProvider
    {
        private readonly IReadOnlyList<LayerDefinition> _layers;
        private readonly LayerProcessor _layerProcessor;

        public CascadingJsonProvider(IReadOnlyList<LayerDefinition> layers)
        {
            _layers = layers;
            _layerProcessor = new LayerProcessor();
        }

        public ObjectNode Load()
        {
            var finalMergedRoot = new ObjectNode("$root", null);

            int layerIndex = 0;
            foreach (var layerDef in _layers)
            {
                var result = _layerProcessor.Process(layerDef.BasePath, layerIndex++);
                // We only care about the merged node, not the other details.
                DomMerger.MergeInto(finalMergedRoot, result.MergedRootNode);
            }

            return finalMergedRoot;
        }

        public void Refresh()
        {
            // For a file-based provider, this is a no-op.
            // A more advanced implementation could use a FileSystemWatcher here.
        }
    }
} 
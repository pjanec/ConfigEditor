using JsonConfigEditor.Core.Dom;
using System;
using System.Collections.Generic;

namespace JsonConfigEditor.Core.Cascade
{
    /// <summary>
    /// Represents a single, processed layer in the configuration cascade.
    /// It holds the layer's identity, all of its source files, a single unified DOM tree
    /// representing the merger of those files, and the origin map for its data.
    /// </summary>
    public class CascadeLayer
    {
        /// <summary>
        /// Gets the zero-based index of this layer in the cascade (0 = lowest priority).
        /// </summary>
        public int LayerIndex { get; }

        /// <summary>
        /// Gets the user-friendly name of the layer (e.g., "Base", "Production").
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the absolute path to the folder containing this layer's source files.
        /// </summary>
        public string FolderPath { get; }

        /// <summary>
        /// Gets a list of all source files that were loaded from this layer's folder.
        /// This is the raw, un-merged data for the layer.
        /// </summary>
        public IReadOnlyList<SourceFileInfo> SourceFiles { get; }

        /// <summary>
        /// Gets the single, unified root DomNode for this layer. This property
        /// will be removed in a later refactoring stage.
        /// </summary>
        public ObjectNode LayerConfigRootNode { get; internal set; }

        /// <summary>
        /// Gets the mapping of a DOM path within this layer to the relative file path
        /// that originally defined it. This is critical for saving changes.
        /// Key: DOM path (e.g., "/network/timeout").
        /// Value: Relative file path (e.g., "network/settings.json").
        /// </summary>
        public Dictionary<string, string> IntraLayerValueOrigins { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this layer has unsaved changes.
        /// </summary>
        public bool IsDirty { get; set; }

        /// <summary>
        /// Gets the set of files that should be deleted when this layer is saved.
        /// These are files that have been consolidated into other files.
        /// </summary>
        public HashSet<string> FilesToDeleteOnSave { get; } = new();

        /// <summary>
        /// Initializes a new instance of the CascadeLayer class.
        /// </summary>
        /// <param name="layerIndex">The layer's index in the cascade.</param>
        /// <param name="name">The user-friendly name of the layer.</param>
        /// <param name="folderPath">The path to the layer's source folder.</param>
        /// <param name="sourceFiles">The list of all parsed files from the folder.</param>
        /// <param name="layerConfigRootNode">The single DOM tree resulting from the intra-layer merge.</param>
        /// <param name="intraLayerValueOrigins">The map tracking the origin file of each property.</param>
        public CascadeLayer(
            int layerIndex,
            string name,
            string folderPath,
            IReadOnlyList<SourceFileInfo> sourceFiles,
            ObjectNode layerConfigRootNode,
            Dictionary<string, string> intraLayerValueOrigins)
        {
            LayerIndex = layerIndex;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            FolderPath = folderPath ?? throw new ArgumentNullException(nameof(folderPath));
            SourceFiles = sourceFiles ?? throw new ArgumentNullException(nameof(sourceFiles));
            LayerConfigRootNode = layerConfigRootNode ?? throw new ArgumentNullException(nameof(layerConfigRootNode));
            IntraLayerValueOrigins = intraLayerValueOrigins ?? throw new ArgumentNullException(nameof(intraLayerValueOrigins));
            IsDirty = false;
        }
    }
}

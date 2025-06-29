using JsonConfigEditor.Core.Dom; // For DomNode
using System;
using System.Collections.Generic;
using System.IO; // Required for Path operations if any are done here

namespace JsonConfigEditor.Core.Cascading
{
    /// <summary>
    /// Represents a single layer in the configuration cascade.
    /// A layer's configuration is derived from one or more source files within its folder,
    /// which are merged to form this layer's complete DOM tree (LayerConfigRootNode).
    /// </summary>
    public class CascadeLayer
    {
        /// <summary>
        /// Gets the user-friendly name of the layer (e.g., "Base", "Site Overrides").
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the resolved absolute path to the folder containing the JSON files for this layer.
        /// </summary>
        public string ResolvedFolderPath { get; }

        /// <summary>
        /// Gets the index of this layer in the cascade order (0 is lowest precedence).
        /// </summary>
        public int LayerIndex { get; }

        /// <summary>
        /// Gets a value indicating whether this layer is read-only.
        /// If true, the editor should prevent modifications to this layer's content.
        /// </summary>
        public bool IsReadOnly { get; }

        /// <summary>
        /// Gets the list of source file information objects that constitute this layer.
        /// These are discovered by scanning the <see cref="ResolvedFolderPath"/>.
        /// </summary>
        public List<SourceFileInfo> SourceFiles { get; internal set; } // Set by loading process

        /// <summary>
        /// Gets or sets the merged DOM tree representing the complete configuration of this single layer.
        /// This is formed by merging all its <see cref="SourceFiles"/> and is the tree that gets edited
        /// when this layer is the selected editor layer.
        /// </summary>
        public DomNode LayerConfigRootNode { get; set; } // Set by intra-layer merge process

        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="LayerConfigRootNode"/>
        /// has changes compared to the content derived from its <see cref="SourceFiles"/>
        /// since the last load or save operation.
        /// </summary>
        public bool IsDirty { get; set; }

        /// <summary>
        /// Tracks the origin of values within this layer's <see cref="LayerConfigRootNode"/>.
        /// Key: A JSON path string relative to <see cref="LayerConfigRootNode"/>.
        /// Value: The <see cref="SourceFileInfo.RelativePathInLayer"/> of the file from which the value at this path originated.
        /// This is populated during the intra-layer merge process.
        /// </summary>
        internal Dictionary<string, string> IntraLayerValueOrigins { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CascadeLayer"/> class using a <see cref="LayerDefinition"/>.
        /// The actual loading of source files and construction of <see cref="LayerConfigRootNode"/>
        /// is handled by a separate loading process (e.g., in MainViewModel or a dedicated service).
        /// </summary>
        /// <param name="definition">The layer definition parsed from the cascade project file.</param>
        public CascadeLayer(LayerDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            Name = definition.Name;
            ResolvedFolderPath = definition.ResolvedFolderPath ?? throw new ArgumentException("ResolvedFolderPath cannot be null in LayerDefinition.", nameof(definition));
            LayerIndex = definition.LayerIndex;
            IsReadOnly = definition.IsReadOnly;

            SourceFiles = new List<SourceFileInfo>();
            // LayerConfigRootNode will be initialized by the intra-layer merge process.
            // For safety, initialize to a basic ObjectNode. The loader will replace it.
            LayerConfigRootNode = new ObjectNode($"layer_{LayerIndex}_{Name}_root", null);
            IsDirty = false;
            IntraLayerValueOrigins = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            return $"{Name} (Index: {LayerIndex}, Path: {ResolvedFolderPath}, ReadOnly: {IsReadOnly}, Dirty: {IsDirty}, Files: {SourceFiles.Count})";
        }
    }
}

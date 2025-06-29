// Namespace will align with other Core entities, e.g., JsonConfigEditor.Core.Cascading
namespace JsonConfigEditor.Core.Cascading
{
    /// <summary>
    /// Represents the definition of a single cascade layer, typically parsed from
    /// the main cascade project configuration file (e.g., cascade_project.jsonc).
    /// </summary>
    public class LayerDefinition
    {
        private string _folderPath = string.Empty;

        /// <summary>
        /// Gets or sets the user-friendly name of the layer (e.g., "Base", "Site Overrides").
        /// This name is used for display purposes in the UI.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the folder containing the JSON files for this layer.
        /// This can be an absolute path or a path relative to the cascade project file.
        /// </summary>
        public string FolderPath
        {
            get => _folderPath;
            set => _folderPath = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this layer is read-only.
        /// If true, the editor should prevent modifications to this layer's content.
        /// Defaults to false.
        /// </summary>
        public bool IsReadOnly { get; set; } = false;

        /// <summary>
        /// Gets or sets the index of this layer in the cascade order (0 is lowest precedence).
        /// This is typically assigned based on the order of definitions in the project file.
        /// </summary>
        public int LayerIndex { get; set; } = -1;

        /// <summary>
        /// Gets or sets the resolved absolute path to the folder for this layer.
        /// This is populated after the FolderPath is resolved relative to the project file location.
        /// </summary>
        public string ResolvedFolderPath { get; set; } = string.Empty;

        public LayerDefinition() { }

        public LayerDefinition(string name, string folderPath, bool isReadOnly = false, int layerIndex = -1)
        {
            Name = name;
            FolderPath = folderPath;
            IsReadOnly = isReadOnly;
            LayerIndex = layerIndex;
            // ResolvedFolderPath would be set after loading and resolving FolderPath
        }

        public override string ToString()
        {
            return $"{Name} (Index: {LayerIndex}, Path: {FolderPath}, ReadOnly: {IsReadOnly})";
        }
    }
}

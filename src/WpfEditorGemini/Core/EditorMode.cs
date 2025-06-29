// Namespace can be global or specific, e.g., JsonConfigEditor.Core
namespace JsonConfigEditor.Core // Using a more general core namespace
{
    /// <summary>
    /// Defines the operational modes for the editor, determining whether it's handling
    /// a single JSON file or a multi-layer cascade project.
    /// </summary>
    public enum EditorMode
    {
        /// <summary>
        /// The editor is working with a single, standalone JSON file.
        /// Cascade-specific features are disabled.
        /// </summary>
        SingleFile,

        /// <summary>
        /// The editor is working with a cascade project defined by a project file,
        /// managing multiple layers of configuration.
        /// </summary>
        CascadeProject
    }
}

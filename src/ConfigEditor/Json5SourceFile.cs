
namespace ConfigDom
{
    /// <summary>
    /// Represents a single parsed JSON5 file in the editor.
    /// Stores file path, parsed DOM root, and raw text content.
    /// Used to track editable values and support regeneration.
    /// </summary>
    public class Json5SourceFile
    {
        /// <summary>
        /// Gets the full absolute file path to the source JSON5 file.
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// Gets the relative path used to construct the logical DOM mount.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// The parsed DOM root node for this file.
        /// </summary>
        public ObjectNode DomRoot { get; }

        /// <summary>
        /// The raw JSON5 text originally read from disk.
        /// </summary>
        public string RawText { get; }

        /// <summary>
        /// Initializes a new Json5SourceFile instance with parsed content.
        /// </summary>
        /// <param name="fullPath">Absolute file path on disk.</param>
        /// <param name="relativePath">Relative path within the cascade folder.</param>
        /// <param name="domRoot">Parsed ObjectNode tree.</param>
        /// <param name="rawText">The unmodified original text.</param>
        public Json5SourceFile(string fullPath, string relativePath, ObjectNode domRoot, string rawText)
        {
            FullPath = fullPath;
            RelativePath = relativePath;
            DomRoot = domRoot;
            RawText = rawText;
        }
    }
}

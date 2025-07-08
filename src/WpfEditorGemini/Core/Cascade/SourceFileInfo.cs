using RuntimeConfig.Core.Dom;
using System;

namespace JsonConfigEditor.Core.Cascade
{
    /// <summary>
    /// Represents a single parsed JSON source file, holding its location,
    /// its parsed DOM representation, and its original text content for change detection.
    /// </summary>
    public class SourceFileInfo
    {
        /// <summary>
        /// Gets the absolute path to the source file on disk.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the path of this file relative to its layer's root folder.
        /// e.g., "network/settings.json"
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Gets the root of the DOM tree parsed from this file's content.
        /// </summary>
        public DomNode DomRoot { get; }

        /// <summary>
        /// Gets the original raw text content of the file.
        /// Used to compare against new content to determine if a save is necessary.
        /// </summary>
        public string OriginalText { get; }

        /// <summary>
        /// Gets the index of the layer this source file belongs to.
        /// </summary>
        public int LayerIndex { get; }

        /// <summary>
        /// Initializes a new instance of the SourceFileInfo class.
        /// </summary>
        /// <param name="filePath">The absolute path to the source file.</param>
        /// <param name="relativePath">The path relative to the layer's root folder.</param>
        /// <param name="domRoot">The parsed DomNode tree from the file.</param>
        /// <param name="originalText">The raw text content of the file.</param>
        /// <param name="layerIndex">The index of the layer this source file belongs to.</param>
        public SourceFileInfo(string filePath, string relativePath, DomNode domRoot, string originalText, int layerIndex)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            DomRoot = domRoot ?? throw new ArgumentNullException(nameof(domRoot));
            OriginalText = originalText ?? throw new ArgumentNullException(nameof(originalText));
            LayerIndex = layerIndex;
        }
    }
}



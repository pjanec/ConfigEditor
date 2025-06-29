using JsonConfigEditor.Core.Dom; // For DomNode
using System;

namespace JsonConfigEditor.Core.Cascading
{
    /// <summary>
    /// Holds information about a single source file that contributes to a CascadeLayer.
    /// </summary>
    public class SourceFileInfo
    {
        /// <summary>
        /// Gets the absolute file path of this source file.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the path of this file relative to its CascadeLayer's root folder.
        /// This path is used to determine its structure/position within the layer's merged DOM.
        /// Example: "network/settings.json" or "common.json".
        /// </summary>
        public string RelativePathInLayer { get; }

        /// <summary>
        /// Gets or sets the DomNode tree parsed directly from this file's content.
        /// This represents the state of the file as of the last load or save.
        /// </summary>
        public DomNode OriginalContentRoot { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of when this file was last loaded or saved.
        /// Can be used for external change detection if needed.
        /// </summary>
        public DateTime LastProcessedTimestamp { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceFileInfo"/> class.
        /// </summary>
        /// <param name="filePath">The absolute file path.</param>
        /// <param name="relativePathInLayer">The path relative to the layer's root folder.</param>
        /// <param name="originalContentRoot">The DomNode parsed from this file.</param>
        public SourceFileInfo(string filePath, string relativePathInLayer, DomNode originalContentRoot)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            RelativePathInLayer = relativePathInLayer ?? throw new ArgumentNullException(nameof(relativePathInLayer));
            OriginalContentRoot = originalContentRoot ?? throw new ArgumentNullException(nameof(originalContentRoot));
            LastProcessedTimestamp = DateTime.UtcNow; // Set current time on creation/load
        }

        public override string ToString()
        {
            return $"{RelativePathInLayer} (at {FilePath})";
        }
    }
}

using RuntimeConfig.Core.Dom;

namespace RuntimeConfig.Core.Models
{
    /// <summary>
    /// Represents a single parsed source file loaded from a layer.
    /// </summary>
    /// <param name="FilePath">The absolute path to the source file on disk.</param>
    /// <param name="RelativePath">The path of this file relative to its layer's root folder.</param>
    /// <param name="DomRoot">The root of the DOM tree parsed from this file's content.</param>
    public record SourceFileInfo(string FilePath, string RelativePath, DomNode DomRoot, string OriginalText, int LayerIndex);
} 
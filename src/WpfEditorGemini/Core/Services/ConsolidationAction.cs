namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// Represents a single proposed consolidation action to merge overlapping files.
    /// </summary>
    /// <param name="AncestorFile">The ancestor file path.</param>
    /// <param name="DescendantFile">The descendant file path.</param>
    /// <param name="TopLevelPropertyPath">The top-level property path.</param>
    /// <param name="LayerName">The layer name.</param>
    public record ConsolidationAction(string AncestorFile, string DescendantFile, string TopLevelPropertyPath, string LayerName);
} 
using System;

namespace ConfigDom.Editor
{
    /// <summary>
    /// Specialized viewmodel for RefNode.
    /// Tracks the resolved target node and provides a live preview.
    /// Enables navigation and validation inside the editor UI.
    /// </summary>
    public class RefNodeViewModel : DomNodeViewModel
    {
        /// <summary>
        /// The resolved target node of the $ref, if available.
        /// May be null if unresolved or invalid.
        /// </summary>
        public DomNode? ResolvedTargetNode { get; set; }

        /// <summary>
        /// A textual preview of the resolved value, used in the editor UI.
        /// </summary>
        public string? ResolvedPreviewValue { get; set; }

        /// <summary>
        /// Constructs a viewmodel for a RefNode.
        /// </summary>
        /// <param name="node">The RefNode this viewmodel wraps.</param>
        /// <param name="path">Full path of the node in the DOM.</param>
        /// <param name="parent">Parent viewmodel node, if any.</param>
        /// <param name="history">Shared undo history instance.</param>
        public RefNodeViewModel(RefNode node, string path, DomNodeViewModel? parent = null, DomEditHistory? history = null)
            : base(node, path, parent, history)
        {
        }

        /// <summary>
        /// Provides the reference path as declared in the RefNode.
        /// </summary>
        public string RefPath => ((RefNode)Node).RefPath;
    }
}

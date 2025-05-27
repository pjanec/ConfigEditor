using System;

namespace JsonConfigEditor.Core.Dom
{
    /// <summary>
    /// Abstract base class for all nodes in the JSON Document Object Model (DOM) tree.
    /// Provides common properties and functionality for all node types.
    /// (From specification document, Section 2.1)
    /// </summary>
    public abstract class DomNode
    {
        /// <summary>
        /// Gets the name of the node (property name or array index as string).
        /// For root nodes, this is typically "$root".
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the parent node. Null for root nodes.
        /// </summary>
        public DomNode? Parent { get; }

        /// <summary>
        /// Gets the depth of this node in the tree (0 for root).
        /// </summary>
        public int Depth { get; }

        /// <summary>
        /// Gets the full path from root to this node using forward slashes as separators.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Initializes a new instance of the DomNode class.
        /// </summary>
        /// <param name="name">The name of the node</param>
        /// <param name="parent">The parent node (null for root)</param>
        protected DomNode(string name, DomNode? parent)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Parent = parent;
            Depth = parent?.Depth + 1 ?? 0;
            Path = BuildPath();
        }

        /// <summary>
        /// Builds the full path from root to this node.
        /// </summary>
        /// <returns>The full path string</returns>
        private string BuildPath()
        {
            if (Parent == null)
            {
                return Name == "$root" ? "" : Name;
            }

            var parentPath = Parent.Path;
            if (string.IsNullOrEmpty(parentPath))
            {
                return Name;
            }

            return $"{parentPath}/{Name}";
        }

        /// <summary>
        /// Returns a string representation of this node for debugging.
        /// </summary>
        public override string ToString()
        {
            return $"{GetType().Name}: {Name} (Path: {Path})";
        }
    }
} 
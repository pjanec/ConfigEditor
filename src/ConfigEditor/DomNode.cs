using System.Collections.Generic;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Abstract base class for all DOM node types (objects, arrays, values, references).
    /// Each node has a name and optional parent to support hierarchical traversal.
    /// </summary>
    public abstract class DomNode
    {
        /// <summary>
        /// The key or name of this node in its parent container.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optional parent node in the DOM tree.
        /// </summary>
        public DomNode? Parent { get; set; }

        protected DomNode(string name, DomNode? parent = null)
        {
            Name = name;
            Parent = parent;
        }

        /// <summary>
        /// Exports the DOM subtree rooted at this node into a JsonElement.
        /// </summary>
        /// <returns>Serialized JsonElement representing this node.</returns>
        public abstract JsonElement ExportJson();

        /// <summary>
        /// Gets the absolute path to this node in the DOM tree.
        /// </summary>
        public string GetAbsolutePath()
        {
            var parts = new List<string>();
            var current = this;
            while (current != null)
            {
                if (current.Name != null)
                {
                    parts.Add(current.Name);
                }
                current = current.Parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}

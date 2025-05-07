using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Represents a DOM node that contains named child nodes, like a JSON object.
    /// </summary>
    public class ObjectNode : DomNode
    {
        /// <summary>
        /// Named children of this object node.
        /// </summary>
        public Dictionary<string, DomNode> Children { get; } = new();

        /// <summary>
        /// The source file that this node came from, if any.
        /// </summary>
        public Json5SourceFile? File { get; set; }

        /// <summary>
        /// Constructs a new object node with a name and optional parent.
        /// </summary>
        /// <param name="name">The key name of this object node in its parent.</param>
        /// <param name="parent">The parent node in the tree, if any.</param>
        public ObjectNode(string name, DomNode? parent = null)
            : base(name, parent)
        {
            Name = name;
            Parent = parent;
            Children = new Dictionary<string, DomNode>();
        }

        /// <summary>
        /// Adds or replaces a child node under the given name.
        /// </summary>
        public void AddChild(DomNode child)
        {
            Children[child.Name] = child;
            child.Parent = this;
        }

        /// <summary>
        /// Attempts to retrieve a child node by name.
        /// </summary>
        public bool TryGetChild(string name, out DomNode? node) => Children.TryGetValue(name, out node);

        /// <summary>
        /// Removes a child node by name if it exists.
        /// </summary>
        public void RemoveChild(string name) => Children.Remove(name);

        /// <summary>
        /// Serializes the node and its children into a JsonElement.
        /// </summary>
        public override JsonElement ExportJson()
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                foreach (var kvp in Children)
                {
                    writer.WritePropertyName(kvp.Key);
                    kvp.Value.ExportJson().WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
        }

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
                current = current.Parent as ObjectNode;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}

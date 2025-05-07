using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Represents an ordered list of child DomNodes, analogous to a JSON array.
    /// Used to model structured array data within the DOM tree.
    /// Maintains the ordering and allows access and manipulation of its elements.
    /// </summary>
    public class ArrayNode : DomNode
    {
        private readonly List<DomNode> _items;

        /// <summary>
        /// Initializes a new ArrayNode with the specified name and optional parent.
        /// </summary>
        /// <param name="name">The name of the node in its parent's context.</param>
        /// <param name="parent">The parent node, if any.</param>
        public ArrayNode(string name, DomNode? parent = null) : base(name, parent)
        {
            _items = new List<DomNode>();
        }

        /// <summary>
        /// Gets a read-only list of the child nodes contained in this array.
        /// </summary>
        public IReadOnlyList<DomNode> Items => _items;

        /// <summary>
        /// Adds a new child item to the end of the array.
        /// </summary>
        /// <param name="item">The DomNode to add.</param>
        public void AddItem(DomNode item)
        {
            _items.Add(item);
        }

        /// <summary>
        /// Removes the item at the specified index.
        /// </summary>
        /// <param name="index">Index of the item to remove.</param>
        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
        }

        /// <summary>
        /// Accesses an item by its index.
        /// </summary>
        /// <param name="index">The index to retrieve.</param>
        /// <returns>The DomNode at the given index.</returns>
        public DomNode this[int index] => _items[index];

        /// <summary>
        /// Serializes the array and all child nodes into a JsonElement.
        /// Used during export and introspection to convert to standard JSON form.
        /// </summary>
        /// <returns>A JsonElement representing this array node and its children.</returns>
        public override JsonElement ExportJson()
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartArray();
                foreach (var item in _items)
                {
                    item.ExportJson().WriteTo(writer);
                }
                writer.WriteEndArray();
            }
            return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
        }
    }
}

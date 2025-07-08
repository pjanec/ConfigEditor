using System;
using System.Collections.Generic;
using System.Linq;

namespace RuntimeConfig.Core.Dom
{
    /// <summary>
    /// Represents a JSON array node in the DOM tree.
    /// Contains a list of child nodes representing array elements.
    /// (From specification document, Section 2.1)
    /// </summary>
    public class ArrayNode : DomNode
    {
        private readonly List<DomNode> _items;

        /// <summary>
        /// Gets the list of array elements.
        /// </summary>
        public IReadOnlyList<DomNode> Items => _items;

        /// <summary>
        /// Initializes a new instance of the ArrayNode class.
        /// </summary>
        /// <param name="name">The name of the node</param>
        /// <param name="parent">The parent node</param>
        public ArrayNode(string name, DomNode? parent) : base(name, parent)
        {
            _items = new List<DomNode>();
        }

        /// <summary>
        /// Adds an item to the end of the array.
        /// </summary>
        /// <param name="item">The item to add</param>
        public void AddItem(DomNode item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            _items.Add(item);
        }

        /// <summary>
        /// Inserts an item at the specified index.
        /// </summary>
        /// <param name="index">The index to insert at</param>
        /// <param name="item">The item to insert</param>
        public void InsertItem(int index, DomNode item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (index < 0 || index > _items.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            _items.Insert(index, item);
            Reindex();
        }

        /// <summary>
        /// Removes an item at the specified index.
        /// </summary>
        /// <param name="index">The index of the item to remove</param>
        /// <returns>True if the item was removed, false if the index was invalid</returns>
        public bool RemoveItemAt(int index)
        {
            if (index < 0 || index >= _items.Count)
                return false;

            _items.RemoveAt(index);
            Reindex();
            return true;
        }

        /// <summary>
        /// Removes the specified item from the array.
        /// </summary>
        /// <param name="item">The item to remove</param>
        /// <returns>True if the item was removed, false if it wasn't found</returns>
        public bool RemoveItem(DomNode item)
        {
            int index = _items.IndexOf(item);
            if (index >= 0)
            {
                return RemoveItemAt(index);
            }
            return false;
        }

        /// <summary>
        /// Gets an item at the specified index.
        /// </summary>
        /// <param name="index">The index of the item</param>
        /// <returns>The item at the specified index, or null if the index is invalid</returns>
        public DomNode? GetItem(int index)
        {
            if (index < 0 || index >= _items.Count)
                return null;

            return _items[index];
        }

        /// <summary>
        /// Replaces an item at the specified index.
        /// </summary>
        /// <param name="index">The index of the item to replace</param>
        /// <param name="newItem">The new item</param>
        /// <returns>True if the replacement was successful, false if the index was invalid</returns>
        public bool ReplaceItem(int index, DomNode newItem)
        {
            if (index < 0 || index >= _items.Count)
                return false;

            if (newItem == null)
                throw new ArgumentNullException(nameof(newItem));

            _items[index] = newItem;
            return true;
        }

        /// <summary>
        /// Gets the number of items in the array.
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// Gets the index of the specified item.
        /// </summary>
        /// <param name="item">The item to find</param>
        /// <returns>The index of the item, or -1 if not found</returns>
        public int IndexOf(DomNode item)
        {
            return _items.IndexOf(item);
        }

        /// <summary>
        /// Clears all items from the array.
        /// </summary>
        public void Clear()
        {
            _items.Clear();
        }

        private DomNode RecreateNodeWithNewName(DomNode oldNode, string newName)
        {
            switch (oldNode)
            {
                case ValueNode v:
                    return new ValueNode(newName, this, v.Value);
                case RefNode r:
                    return new RefNode(newName, this, r.ReferencePath, r.OriginalValue);
                case ObjectNode o:
                    var newObject = new ObjectNode(newName, this);
                    foreach (var child in o.Children.Values)
                    {
                        newObject.AddChild(child.Name, DomCloning.CloneNode(child, newObject));
                    }
                    return newObject;
                case ArrayNode a:
                    var newArray = new ArrayNode(newName, this);
                    foreach (var item in a.Items)
                    {
                        newArray.AddItem(DomCloning.CloneNode(item, newArray));
                    }
                    return newArray;
                default:
                    throw new NotSupportedException($"Unsupported node type for re-indexing: {oldNode.GetType().Name}");
            }
        }

        private void Reindex()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var oldNode = _items[i];
                string newName = i.ToString();

                if (oldNode.Name != newName)
                {
                    _items[i] = RecreateNodeWithNewName(oldNode, newName);
                }
            }
        }

        /// <summary>
        /// Gets all items in the array.
        /// </summary>
        /// <returns>Enumerable of all items</returns>
        public IEnumerable<DomNode> GetItems()
        {
            return _items;
        }

        /// <summary>
        /// Returns a string representation of this array node.
        /// </summary>
        public override string ToString()
        {
            return $"ArrayNode: {Name} ({Count} items) (Path: {Path})";
        }
    }
} 
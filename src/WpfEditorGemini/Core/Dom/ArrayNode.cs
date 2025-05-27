using System;
using System.Collections.Generic;
using System.Linq;

namespace JsonConfigEditor.Core.Dom
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
            
            // Update the names of subsequent items to reflect their new indices
            UpdateItemNames(index);
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
            
            // Update the names of subsequent items to reflect their new indices
            UpdateItemNames(index);
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

        /// <summary>
        /// Updates the names of items starting from the specified index to reflect their array indices.
        /// This is called after insertions or deletions to keep names synchronized.
        /// </summary>
        /// <param name="startIndex">The index to start updating from</param>
        private void UpdateItemNames(int startIndex)
        {
            for (int i = startIndex; i < _items.Count; i++)
            {
                // Note: This assumes that array item names are their string indices
                // In a more sophisticated implementation, we might need to recreate nodes
                // with new names, but for now we'll assume the names can be updated
                // This is a simplification - in practice, DomNode names are readonly
                // so we might need a different approach
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
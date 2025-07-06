using System;
using System.Collections.Generic;
using System.Linq;

namespace JsonConfigEditor.Core.Dom
{
    /// <summary>
    /// Represents a JSON object node in the DOM tree.
    /// Contains a dictionary of child nodes keyed by property name.
    /// (From specification document, Section 2.1)
    /// </summary>
    public class ObjectNode : DomNode
    {
        private readonly Dictionary<string, DomNode> _children;

        /// <summary>
        /// Gets the dictionary of child nodes keyed by property name.
        /// </summary>
        public IReadOnlyDictionary<string, DomNode> Children => _children;

        /// <summary>
        /// Initializes a new instance of the ObjectNode class.
        /// </summary>
        /// <param name="name">The name of the node</param>
        /// <param name="parent">The parent node</param>
        public ObjectNode(string name, DomNode? parent) : base(name, parent)
        {
            // CHANGE: Initialize the dictionary to be case-insensitive.
            _children = new Dictionary<string, DomNode>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds a child node to this object.
        /// </summary>
        /// <param name="propertyName">The property name</param>
        /// <param name="child">The child node to add</param>
        /// <exception cref="ArgumentException">Thrown if a property with the same name already exists</exception>
        public void AddChild(string propertyName, DomNode child)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentException("Property name cannot be null or empty", nameof(propertyName));

            if (_children.ContainsKey(propertyName))
                throw new ArgumentException($"Property '{propertyName}' already exists in this object", nameof(propertyName));

            _children[propertyName] = child ?? throw new ArgumentNullException(nameof(child));
        }

        /// <summary>
        /// Removes a child node from this object.
        /// </summary>
        /// <param name="propertyName">The property name to remove</param>
        /// <returns>True if the property was removed, false if it didn't exist</returns>
        public bool RemoveChild(string propertyName)
        {
            return _children.Remove(propertyName);
        }

        /// <summary>
        /// Gets a child node by property name.
        /// </summary>
        /// <param name="propertyName">The property name</param>
        /// <returns>The child node, or null if not found</returns>
        public DomNode? GetChild(string propertyName)
        {
            _children.TryGetValue(propertyName, out var child);
            return child;
        }

        /// <summary>
        /// Checks if a property exists in this object.
        /// </summary>
        /// <param name="propertyName">The property name to check</param>
        /// <returns>True if the property exists, false otherwise</returns>
        public bool HasProperty(string propertyName)
        {
            return _children.ContainsKey(propertyName);
        }

        /// <summary>
        /// Gets all property names in this object.
        /// </summary>
        /// <returns>Collection of property names</returns>
        public IEnumerable<string> GetPropertyNames()
        {
            return _children.Keys;
        }

        /// <summary>
        /// Gets all child nodes in this object.
        /// </summary>
        /// <returns>Collection of child nodes</returns>
        public IEnumerable<DomNode> GetChildren()
        {
            return _children.Values;
        }

        /// <summary>
        /// Gets the number of properties in this object.
        /// </summary>
        public int Count => _children.Count;

        /// <summary>
        /// Replaces a child node with a new one.
        /// </summary>
        /// <param name="propertyName">The property name</param>
        /// <param name="newChild">The new child node</param>
        /// <returns>True if the replacement was successful, false if the property didn't exist</returns>
        public bool ReplaceChild(string propertyName, DomNode newChild)
        {
            if (!_children.ContainsKey(propertyName))
                return false;

            _children[propertyName] = newChild ?? throw new ArgumentNullException(nameof(newChild));
            return true;
        }

        /// <summary>
        /// Clears all children from this object.
        /// </summary>
        public void Clear()
        {
            _children.Clear();
        }

        /// <summary>
        /// Returns a string representation of this object node.
        /// </summary>
        public override string ToString()
        {
            return $"ObjectNode: {Name} ({Count} properties) (Path: {Path})";
        }
    }
} 
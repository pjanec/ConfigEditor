using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Represents a terminal value node in the DOM tree, analogous to a JSON primitive (string, number, bool, etc.).
    /// Leaf nodes hold actual data values and do not have any children.
    /// </summary>
    public class LeafNode : DomNode
    {
        private readonly JsonElement _value;

        /// <summary>
        /// Initializes a new LeafNode with the specified name, value, and optional parent.
        /// </summary>
        /// <param name="name">The key name of the value in the parent's context.</param>
        /// <param name="value">The JSON value to store as a leaf node.</param>
        /// <param name="parent">The parent DOM node, if any.</param>
        public LeafNode(string name, JsonElement value, DomNode? parent = null) : base(name, parent)
        {
            _value = value;
        }

        /// <summary>
        /// Gets the underlying JSON value represented by this leaf node.
        /// </summary>
        public JsonElement Value => _value;

        /// <summary>
        /// Returns the stored value as a JsonElement.
        /// </summary>
        /// <returns>The JSON representation of this leaf node's value.</returns>
        public override JsonElement ExportJson() => _value.Clone();

        public override DomNode Clone()
        {
            return new LeafNode(Name, _value, Parent);
        }
    }
}

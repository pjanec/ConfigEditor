using System.Text.Json;

namespace ConfigEditor.Dom
{
	/// <summary>
	/// Represents a terminal value node in the DOM tree, analogous to a JSON primitive (string, number, bool, etc.).
	/// Leaf nodes hold actual data values and do not have any children.
	/// </summary>
	public class ValueNode : DomNode
	{
		private readonly JsonElement _value;

		/// <summary>
		/// Initializes a new LeafNode with the specified name, value, and optional parent.
		/// </summary>
		/// <param name="name">The key name of the value in the parent's context.</param>
		/// <param name="value">The JSON value to store as a leaf node.</param>
		/// <param name="parent">The parent DOM node, if any.</param>
		public ValueNode( string name, JsonElement value, DomNode? parent = null ) : base( name, parent )
		{
			_value = value;
		}

		/// <summary>
		/// Gets the underlying JSON value represented by this leaf node.
		/// </summary>
		public JsonElement Value => _value;

		public override DomNode Clone()
		{
			return new ValueNode( Name, _value, Parent );
		}
	}
}

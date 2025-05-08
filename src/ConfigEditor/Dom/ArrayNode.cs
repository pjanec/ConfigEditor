using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ConfigEditor.Dom
{
	/// <summary>
	/// Represents an ordered list of child DomNodes, analogous to a JSON array.
	/// Used to model structured array data within the DOM tree.
	/// Maintains ordering and allows mutation of elements.
	/// </summary>
	public class ArrayNode : DomNode
	{
		/// <summary>
		/// Initializes a new ArrayNode with the specified name and optional parent.
		/// </summary>
		/// <param name="name">The name of the node in its parent's context.</param>
		/// <param name="parent">The parent DOM node, if any.</param>
		public ArrayNode( string name, DomNode? parent = null ) : base( name, parent ) { }

		/// <summary>
		/// Mutable list of DOM child elements in this array.
		/// </summary>
		public List<DomNode> Items { get; } = new();

		/// <summary>
		/// Exports this array node to a JSON array representation.
		/// </summary>
		public override JsonElement ExportJson()
		{
			var elements = Items.ConvertAll( item => item.ExportJson() );
			return JsonSerializer.SerializeToElement( elements );
		}

		public override DomNode Clone()
		{
			var clone = new ArrayNode( Name );
			foreach( var item in Items )
			{
				var itemClone = item.Clone();
				itemClone.SetParent( clone );
				clone.Items.Add( itemClone );
			}
			return clone;
		}
	}
}

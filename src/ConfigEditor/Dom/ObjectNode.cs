using ConfigEditor.IO;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ConfigEditor.Dom
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
		public SourceFile? File { get; set; }

		/// <summary>
		/// Constructs a new object node with a name and optional parent.
		/// </summary>
		/// <param name="name">The key name of this object node in its parent.</param>
		/// <param name="parent">The parent node in the tree, if any.</param>
		public ObjectNode( string name, DomNode? parent = null )
			: base( name, parent )
		{
			Children = new Dictionary<string, DomNode>();
		}

		/// <summary>
		/// Adds or replaces a child node under the given name.
		/// </summary>
		public void AddChild( DomNode child )
		{
			Children[child.Name] = child;
			child.Parent = this;
		}

		/// <summary>
		/// Attempts to retrieve a child node by name.
		/// </summary>
		public bool TryGetChild( string name, out DomNode? node ) => Children.TryGetValue( name, out node );

		/// <summary>
		/// Removes a child node by name if it exists.
		/// </summary>
		public void RemoveChild( string name ) => Children.Remove( name );

		/// <summary>
		/// Clones the current object node and its children.
		/// </summary>
		public override DomNode Clone()
		{
			var clone = new ObjectNode( Name );
			foreach( var (key, child) in Children )
			{
				var childClone = child.Clone();
				childClone.SetParent( clone );
				clone.Children[key] = childClone;
			}
			return clone;
		}

	}
}

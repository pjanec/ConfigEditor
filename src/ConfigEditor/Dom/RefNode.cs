using System;
using System.IO;
using System.Text.Json;

namespace ConfigEditor.Dom
{
	/// <summary>
	/// Represents a symbolic reference to another path in the DOM tree.
	/// The reference is encoded using a $ref key and resolved during build/export.
	/// Only supported in editable JSON5 sources (not runtime DOM).
	/// </summary>
	public class RefNode : DomNode
	{
		/// <summary>
		/// Gets the absolute reference path this node points to.
		/// Must be resolvable at build time and must not be relative or scoped.
		/// </summary>
		public string RefPath { get; }

		/// <summary>
		/// Initializes a new reference node with a symbolic path.
		/// </summary>
		/// <param name="name">The field name this node is associated with in its parent.</param>
		/// <param name="refPath">The absolute reference path to resolve.</param>
		/// <param name="parent">The parent node in the DOM tree.</param>
		public RefNode( string name, string refPath, DomNode? parent = null ) : base( name, parent )
		{
			RefPath = refPath;
		}

		public DomNode ResolvedTarget => RefNodeResolver.Resolve( this, DomTreePathHelper.GetRoot(this) );

		public override DomNode Clone()
		{
			return new RefNode( Name, RefPath, Parent );
		}
	}
}

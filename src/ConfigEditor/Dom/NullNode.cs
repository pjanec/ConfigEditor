using System.Text.Json;

namespace ConfigEditor.Dom;

/// <summary>
/// A placeholder node used when building array structures to pre-fill missing elements during unflattening.
/// This is not a JSON null value, but rather a temporary node to pad an ArrayNode.Items list.
/// </summary>
public class NullNode : DomNode
{
	public NullNode( string name, DomNode? parent = null ) : base( name, parent )
	{
	}

	public override DomNode Clone()
	{
		return new NullNode( Name, Parent );
	}

	public override string ToString() => "(null placeholder)";
}
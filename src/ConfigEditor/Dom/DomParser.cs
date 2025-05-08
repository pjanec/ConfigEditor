using System.Collections.Generic;
using System.Text.Json;

namespace ConfigEditor.Dom;

public static class DomParser
{
	/// <summary>
	/// Builds a structured DomNode tree from a flat path map.
	/// Each key in the map is a slash-separated path, and each value is a JsonElement.
	/// Paths may contain object keys and array indices (e.g. "net/interfaces/0/ip").
	/// Arrays must be dense (i.e. if index 5 is present, indices 0 through 4 must also be defined).
	///
	/// Example input flat map:
	/// {
	///     "net/port": 1234,
	///     "net/interfaces/0/ip": "10.0.0.1",
	///     "net/interfaces/1/ip": "10.0.0.2"
	/// }
	///
	/// Output structure:
	/// ObjectNode "net"
	/// ├── LeafNode "port" = 1234
	/// └── ArrayNode "interfaces"
	///     ├── ObjectNode { "ip" = "10.0.0.1" }
	///     └── ObjectNode { "ip" = "10.0.0.2" }
	/// </summary>
	public static DomNode ParseFromFlatMap( Dictionary<string, JsonElement> flatMap )
	{
		var root = new ObjectNode( "$root", null );

		foreach( var (flatPath, value) in flatMap )
		{
			var segments = flatPath.Split( '/' );
			Insert( root, segments, 0, value );
		}

		return root;
	}

	private static void Insert( ObjectNode current, string[] segments, int index, JsonElement value )
	{
		string key = segments[index];
		bool isLast = index == segments.Length - 1;

		if( isLast )
		{
			current.Children[key] = new ValueNode( key, value, current );
			return;
		}

		string nextSegment = segments[index + 1];
		bool nextIsArray = int.TryParse( nextSegment, out _ );

		if( !current.Children.TryGetValue( key, out var existing ) )
		{
			existing = nextIsArray
				? new ArrayNode( key, current )
				: new ObjectNode( key, current );
			current.Children[key] = existing;
		}

		if( nextIsArray )
		{
			var arrayNode = (ArrayNode)existing;
			int arrayIdx = int.Parse( nextSegment );

			// The following is only good if we need to support sparse arrays.
			// But Json sources does not allow for that, so it is not needed.
			// Instead, we will throw an exception if the array is not dense.
			//while (arrayNode.Items.Count <= arrayIdx)
			//    arrayNode.Items.Add(new NullNode("$null", arrayNode));
			if( arrayIdx != arrayNode.Items.Count )
				throw new InvalidOperationException( $"Unexpected non-dense array index {arrayIdx}" );

			if( segments.Length - 1 == index + 1 )
			{
				arrayNode.Items[arrayIdx] = new ValueNode( nextSegment, value, arrayNode );
			}
			else
			{
				if( arrayNode.Items[arrayIdx] is not ObjectNode innerObj )
				{
					innerObj = new ObjectNode( nextSegment, arrayNode );
					arrayNode.Items[arrayIdx] = innerObj;
				}
				Insert( innerObj, segments, index + 2, value );
			}
		}
		else
		{
			Insert( (ObjectNode)existing, segments, index + 1, value );
		}
	}
}

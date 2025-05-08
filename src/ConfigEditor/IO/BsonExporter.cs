using System.IO;
using System.Text.Json;
using ConfigEditor.Dom;
using ConfigEditor.Schema;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace ConfigEditor.IO;

public static class BsonExporter
{
	/// <summary>
	/// Resolves all $ref nodes, applies defaults, and writes the resulting DOM tree as BSON.
	/// </summary>
	/// <param name="root">The root DOM tree to export.</param>
	/// <param name="schema">The schema used to inject default values and validate required fields.</param>
	/// <param name="filePath">Path to the target BSON file.</param>
	public static void ExportToBsonFile( DomNode root, SchemaNode schema, string filePath )
	{
		// Inline all $ref nodes
		RefNodeResolver.ResolveAllInPlace( root );

		// Fill in any missing default-valued fields
		SchemaDefaultInjector.ApplyDefaults( root, schema );

		// Export to JSON first
		var json = root.ExportJson();

		// Convert to BSON and write to file
		var bson = BsonSerializer.Deserialize<BsonDocument>( json.GetRawText() );
		using var writer = new BsonBinaryWriter( File.Create( filePath ) );
		BsonSerializer.Serialize( writer, bson );
	}
}

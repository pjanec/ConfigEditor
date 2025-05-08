using System.Text.Json;
using ConfigEditor.Dom;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace ConfigEditor.IO
{
	/// <summary>
	/// Converts a BSON binary blob into a DomNode tree.
	/// Used by the runtime to load prebuilt configuration.
	/// </summary>
	public static class BsonImporter
	{
		public static DomNode Import( byte[] bsonData )
		{
			var doc = BsonSerializer.Deserialize<BsonDocument>( bsonData );
			var json = doc.ToJson( new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson } );
			var element = JsonDocument.Parse( json ).RootElement;
			return JsonDomBuilder.BuildFromJsonElement( "root", element );
		}
	}
}

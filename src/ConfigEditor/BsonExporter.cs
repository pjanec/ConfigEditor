using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace ConfigDom
{
    /// <summary>
    /// Converts a DomNode subtree into a BSON binary blob.
    /// Used during editor export to persist a resolved config state.
    /// </summary>
    public static class BsonExporter
    {
        public static byte[] Export(DomNode root)
        {
            var json = root.ExportJson();
            var doc = BsonSerializer.Deserialize<BsonDocument>(json.GetRawText());
            return doc.ToBson();
        }
    }
}

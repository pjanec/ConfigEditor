using System.Text.Json;
using System.Text.Json.Nodes;
using ConfigEditor.Dom;

namespace ConfigEditor.Util
{
    public static class ExportJsonSubtree
    {
        public static JsonNode? ToJsonNode(DomNode node)
        {
            return node switch
            {
                ObjectNode obj => ExportObject(obj),
                ArrayNode arr => ExportArray(arr),
                LeafNode leaf => ExportLeaf(leaf),
                RefNode r => JsonValue.Create(r.RefPath),
                _ => null
            };
        }

        private static JsonObject ExportObject(ObjectNode obj)
        {
            var json = new JsonObject();
            foreach (var (key, child) in obj.GetChildren())
            {
                if (key != null)
                    json[key] = ToJsonNode(child);
            }
            return json;
        }

        private static JsonArray ExportArray(ArrayNode arr)
        {
            var json = new JsonArray();
            foreach (var (_, child) in arr.GetChildren())
            {
                json.Add(ToJsonNode(child));
            }
            return json;
        }

        private static JsonValue? ExportLeaf(LeafNode leaf)
        {
            return JsonValue.Create(leaf.GetValue());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConfigEditor.Dom
{
    public static class DomExportExtensions
    {
        public static JsonNode ExportJsonNode(this DomNode node)
        {
            return node switch
            {
                RefNode r => new JsonObject { ["$ref"] = r.RefPath },
                ValueNode v => JsonValue.Create(v.Value)!,
                ObjectNode o => ExportObject(o),
                ArrayNode a => ExportArray(a),
                _ => throw new NotSupportedException($"Unsupported node type: {node.GetType().Name}")
            };
        }

        public static JsonElement ExportJson(this DomNode node)
        {
            var jsonNode = node.ExportJsonNode();
            return JsonSerializer.SerializeToElement(jsonNode);
        }

        private static JsonObject ExportObject(ObjectNode obj)
        {
            var result = new JsonObject();
            foreach (var (key, child) in obj.Children)
                result[key] = child.ExportJsonNode();
            return result;
        }

        private static JsonArray ExportArray(ArrayNode arr)
        {
            var result = new JsonArray();
            foreach (var item in arr.Items)
                result.Add(item.ExportJsonNode());
            return result;
        }
    }
}

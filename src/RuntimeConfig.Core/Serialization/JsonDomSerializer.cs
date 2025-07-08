using System;
using System.Text.Json;
using RuntimeConfig.Core.Dom;
using System.Collections.Generic;

namespace RuntimeConfig.Core.Serialization
{
    /// <summary>
    /// Serializes a DOM tree to JSON.
    /// </summary>
    public class JsonDomSerializer
    {
        private readonly JsonSerializerOptions _options;

        /// <summary>
        /// Initializes a new instance of the JsonDomSerializer class.
        /// </summary>
        /// <param name="options">Optional JSON serialization options.</param>
        public JsonDomSerializer(JsonSerializerOptions? options = null)
        {
            _options = options ?? new JsonSerializerOptions
            {
                WriteIndented = true
            };
        }

        /// <summary>
        /// Serializes a DOM node to JSON text.
        /// </summary>
        /// <param name="node">The DOM node to serialize.</param>
        /// <returns>The JSON text representation.</returns>
        public string ToJson(DomNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            var jsonElement = SerializeNode(node);
            return jsonElement.GetRawText();
        }

        /// <summary>
        /// Serializes a DOM node to a JsonElement.
        /// </summary>
        /// <param name="node">The DOM node to serialize.</param>
        /// <returns>The JsonElement representation.</returns>
        public JsonElement ToJsonElement(DomNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            return SerializeNode(node);
        }

        /// <summary>
        /// Serializes a DOM node to a JsonElement.
        /// </summary>
        /// <param name="node">The DOM node to serialize.</param>
        /// <returns>The JsonElement representation.</returns>
        private JsonElement SerializeNode(DomNode node)
        {
            return node switch
            {
                ValueNode valueNode => valueNode.Value,
                RefNode refNode => refNode.ToJsonElement(),
                ArrayNode arrayNode => SerializeArray(arrayNode),
                ObjectNode objectNode => SerializeObject(objectNode),
                _ => throw new NotSupportedException($"Unsupported node type: {node.GetType().Name}")
            };
        }

        /// <summary>
        /// Serializes an ArrayNode to a JsonElement.
        /// </summary>
        /// <param name="arrayNode">The ArrayNode to serialize.</param>
        /// <returns>The JsonElement representation.</returns>
        private JsonElement SerializeArray(ArrayNode arrayNode)
        {
            // Build a list of elements
            var elements = new List<JsonElement>();
            foreach (var item in arrayNode.Items)
            {
                elements.Add(SerializeNode(item));
            }
            // Serialize the list to a JsonElement
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(elements, _options));
            return doc.RootElement.Clone();
        }

        /// <summary>
        /// Serializes an ObjectNode to a JsonElement.
        /// </summary>
        /// <param name="objectNode">The ObjectNode to serialize.</param>
        /// <returns>The JsonElement representation.</returns>
        private JsonElement SerializeObject(ObjectNode objectNode)
        {
            // Build a dictionary of properties
            var dict = new Dictionary<string, JsonElement>();
            foreach (var (key, child) in objectNode.Children)
            {
                dict[key] = SerializeNode(child);
            }
            // Serialize the dictionary to a JsonElement
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dict, _options));
            return doc.RootElement.Clone();
        }
    }
} 
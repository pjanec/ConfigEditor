using System;
using System.Text.Json;
using RuntimeConfig.Core.Dom;

namespace RuntimeConfig.Core.Serialization
{
    /// <summary>
    /// Deserializes JSON into a DOM tree.
    /// </summary>
    public class JsonDomDeserializer
    {
        /// <summary>
        /// Deserializes JSON text into a DOM tree.
        /// </summary>
        /// <param name="jsonText">The JSON text to deserialize.</param>
        /// <returns>The root node of the DOM tree.</returns>
        public DomNode FromJson(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText))
                throw new ArgumentException("JSON text cannot be null or empty.", nameof(jsonText));

            try
            {
                using var document = JsonDocument.Parse(jsonText);
                return ParseElement(document.RootElement, "$root", null);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses a JSON element into a DOM node.
        /// </summary>
        /// <param name="element">The JSON element to parse.</param>
        /// <param name="name">The name for the node.</param>
        /// <param name="parent">The parent node.</param>
        /// <returns>The parsed DOM node.</returns>
        private DomNode ParseElement(JsonElement element, string name, DomNode? parent)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => ParseObject(element, name, parent),
                JsonValueKind.Array => ParseArray(element, name, parent),
                _ => ParseValue(element, name, parent)
            };
        }

        /// <summary>
        /// Parses a JSON object into an ObjectNode.
        /// </summary>
        /// <param name="element">The JSON object element.</param>
        /// <param name="name">The name for the node.</param>
        /// <param name="parent">The parent node.</param>
        /// <returns>The parsed ObjectNode.</returns>
        private ObjectNode ParseObject(JsonElement element, string name, DomNode? parent)
        {
            var objectNode = new ObjectNode(name, parent);

            foreach (var property in element.EnumerateObject())
            {
                var childNode = ParseElement(property.Value, property.Name, objectNode);
                objectNode.AddChild(property.Name, childNode);
            }

            return objectNode;
        }

        /// <summary>
        /// Parses a JSON array into an ArrayNode.
        /// </summary>
        /// <param name="element">The JSON array element.</param>
        /// <param name="name">The name for the node.</param>
        /// <param name="parent">The parent node.</param>
        /// <returns>The parsed ArrayNode.</returns>
        private ArrayNode ParseArray(JsonElement element, string name, DomNode? parent)
        {
            var arrayNode = new ArrayNode(name, parent);

            for (int i = 0; i < element.GetArrayLength(); i++)
            {
                var childNode = ParseElement(element[i], i.ToString(), arrayNode);
                arrayNode.AddItem(childNode);
            }

            return arrayNode;
        }

        /// <summary>
        /// Parses a JSON value into a ValueNode or RefNode.
        /// </summary>
        /// <param name="element">The JSON value element.</param>
        /// <param name="name">The name for the node.</param>
        /// <param name="parent">The parent node.</param>
        /// <returns>The parsed ValueNode or RefNode.</returns>
        private DomNode ParseValue(JsonElement element, string name, DomNode? parent)
        {
            // Check if this is a reference node
            if (RefNode.IsRefElement(element))
            {
                return RefNode.FromJsonElement(name, parent, element);
            }

            // Regular value node
            return new ValueNode(name, parent, element);
        }
    }
} 
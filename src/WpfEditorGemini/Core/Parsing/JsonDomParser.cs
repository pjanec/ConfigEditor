using JsonConfigEditor.Core.Dom;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JsonConfigEditor.Core.Parsing
{
    /// <summary>
    /// Implementation of IJsonDomParser for parsing JSON into DOM trees.
    /// (From specification document, Section 2.1)
    /// </summary>
    public class JsonDomParser : IJsonDomParser
    {
        /// <summary>
        /// Parses a JSON string into a DOM tree.
        /// </summary>
        /// <param name="jsonContent">The JSON content to parse</param>
        /// <returns>The root DOM node</returns>
        /// <exception cref="JsonException">Thrown when the JSON is malformed</exception>
        public DomNode ParseFromString(string jsonContent)
        {
            if (string.IsNullOrEmpty(jsonContent))
                throw new ArgumentException("JSON content cannot be null or empty", nameof(jsonContent));

            try
            {
                using var document = JsonDocument.Parse(jsonContent);
                return ParseFromJsonElement(document.RootElement, "$root");
            }
            catch (JsonException)
            {
                throw; // Re-throw JSON parsing exceptions
            }
            catch (Exception ex)
            {
                throw new JsonException($"Failed to parse JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Asynchronously parses a JSON file into a DOM tree.
        /// </summary>
        /// <param name="filePath">The path to the JSON file</param>
        /// <returns>A task containing the root DOM node</returns>
        /// <exception cref="JsonException">Thrown when the JSON is malformed</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist</exception>
        public async Task<DomNode> ParseFromFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            try
            {
                var jsonContent = await File.ReadAllTextAsync(filePath);
                return ParseFromString(jsonContent);
            }
            catch (JsonException)
            {
                throw; // Re-throw JSON parsing exceptions
            }
            catch (FileNotFoundException)
            {
                throw; // Re-throw file not found exceptions
            }
            catch (Exception ex)
            {
                throw new JsonException($"Failed to parse JSON file '{filePath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses a JsonElement into a DOM tree.
        /// </summary>
        /// <param name="jsonElement">The JSON element to parse</param>
        /// <param name="name">The name for the root node</param>
        /// <param name="parent">The parent node (null for root)</param>
        /// <returns>The DOM node representing the JSON element</returns>
        public DomNode ParseFromJsonElement(JsonElement jsonElement, string name, DomNode? parent = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Node name cannot be null or empty", nameof(name));

            return jsonElement.ValueKind switch
            {
                JsonValueKind.Object => ParseObjectElement(jsonElement, name, parent),
                JsonValueKind.Array => ParseArrayElement(jsonElement, name, parent),
                _ => ParseValueElement(jsonElement, name, parent)
            };
        }

        /// <summary>
        /// Parses a JSON object element into an ObjectNode.
        /// </summary>
        private DomNode ParseObjectElement(JsonElement jsonElement, string name, DomNode? parent)
        {
            // Check if this is a reference node (contains $ref property)
            if (RefNode.IsRefElement(jsonElement))
            {
                return RefNode.FromJsonElement(name, parent, jsonElement);
            }

            // Create regular object node
            var objectNode = new ObjectNode(name, parent);

            // Parse all properties
            foreach (var property in jsonElement.EnumerateObject())
            {
                var childNode = ParseFromJsonElement(property.Value, property.Name, objectNode);
                objectNode.AddChild(property.Name, childNode);
            }

            return objectNode;
        }

        /// <summary>
        /// Parses a JSON array element into an ArrayNode.
        /// </summary>
        private DomNode ParseArrayElement(JsonElement jsonElement, string name, DomNode? parent)
        {
            var arrayNode = new ArrayNode(name, parent);

            // Parse all array items
            int index = 0;
            foreach (var item in jsonElement.EnumerateArray())
            {
                var itemNode = ParseFromJsonElement(item, index.ToString(), arrayNode);
                arrayNode.AddItem(itemNode);
                index++;
            }

            return arrayNode;
        }

        /// <summary>
        /// Parses a JSON value element into a ValueNode.
        /// </summary>
        private DomNode ParseValueElement(JsonElement jsonElement, string name, DomNode? parent)
        {
            return new ValueNode(name, parent, jsonElement);
        }
    }
} 
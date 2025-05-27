using JsonConfigEditor.Core.Dom;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JsonConfigEditor.Core.Serialization
{
    /// <summary>
    /// Implementation of IDomNodeToJsonSerializer for serializing DOM trees back to JSON.
    /// (From specification document, Section 2.8)
    /// </summary>
    public class DomNodeToJsonSerializer : IDomNodeToJsonSerializer
    {
        /// <summary>
        /// Serializes a DOM tree to a JSON string.
        /// </summary>
        /// <param name="rootNode">The root DOM node to serialize</param>
        /// <param name="indented">Whether to format the JSON with indentation</param>
        /// <returns>The JSON string representation</returns>
        public string SerializeToString(DomNode rootNode, bool indented = true)
        {
            if (rootNode == null)
                throw new ArgumentNullException(nameof(rootNode));

            var options = new JsonWriterOptions
            {
                Indented = indented
            };

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, options);

            WriteNode(writer, rootNode);
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Asynchronously serializes a DOM tree to a JSON file.
        /// </summary>
        /// <param name="rootNode">The root DOM node to serialize</param>
        /// <param name="filePath">The path where to save the JSON file</param>
        /// <param name="indented">Whether to format the JSON with indentation</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task SerializeToFileAsync(DomNode rootNode, string filePath, bool indented = true)
        {
            if (rootNode == null)
                throw new ArgumentNullException(nameof(rootNode));

            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            try
            {
                var jsonString = SerializeToString(rootNode, indented);
                await File.WriteAllTextAsync(filePath, jsonString);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to serialize DOM tree to file '{filePath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Writes a DOM node to a JSON writer.
        /// </summary>
        /// <param name="writer">The JSON writer</param>
        /// <param name="node">The DOM node to write</param>
        private void WriteNode(Utf8JsonWriter writer, DomNode node)
        {
            switch (node)
            {
                case ValueNode valueNode:
                    WriteValueNode(writer, valueNode);
                    break;
                case RefNode refNode:
                    WriteRefNode(writer, refNode);
                    break;
                case ObjectNode objectNode:
                    WriteObjectNode(writer, objectNode);
                    break;
                case ArrayNode arrayNode:
                    WriteArrayNode(writer, arrayNode);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown node type: {node.GetType().Name}");
            }
        }

        /// <summary>
        /// Writes a value node to the JSON writer.
        /// </summary>
        private void WriteValueNode(Utf8JsonWriter writer, ValueNode valueNode)
        {
            var value = valueNode.Value;

            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    writer.WriteStringValue(value.GetString());
                    break;
                case JsonValueKind.Number:
                    if (value.TryGetInt32(out int intValue))
                    {
                        writer.WriteNumberValue(intValue);
                    }
                    else if (value.TryGetInt64(out long longValue))
                    {
                        writer.WriteNumberValue(longValue);
                    }
                    else if (value.TryGetDouble(out double doubleValue))
                    {
                        writer.WriteNumberValue(doubleValue);
                    }
                    else
                    {
                        // Fallback: write as raw value
                        writer.WriteRawValue(value.GetRawText());
                    }
                    break;
                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonValueKind.Null:
                    writer.WriteNullValue();
                    break;
                default:
                    // For other types, write as raw value
                    writer.WriteRawValue(value.GetRawText());
                    break;
            }
        }

        /// <summary>
        /// Writes a reference node to the JSON writer.
        /// </summary>
        private void WriteRefNode(Utf8JsonWriter writer, RefNode refNode)
        {
            writer.WriteStartObject();
            writer.WriteString("$ref", refNode.ReferencePath);
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes an object node to the JSON writer.
        /// </summary>
        private void WriteObjectNode(Utf8JsonWriter writer, ObjectNode objectNode)
        {
            writer.WriteStartObject();

            foreach (var property in objectNode.Children)
            {
                writer.WritePropertyName(property.Key);
                WriteNode(writer, property.Value);
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes an array node to the JSON writer.
        /// </summary>
        private void WriteArrayNode(Utf8JsonWriter writer, ArrayNode arrayNode)
        {
            writer.WriteStartArray();

            foreach (var item in arrayNode.Items)
            {
                WriteNode(writer, item);
            }

            writer.WriteEndArray();
        }
    }
} 
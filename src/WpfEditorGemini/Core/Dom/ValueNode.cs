using System;
using System.Text.Json;

namespace JsonConfigEditor.Core.Dom
{
    /// <summary>
    /// Represents a JSON primitive value node in the DOM tree.
    /// When updated, the ValueNode instance remains the same, but its Value property references a newly created JsonElement.
    /// (From specification document, Section 2.1)
    /// </summary>
    public class ValueNode : DomNode
    {
        private JsonElement _value;

        /// <summary>
        /// Gets or sets the JSON value of this node.
        /// When set, a new JsonElement is created while the ValueNode instance remains the same.
        /// </summary>
        public JsonElement Value
        {
            get => _value;
            set => _value = value;
        }

        /// <summary>
        /// Initializes a new instance of the ValueNode class.
        /// </summary>
        /// <param name="name">The name of the node</param>
        /// <param name="parent">The parent node</param>
        /// <param name="value">The JSON value</param>
        public ValueNode(string name, DomNode? parent, JsonElement value) : base(name, parent)
        {
            // Clone the JsonElement to avoid disposal issues
            _value = CloneJsonElement(value);
        }

        /// <summary>
        /// Clones a JsonElement to avoid disposal issues with the underlying JsonDocument.
        /// </summary>
        /// <param name="element">The JsonElement to clone</param>
        /// <returns>A cloned JsonElement backed by a new JsonDocument</returns>
        private static JsonElement CloneJsonElement(JsonElement element)
        {
            // Convert to string and parse back to create a new JsonDocument
            var rawText = element.GetRawText();
            return JsonDocument.Parse(rawText).RootElement;
        }

        /// <summary>
        /// Gets a string representation of the value for display purposes.
        /// </summary>
        /// <returns>String representation of the value</returns>
        public string GetDisplayValue()
        {
            return Value.ValueKind switch
            {
                JsonValueKind.String => Value.GetString() ?? "",
                JsonValueKind.Number => Value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => Value.ToString()
            };
        }

        /// <summary>
        /// Updates the value from a string representation, attempting to preserve the original JSON type.
        /// </summary>
        /// <param name="stringValue">The string representation of the new value</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        public bool TryUpdateFromString(string stringValue)
        {
            try
            {
                // Try to preserve the original type if possible
                switch (Value.ValueKind)
                {
                    case JsonValueKind.String:
                        Value = JsonDocument.Parse($"\"{stringValue}\"").RootElement;
                        return true;

                    case JsonValueKind.Number:
                        if (double.TryParse(stringValue, out double numValue))
                        {
                            // Check if it's an integer
                            if (numValue == Math.Floor(numValue) && numValue >= int.MinValue && numValue <= int.MaxValue)
                            {
                                Value = JsonDocument.Parse(((int)numValue).ToString()).RootElement;
                            }
                            else
                            {
                                Value = JsonDocument.Parse(numValue.ToString()).RootElement;
                            }
                            return true;
                        }
                        return false;

                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        if (bool.TryParse(stringValue, out bool boolValue))
                        {
                            Value = JsonDocument.Parse(boolValue.ToString().ToLower()).RootElement;
                            return true;
                        }
                        return false;

                    case JsonValueKind.Null:
                        if (stringValue.Equals("null", StringComparison.OrdinalIgnoreCase))
                        {
                            Value = JsonDocument.Parse("null").RootElement;
                            return true;
                        }
                        // For null values, try to infer the type from the string
                        return TryInferTypeAndUpdate(stringValue);

                    default:
                        return TryInferTypeAndUpdate(stringValue);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Attempts to infer the JSON type from a string and update the value.
        /// </summary>
        /// <param name="stringValue">The string value to parse</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool TryInferTypeAndUpdate(string stringValue)
        {
            try
            {
                // Try parsing as JSON directly
                var doc = JsonDocument.Parse(stringValue);
                Value = doc.RootElement;
                return true;
            }
            catch
            {
                // If direct JSON parsing fails, treat as string
                try
                {
                    Value = JsonDocument.Parse($"\"{stringValue}\"").RootElement;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Returns a string representation of this value node.
        /// </summary>
        public override string ToString()
        {
            return $"ValueNode: {Name} = {GetDisplayValue()} (Path: {Path})";
        }
    }
} 
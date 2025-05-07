using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Schema node representing a string field with optional formatting hints.
    /// Used to influence input validation and editor rendering.
    /// </summary>
    public class StringSchemaNode : ISchemaNode
    {
        /// <summary>
        /// Optional regex pattern that the string must match.
        /// </summary>
        public string? Pattern { get; set; }

        /// <summary>
        /// Optional format name (e.g., "ip", "url", "email") for semantic hints.
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// Optional unit label (e.g., "chars", "lines").
        /// </summary>
        public string? Unit { get; set; }

        public List<IErrorStatusProvider> Validate(JsonElement value, string path)
        {
            var errors = new List<IErrorStatusProvider>();
            if (value.ValueKind != JsonValueKind.String)
            {
                errors.Add(new BasicValidationError(path, "Expected string value."));
                return errors;
            }

            var str = value.GetString();
            if (Pattern != null && !Regex.IsMatch(str!, Pattern))
                errors.Add(new BasicValidationError(path, $"String does not match pattern: {Pattern}"));

            return errors;
        }

        public string GetSchemaType() => "string";

        public string? GetHint()
        {
            if (Format != null) return $"String ({Format})";
            if (Unit != null) return $"String ({Unit})";
            return "String";
        }
    }
}

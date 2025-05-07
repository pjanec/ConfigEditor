using System.Collections.Generic;

namespace ConfigDom
{
    /// <summary>
    /// Schema node representing a boolean field.
    /// May include optional display hints.
    /// </summary>
    public class BooleanSchemaNode : ISchemaNode
    {
        /// <summary>
        /// Optional label for the "true" state.
        /// </summary>
        public string? TrueLabel { get; set; }

        /// <summary>
        /// Optional label for the "false" state.
        /// </summary>
        public string? FalseLabel { get; set; }

        /// <inheritdoc />
        public List<IErrorStatusProvider> Validate(System.Text.Json.JsonElement value, string path)
        {
            var errors = new List<IErrorStatusProvider>();
            if (value.ValueKind != System.Text.Json.JsonValueKind.True &&
                value.ValueKind != System.Text.Json.JsonValueKind.False)
            {
                errors.Add(new BasicValidationError(path, "Expected boolean value."));
            }
            return errors;
        }

        /// <inheritdoc />
        public string GetSchemaType() => "boolean";

        /// <inheritdoc />
        public string? GetHint() => "Boolean value (true/false)";
    }
}

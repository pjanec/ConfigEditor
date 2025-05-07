using System.Collections.Generic;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Schema node representing an integer field with optional range and unit.
    /// Used for validation and UI hinting.
    /// </summary>
    public class IntegerSchemaNode : ISchemaNode
    {
        public int? Min { get; set; }
        public int? Max { get; set; }
        public string? Unit { get; set; }
        public string? Format { get; set; }

        public List<IErrorStatusProvider> Validate(JsonElement value, string path)
        {
            var errors = new List<IErrorStatusProvider>();
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var intValue))
            {
                errors.Add(new BasicValidationError(path, "Expected integer value."));
                return errors;
            }
            if (Min.HasValue && intValue < Min.Value)
                errors.Add(new BasicValidationError(path, $"Value {intValue} is below minimum {Min}"));
            if (Max.HasValue && intValue > Max.Value)
                errors.Add(new BasicValidationError(path, $"Value {intValue} exceeds maximum {Max}"));
            return errors;
        }

        public string GetSchemaType() => "integer";

        public string? GetHint() => Unit != null ? $"Integer ({Unit})" : "Integer";
    }
}

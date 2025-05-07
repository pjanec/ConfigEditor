using System.Collections.Generic;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Schema node that describes an object structure with named properties.
    /// Each property may be required and have its own nested schema.
    /// </summary>
    public class ObjectSchemaNode : ISchemaNode
    {
        /// <summary>
        /// Maps property names to their respective schema node.
        /// </summary>
        public Dictionary<string, ISchemaNode> Properties { get; } = new();

        /// <summary>
        /// Set of property names that are required.
        /// </summary>
        public HashSet<string> Required { get; } = new();

        public List<IErrorStatusProvider> Validate(JsonElement value, string path)
        {
            var errors = new List<IErrorStatusProvider>();

            if (value.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new BasicValidationError(path, "Expected object."));
                return errors;
            }

            foreach (var kvp in Properties)
            {
                string prop = kvp.Key;
                var subSchema = kvp.Value;

                if (value.TryGetProperty(prop, out var subValue))
                {
                    errors.AddRange(subSchema.Validate(subValue, path + "/" + prop));
                }
                else if (Required.Contains(prop))
                {
                    errors.Add(new BasicValidationError(path + "/" + prop, "Missing required property."));
                }
            }

            return errors;
        }

        public string GetSchemaType() => "object";

        public string? GetHint() => "Structured object";
    }
}

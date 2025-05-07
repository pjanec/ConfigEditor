using System.Collections.Generic;

namespace ConfigDom
{
    /// <summary>
    /// Interface to represent a strongly typed schema node.
    /// Real implementations include object, array, primitive schema types.
    /// </summary>
    public interface ISchemaNode
    {
        /// <summary>
        /// Validates the given JsonElement against this schema node.
        /// </summary>
        /// <param name="value">The JSON value to validate.</param>
        /// <param name="path">The DOM path to include in diagnostics.</param>
        /// <returns>List of validation issues (or empty if valid).</returns>
        List<IErrorStatusProvider> Validate(System.Text.Json.JsonElement value, string path);

        /// <summary>
        /// Returns the expected type of this schema node (e.g., object, string).
        /// </summary>
        string GetSchemaType();

        /// <summary>
        /// Optional hint describing the field usage.
        /// </summary>
        string? GetHint();
    }
} 

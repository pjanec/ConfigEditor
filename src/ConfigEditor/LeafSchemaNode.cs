using System;
using System.Collections.Generic;

namespace ConfigDom
{
    /// <summary>
    /// Represents the schema for a leaf node (value node).
    /// Includes optional validation and UI metadata.
    /// </summary>
    public class LeafSchemaNode : ISchemaNode
    {
        public string? Format { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }

        /// <summary>
        /// Optional list of allowed values for this leaf node (enum enforcement).
        /// </summary>
        public List<string>? AllowedValues { get; set; }

        /// <summary>
        /// Optional regular expression that the value must match (string only).
        /// </summary>
        public string? RegexPattern { get; set; }
    }
}

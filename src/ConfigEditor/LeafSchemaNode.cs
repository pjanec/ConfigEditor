using System;
using System.Collections.Generic;

namespace ConfigDom
{
    /// <summary>
    /// Represents the schema for a leaf node (value node).
    /// Includes optional validation and UI metadata.
    /// </summary>
    public class LeafSchemaNode : SchemaNode
    {
        /// <summary>
        /// Optional regular expression that the value must match (string only).
        /// </summary>
        public string? RegexPattern { get; set; }
    }
}

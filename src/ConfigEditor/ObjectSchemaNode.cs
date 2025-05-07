using System.Collections.Generic;
using System;

namespace ConfigDom
{
    /// <summary>
    /// Represents the schema for an object node.
    /// Maps child property names to their respective schema definitions.
    /// </summary>
    public class ObjectSchemaNode : ISchemaNode
    {
        public Dictionary<string, SchemaProperty> Properties { get; init; } = new();
    
        /// <summary>
        /// Optional formatting hint (not commonly used at object level).
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// Minimum value range (not typically used for objects).
        /// </summary>
        public double? Min { get; set; }

        /// <summary>
        /// Maximum value range (not typically used for objects).
        /// </summary>
        public double? Max { get; set; }

    }
}

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
        /// <summary>
        /// Maps property names to child schema nodes.
        /// </summary>
        public Dictionary<string, ISchemaNode> ChildrenByName { get; } = new();

        /// <summary>
        /// Optional description for display or documentation.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Optional formatting hint (not commonly used at object level).
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// Optional display unit (for UI rendering).
        /// </summary>
        public string? Unit { get; set; }

        /// <summary>
        /// Minimum value range (not typically used for objects).
        /// </summary>
        public double? Min { get; set; }

        /// <summary>
        /// Maximum value range (not typically used for objects).
        /// </summary>
        public double? Max { get; set; }

        /// <summary>
        /// Indicates whether this node is required in the config.
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// Optional C# type reference for tooling.
        /// </summary>
        public Type? ClrType { get; set; }
    }
}

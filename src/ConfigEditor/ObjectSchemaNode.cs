using System.Collections.Generic;
using System;

namespace ConfigDom
{
    /// <summary>
    /// Represents the schema for an object node.
    /// Maps child property names to their respective schema definitions.
    /// </summary>
    public class ObjectSchemaNode : SchemaNode
    {
        public Dictionary<string, SchemaProperty> Properties { get; init; } = new();
    }
}

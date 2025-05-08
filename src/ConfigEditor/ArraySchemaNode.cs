using System;

namespace ConfigDom
{
    /// <summary>
    /// Represents the schema for an array node.
    /// Holds the schema of its repeated item elements.
    /// </summary>
    public class ArraySchemaNode : SchemaNode
    {
        /// <summary>
        /// The schema of each array element. Required.
        /// </summary>
        public required SchemaNode ItemSchema { get; init; }
    }
}

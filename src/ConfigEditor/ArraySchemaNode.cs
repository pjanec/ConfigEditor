using System;

namespace ConfigDom
{
    /// <summary>
    /// Represents the schema for an array node.
    /// Holds the schema of its repeated item elements.
    /// </summary>
    public class ArraySchemaNode : ISchemaNode
    {
        public ISchemaNode ItemSchema { get; }

        public ArraySchemaNode(ISchemaNode itemSchema)
        {
            ItemSchema = itemSchema;
        }

        public string? Description { get; set; }
        public string? Unit { get; set; }
        public string? Format { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public bool IsRequired { get; set; } = false;
        public Type? ClrType { get; set; }
    }
}

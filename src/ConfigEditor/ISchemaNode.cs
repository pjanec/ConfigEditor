using System;

namespace ConfigDom
{
    /// <summary>
    /// Common interface implemented by all schema node types.
    /// Used to provide UI and validation metadata for each DOM node.
    /// </summary>
    public interface ISchemaNode
    {
        /// <summary>
        /// Optional description or label for display or tooltip purposes.
        /// </summary>
        string? Description { get; set; }

        /// <summary>
        /// Optional unit of measure (e.g., \"MB\", \"seconds\").
        /// </summary>
        string? Unit { get; set; }

        /// <summary>
        /// Optional formatting hint (e.g., \"hex\", \"ip\", \"currency\").
        /// </summary>
        string? Format { get; set; }

        /// <summary>
        /// Minimum valid numeric value (used by leaf nodes).
        /// </summary>
        double? Min { get; set; }

        /// <summary>
        /// Maximum valid numeric value (used by leaf nodes).
        /// </summary>
        double? Max { get; set; }

        /// <summary>
        /// Indicates whether the node is required.
        /// </summary>
        bool IsRequired { get; set; }

        /// <summary>
        /// Optional CLR type for this field, useful for tooling.
        /// </summary>
        Type? ClrType { get; set; }
    }
}

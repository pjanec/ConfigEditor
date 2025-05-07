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

    }
}

using System;
using System.Collections.Generic;

namespace JsonConfigEditor.Core.Schema
{
    /// <summary>
    /// Defines the type of a schema node (Value, Object, or Array).
    /// (From specification document, Section 2.2)
    /// </summary>
    public enum SchemaNodeType
    {
        Value,
        Object,
        Array
    }

    /// <summary>
    /// Represents a node in the schema tree, derived from C# model classes and their attributes.
    /// This class should be treated as immutable after its initial construction.
    /// (From specification document, Section 2.2)
    /// </summary>
    public class SchemaNode
    {
        /// <summary>
        /// Gets the name of the property or "*" for array items.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the actual C# type for the schema node. Used for registering custom renderers/editors.
        /// </summary>
        public Type ClrType { get; }

        /// <summary>
        /// Gets a value indicating whether this property is required.
        /// Derived from C# non-nullable reference types and value types not explicitly marked as nullable.
        /// </summary>
        public bool IsRequired { get; }

        /// <summary>
        /// Gets a value indicating whether this property is read-only.
        /// Derived from [System.ComponentModel.ReadOnly(true)] attribute on a C# property.
        /// </summary>
        public bool IsReadOnly { get; }

        /// <summary>
        /// Gets the default value for this property.
        /// Derived primarily from C# property initializers or parameterless constructors.
        /// </summary>
        public object? DefaultValue { get; }

        /// <summary>
        /// Gets the minimum value for numeric types.
        /// From [System.ComponentModel.DataAnnotations.RangeAttribute(double min, double max)].
        /// </summary>
        public double? Min { get; }

        /// <summary>
        /// Gets the maximum value for numeric types.
        /// From [System.ComponentModel.DataAnnotations.RangeAttribute(double min, double max)].
        /// </summary>
        public double? Max { get; }

        /// <summary>
        /// Gets the regex pattern for string validation.
        /// From a custom [SchemaRegexPatternAttribute(string pattern)] on a C# property.
        /// </summary>
        public string? RegexPattern { get; }

        /// <summary>
        /// Gets the allowed values for string types or enum members.
        /// For string types, derived from a custom [SchemaAllowedValues("a", "b")] attribute.
        /// For C# enum types, this list is populated with the enum member names.
        /// Comparisons are case-insensitive.
        /// </summary>
        public IReadOnlyList<string>? AllowedValues { get; }

        /// <summary>
        /// Gets a value indicating whether the ClrType is a C# enum marked with [System.FlagsAttribute].
        /// </summary>
        public bool IsEnumFlags { get; }

        /// <summary>
        /// Gets the properties for object types, keyed by property name.
        /// </summary>
        public IReadOnlyDictionary<string, SchemaNode>? Properties { get; }

        /// <summary>
        /// Gets the schema for additional properties in dictionary types (e.g., Dictionary&lt;string, T&gt;).
        /// This holds the schema for T, generated recursively.
        /// </summary>
        public SchemaNode? AdditionalPropertiesSchema { get; }

        /// <summary>
        /// Gets a value indicating whether additional properties are allowed.
        /// True if Properties is null or if backed by a dictionary type allowing arbitrary keys.
        /// </summary>
        public bool AllowAdditionalProperties { get; }

        /// <summary>
        /// Gets the schema for array items.
        /// </summary>
        public SchemaNode? ItemSchema { get; }

        /// <summary>
        /// Gets the mount path for top-level SchemaNode instances derived from [ConfigSchemaAttribute].
        /// Only set for root-level schema nodes.
        /// </summary>
        public string? MountPath { get; }

        /// <summary>
        /// Gets the type of this schema node (Value, Object, or Array).
        /// </summary>
        public SchemaNodeType NodeType
        {
            get
            {
                if (ItemSchema != null)
                    return SchemaNodeType.Array;
                if (Properties != null || AdditionalPropertiesSchema != null)
                    return SchemaNodeType.Object;
                return SchemaNodeType.Value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the SchemaNode class.
        /// </summary>
        /// <param name="name">The name of the property or "*" for array items</param>
        /// <param name="clrType">The actual C# type for the schema node</param>
        /// <param name="isRequired">Whether this property is required</param>
        /// <param name="isReadOnly">Whether this property is read-only</param>
        /// <param name="defaultValue">The default value for this property</param>
        /// <param name="min">The minimum value for numeric types</param>
        /// <param name="max">The maximum value for numeric types</param>
        /// <param name="regexPattern">The regex pattern for string validation</param>
        /// <param name="allowedValues">The allowed values for string types or enum members</param>
        /// <param name="isEnumFlags">Whether the ClrType is a flags enum</param>
        /// <param name="properties">The properties for object types</param>
        /// <param name="additionalPropertiesSchema">The schema for additional properties</param>
        /// <param name="allowAdditionalProperties">Whether additional properties are allowed</param>
        /// <param name="itemSchema">The schema for array items</param>
        /// <param name="mountPath">The mount path for top-level schema nodes</param>
        public SchemaNode(
            string name,
            Type clrType,
            bool isRequired,
            bool isReadOnly,
            object? defaultValue,
            double? min,
            double? max,
            string? regexPattern,
            IReadOnlyList<string>? allowedValues,
            bool isEnumFlags,
            IReadOnlyDictionary<string, SchemaNode>? properties,
            SchemaNode? additionalPropertiesSchema,
            bool allowAdditionalProperties,
            SchemaNode? itemSchema,
            string? mountPath = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
            IsRequired = isRequired;
            IsReadOnly = isReadOnly;
            DefaultValue = defaultValue;
            Min = min;
            Max = max;
            RegexPattern = regexPattern;
            AllowedValues = allowedValues;
            IsEnumFlags = isEnumFlags;
            Properties = properties;
            AdditionalPropertiesSchema = additionalPropertiesSchema;
            AllowAdditionalProperties = allowAdditionalProperties;
            ItemSchema = itemSchema;
            MountPath = mountPath;
        }

        /// <summary>
        /// Returns a string representation of this schema node.
        /// </summary>
        public override string ToString()
        {
            var typeInfo = NodeType switch
            {
                SchemaNodeType.Array => $"Array<{ItemSchema?.ClrType.Name ?? "unknown"}>",
                SchemaNodeType.Object => $"Object ({Properties?.Count ?? 0} properties)",
                _ => ClrType.Name
            };

            var mountInfo = !string.IsNullOrEmpty(MountPath) ? $" (Mount: {MountPath})" : "";
            return $"SchemaNode: {Name} : {typeInfo}{mountInfo}";
        }
    }
} 
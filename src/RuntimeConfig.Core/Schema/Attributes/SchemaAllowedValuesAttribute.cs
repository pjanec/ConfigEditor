using System;

namespace RuntimeConfig.Core.Schema.Attributes
{
    /// <summary>
    /// Attribute to specify allowed values for string properties in schemas.
    /// (From specification document, Section 2.2)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class SchemaAllowedValuesAttribute : Attribute
    {
        /// <summary>
        /// Gets the allowed values for the property.
        /// </summary>
        public string[] AllowedValues { get; }

        /// <summary>
        /// Initializes a new instance of the SchemaAllowedValuesAttribute.
        /// </summary>
        /// <param name="allowedValues">The allowed values for the property</param>
        public SchemaAllowedValuesAttribute(params string[] allowedValues)
        {
            AllowedValues = allowedValues ?? throw new ArgumentNullException(nameof(allowedValues));
        }
    }
} 
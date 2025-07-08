using System;

namespace RuntimeConfig.Core.Schema.Attributes
{
    /// <summary>
    /// Attribute to specify a regex pattern for string property validation in schemas.
    /// (From specification document, Section 2.2)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class SchemaRegexPatternAttribute : Attribute
    {
        /// <summary>
        /// Gets the regex pattern for validation.
        /// </summary>
        public string Pattern { get; }

        /// <summary>
        /// Initializes a new instance of the SchemaRegexPatternAttribute.
        /// </summary>
        /// <param name="pattern">The regex pattern for validation</param>
        public SchemaRegexPatternAttribute(string pattern)
        {
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        }
    }
} 
using System;

namespace JsonConfigEditor.Contracts.Attributes
{
    /// <summary>
    /// Attribute to mark C# classes as schema definitions for JSON configuration sections.
    /// The mount path specifies where in the DOM tree this schema applies.
    /// (From specification document, Section 2.2)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ConfigSchemaAttribute : Attribute
    {
        /// <summary>
        /// Gets the mount path in the DOM tree where this schema applies.
        /// Uses forward slash as separator (e.g., "section/subsection").
        /// </summary>
        public string MountPath { get; }

        /// <summary>
        /// Gets the type of the schema class.
        /// </summary>
        public Type SchemaClassType { get; }

        /// <summary>
        /// Initializes a new instance of the ConfigSchemaAttribute.
        /// </summary>
        /// <param name="mountPath">The mount path in the DOM tree</param>
        /// <param name="schemaClassType">The type of the schema class</param>
        public ConfigSchemaAttribute(string mountPath, Type schemaClassType)
        {
            MountPath = mountPath ?? throw new ArgumentNullException(nameof(mountPath));
            SchemaClassType = schemaClassType ?? throw new ArgumentNullException(nameof(schemaClassType));
        }
    }
} 
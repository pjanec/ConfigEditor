using System;

namespace JsonConfigEditor.Contracts.Attributes
{
    /// <summary>
    /// Marks a class as a custom value renderer for a specific CLR Type.
    /// The decorated class must implement IValueRenderer.
    /// (From specification document, Section 2.3.2 & Clarification 7)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class ValueRendererAttribute : Attribute
    {
        /// <summary>
        /// The System.Type for which this renderer should be used.
        /// </summary>
        public Type TargetClrType { get; }

        public ValueRendererAttribute(Type targetClrType)
        {
            TargetClrType = targetClrType;
        }
    }
} 
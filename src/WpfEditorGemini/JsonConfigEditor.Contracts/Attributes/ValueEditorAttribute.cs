using System;

namespace JsonConfigEditor.Contracts.Attributes
{
    /// <summary>
    /// Marks a class as a custom value editor for a specific CLR Type.
    /// The decorated class must implement IValueEditor.
    /// (From specification document, Section 2.4 & Clarification 7)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class ValueEditorAttribute : Attribute
    {
        /// <summary>
        /// The System.Type for which this editor should be used.
        /// </summary>
        public Type TargetClrType { get; }

        /// <summary>
        /// Specifies if this custom editor requires a modal window for editing.
        /// (From specification document, Section 2.4)
        /// </summary>
        public bool RequiresModal { get; }

        public ValueEditorAttribute(Type targetClrType, bool requiresModal = false)
        {
            TargetClrType = targetClrType;
            RequiresModal = requiresModal;
        }
    }
} 
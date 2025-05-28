using System;

namespace JsonConfigEditor.Contracts.Attributes
{
    /// <summary>
    /// Marks a class as a custom tooltip provider for a specific CLR Type.
    /// The decorated class must implement ITooltipProvider.
    /// (From specification document, Section 2.3.4 & Clarification 7)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class TooltipProviderAttribute : Attribute
    {
        /// <summary>
        /// The System.Type for which this tooltip provider should be used.
        /// </summary>
        public Type TargetClrType { get; }

        public TooltipProviderAttribute(Type targetClrType)
        {
            TargetClrType = targetClrType;
        }
    }
} 
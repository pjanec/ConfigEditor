using RuntimeConfig.Core.Dom;

namespace JsonConfigEditor.Core.Validation
{
    /// <summary>
    /// Represents the severity level of a validation issue.
    /// </summary>
    public enum ValidationSeverity
    {
        Error,
        Warning,
        Info
    }

    /// <summary>
    /// Represents a validation issue found in the DOM tree.
    /// (From specification document, Section 2.4.1)
    /// </summary>
    public class ValidationIssue
    {
        /// <summary>
        /// Gets the DOM node where the issue was found.
        /// </summary>
        public DomNode Node { get; }

        /// <summary>
        /// Gets the severity level of the issue.
        /// </summary>
        public ValidationSeverity Severity { get; }

        /// <summary>
        /// Gets the validation error message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the validation rule that was violated.
        /// </summary>
        public string RuleType { get; }

        /// <summary>
        /// Gets additional details about the validation issue.
        /// </summary>
        public string? Details { get; }

        /// <summary>
        /// Initializes a new instance of the ValidationIssue class.
        /// </summary>
        /// <param name="node">The DOM node where the issue was found</param>
        /// <param name="severity">The severity level of the issue</param>
        /// <param name="message">The validation error message</param>
        /// <param name="ruleType">The validation rule that was violated</param>
        /// <param name="details">Additional details about the validation issue</param>
        public ValidationIssue(DomNode node, ValidationSeverity severity, string message, string ruleType, string? details = null)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            Severity = severity;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            RuleType = ruleType ?? throw new ArgumentNullException(nameof(ruleType));
            Details = details;
        }

        /// <summary>
        /// Returns a string representation of this validation issue.
        /// </summary>
        public override string ToString()
        {
            var detailsInfo = !string.IsNullOrEmpty(Details) ? $" ({Details})" : "";
            return $"{Severity}: {Message} at {Node.Path} [{RuleType}]{detailsInfo}";
        }
    }
} 
using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.Core.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JsonConfigEditor.Core.Validation
{
    /// <summary>
    /// Service for validating DOM nodes against their schemas.
    /// (From specification document, Section 2.4.1)
    /// </summary>
    public class ValidationService
    {
        /// <summary>
        /// Validates a DOM node against its schema.
        /// </summary>
        /// <param name="domNode">The DOM node to validate</param>
        /// <param name="schemaNode">The schema to validate against (can be null for unschematized nodes)</param>
        /// <returns>A list of validation issues found</returns>
        public List<ValidationIssue> ValidateNode(DomNode domNode, SchemaNode? schemaNode)
        {
            var issues = new List<ValidationIssue>();

            if (domNode == null)
                throw new ArgumentNullException(nameof(domNode));

            // If there's no schema, only basic validation is performed
            if (schemaNode == null)
            {
                ValidateUnschematizedNode(domNode, issues);
                return issues;
            }

            // Validate based on node type
            switch (domNode)
            {
                case ValueNode valueNode:
                    ValidateValueNode(valueNode, schemaNode, issues);
                    break;
                case RefNode refNode:
                    ValidateRefNode(refNode, schemaNode, issues);
                    break;
                case ObjectNode objectNode:
                    ValidateObjectNode(objectNode, schemaNode, issues);
                    break;
                case ArrayNode arrayNode:
                    ValidateArrayNode(arrayNode, schemaNode, issues);
                    break;
            }

            return issues;
        }

        /// <summary>
        /// Validates an entire DOM tree against schemas.
        /// </summary>
        /// <param name="rootNode">The root DOM node</param>
        /// <param name="domToSchemaMap">Mapping from DOM nodes to their schemas</param>
        /// <returns>A dictionary mapping DOM nodes to their validation issues</returns>
        public Dictionary<DomNode, List<ValidationIssue>> ValidateTree(DomNode rootNode, Dictionary<DomNode, SchemaNode?> domToSchemaMap)
        {
            var allIssues = new Dictionary<DomNode, List<ValidationIssue>>();
            ValidateTreeRecursive(rootNode, domToSchemaMap, allIssues);
            return allIssues;
        }

        /// <summary>
        /// Recursively validates a DOM tree.
        /// </summary>
        private void ValidateTreeRecursive(DomNode node, Dictionary<DomNode, SchemaNode?> domToSchemaMap, Dictionary<DomNode, List<ValidationIssue>> allIssues)
        {
            // Get schema for this node
            domToSchemaMap.TryGetValue(node, out var schemaNode);

            // Validate this node
            var issues = ValidateNode(node, schemaNode);
            if (issues.Any())
            {
                allIssues[node] = issues;
            }

            // Recursively validate children
            switch (node)
            {
                case ObjectNode objectNode:
                    foreach (var child in objectNode.GetChildren())
                    {
                        ValidateTreeRecursive(child, domToSchemaMap, allIssues);
                    }
                    break;
                case ArrayNode arrayNode:
                    foreach (var item in arrayNode.GetItems())
                    {
                        ValidateTreeRecursive(item, domToSchemaMap, allIssues);
                    }
                    break;
            }
        }

        /// <summary>
        /// Validates an unschematized node (basic validation only).
        /// </summary>
        private void ValidateUnschematizedNode(DomNode domNode, List<ValidationIssue> issues)
        {
            // For unschematized nodes, we only perform basic JSON validity checks
            if (domNode is ValueNode valueNode)
            {
                // Check if the JSON value is valid
                try
                {
                    var _ = valueNode.Value.ToString();
                }
                catch (Exception ex)
                {
                    issues.Add(new ValidationIssue(domNode, ValidationSeverity.Error, 
                        "Invalid JSON value", "JsonValidity", ex.Message));
                }
            }
            else if (domNode is RefNode refNode)
            {
                // Validate reference path syntax
                if (!refNode.IsPathSyntaxValid())
                {
                    issues.Add(new ValidationIssue(domNode, ValidationSeverity.Error,
                        "Invalid reference path syntax", "RefPathSyntax"));
                }
            }
        }

        /// <summary>
        /// Validates a value node against its schema.
        /// </summary>
        private void ValidateValueNode(ValueNode valueNode, SchemaNode schemaNode, List<ValidationIssue> issues)
        {
            var value = valueNode.Value;

            // Check if null is allowed
            if (value.ValueKind == JsonValueKind.Null)
            {
                if (schemaNode.IsRequired)
                {
                    issues.Add(new ValidationIssue(valueNode, ValidationSeverity.Error,
                        "Required property cannot be null", "RequiredProperty"));
                }
                return; // No further validation for null values
            }

            // Type compatibility check
            if (!IsValueCompatibleWithSchema(value, schemaNode))
            {
                issues.Add(new ValidationIssue(valueNode, ValidationSeverity.Error,
                    $"Value type {value.ValueKind} is not compatible with expected type {schemaNode.ClrType.Name}",
                    "TypeMismatch"));
                return; // No further validation if type is incompatible
            }

            // Numeric range validation
            if (IsNumericType(schemaNode.ClrType) && value.ValueKind == JsonValueKind.Number)
            {
                var numValue = value.GetDouble();
                
                if (schemaNode.Min.HasValue && numValue < schemaNode.Min.Value)
                {
                    issues.Add(new ValidationIssue(valueNode, ValidationSeverity.Error,
                        $"Value {numValue} is below minimum {schemaNode.Min.Value}", "MinValue"));
                }

                if (schemaNode.Max.HasValue && numValue > schemaNode.Max.Value)
                {
                    issues.Add(new ValidationIssue(valueNode, ValidationSeverity.Error,
                        $"Value {numValue} is above maximum {schemaNode.Max.Value}", "MaxValue"));
                }
            }

            // String validation
            if (schemaNode.ClrType == typeof(string) && value.ValueKind == JsonValueKind.String)
            {
                var stringValue = value.GetString() ?? "";

                // Regex pattern validation
                if (!string.IsNullOrEmpty(schemaNode.RegexPattern))
                {
                    try
                    {
                        if (!Regex.IsMatch(stringValue, schemaNode.RegexPattern))
                        {
                            issues.Add(new ValidationIssue(valueNode, ValidationSeverity.Error,
                                $"Value '{stringValue}' does not match pattern '{schemaNode.RegexPattern}'",
                                "RegexPattern"));
                        }
                    }
                    catch (Exception ex)
                    {
                        issues.Add(new ValidationIssue(valueNode, ValidationSeverity.Warning,
                            $"Invalid regex pattern '{schemaNode.RegexPattern}': {ex.Message}",
                            "InvalidRegex"));
                    }
                }

                // Allowed values validation
                if (schemaNode.AllowedValues != null && schemaNode.AllowedValues.Any())
                {
                    if (!schemaNode.AllowedValues.Any(allowed => 
                        string.Equals(allowed, stringValue, StringComparison.OrdinalIgnoreCase)))
                    {
                        issues.Add(new ValidationIssue(valueNode, ValidationSeverity.Error,
                            $"Value '{stringValue}' is not in the list of allowed values: {string.Join(", ", schemaNode.AllowedValues)}",
                            "AllowedValues"));
                    }
                }
            }

            // Enum validation
            if (schemaNode.ClrType.IsEnum && value.ValueKind == JsonValueKind.String)
            {
                var stringValue = value.GetString() ?? "";
                if (!Enum.GetNames(schemaNode.ClrType).Any(name => 
                    string.Equals(name, stringValue, StringComparison.OrdinalIgnoreCase)))
                {
                    issues.Add(new ValidationIssue(valueNode, ValidationSeverity.Error,
                        $"Value '{stringValue}' is not a valid enum value for {schemaNode.ClrType.Name}",
                        "EnumValue"));
                }
            }
        }

        /// <summary>
        /// Validates a reference node against its schema.
        /// </summary>
        private void ValidateRefNode(RefNode refNode, SchemaNode schemaNode, List<ValidationIssue> issues)
        {
            // Validate reference path syntax
            if (!refNode.IsPathSyntaxValid())
            {
                issues.Add(new ValidationIssue(refNode, ValidationSeverity.Error,
                    "Invalid reference path syntax", "RefPathSyntax"));
            }

            // Note: We don't validate the target of the reference here as it might be external
            // or the target validation should be handled separately
        }

        /// <summary>
        /// Validates an object node against its schema.
        /// </summary>
        private void ValidateObjectNode(ObjectNode objectNode, SchemaNode schemaNode, List<ValidationIssue> issues)
        {
            if (schemaNode.NodeType != SchemaNodeType.Object)
            {
                issues.Add(new ValidationIssue(objectNode, ValidationSeverity.Error,
                    "Schema expects non-object type for object node", "TypeMismatch"));
                return;
            }

            // Check required properties
            if (schemaNode.Properties != null)
            {
                foreach (var requiredProperty in schemaNode.Properties.Where(p => p.Value.IsRequired))
                {
                    if (!objectNode.HasProperty(requiredProperty.Key))
                    {
                        issues.Add(new ValidationIssue(objectNode, ValidationSeverity.Error,
                            $"Required property '{requiredProperty.Key}' is missing", "MissingProperty"));
                    }
                }

                // Check for unexpected properties
                if (!schemaNode.AllowAdditionalProperties)
                {
                    foreach (var propertyName in objectNode.GetPropertyNames())
                    {
                        if (!schemaNode.Properties.ContainsKey(propertyName))
                        {
                            issues.Add(new ValidationIssue(objectNode, ValidationSeverity.Error,
                                $"Unexpected property '{propertyName}' found", "UnexpectedProperty"));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Validates an array node against its schema.
        /// </summary>
        private void ValidateArrayNode(ArrayNode arrayNode, SchemaNode schemaNode, List<ValidationIssue> issues)
        {
            if (schemaNode.NodeType != SchemaNodeType.Array)
            {
                issues.Add(new ValidationIssue(arrayNode, ValidationSeverity.Error,
                    "Schema expects non-array type for array node", "TypeMismatch"));
                return;
            }

            // Array-specific validation could be added here (e.g., min/max items)
        }

        /// <summary>
        /// Checks if a JSON value is compatible with a schema type.
        /// </summary>
        private bool IsValueCompatibleWithSchema(JsonElement value, SchemaNode schemaNode)
        {
            var clrType = schemaNode.ClrType;

            return value.ValueKind switch
            {
                JsonValueKind.String => clrType == typeof(string) || clrType.IsEnum,
                JsonValueKind.Number => IsNumericType(clrType),
                JsonValueKind.True or JsonValueKind.False => clrType == typeof(bool),
                JsonValueKind.Null => !schemaNode.IsRequired,
                _ => false
            };
        }

        /// <summary>
        /// Checks if a type is numeric.
        /// </summary>
        private bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(float) || 
                   type == typeof(double) || type == typeof(decimal) || type == typeof(byte) ||
                   type == typeof(short) || type == typeof(uint) || type == typeof(ulong) ||
                   type == typeof(ushort) || type == typeof(sbyte);
        }
    }
} 
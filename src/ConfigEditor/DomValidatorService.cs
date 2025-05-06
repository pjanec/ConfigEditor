using System;
using System.Collections.Generic;
using ConfigEditor.Dom;
using ConfigEditor.Schema;

namespace ConfigEditor.Validation
{
    public class DomValidatorService
    {
        public List<IErrorStatusProvider> ValidateTree(DomNode root)
        {
            var errors = new List<IErrorStatusProvider>();
            ValidateRecursive(root, errors);
            return errors;
        }

        private void ValidateRecursive(DomNode node, List<IErrorStatusProvider> errors)
        {
            if (node.SchemaNode != null)
            {
                var issues = node.SchemaNode switch
                {
                    IntegerSchema intSchema => ValidateInteger(node, intSchema),
                    StringSchema strSchema => ValidateString(node, strSchema),
                    EnumSchema enumSchema => ValidateEnum(node, enumSchema),
                    _ => null
                };

                if (issues != null)
                    errors.AddRange(issues);
            }

            foreach (var (_, child) in node.GetChildren())
                ValidateRecursive(child, errors);
        }

        private IEnumerable<IErrorStatusProvider>? ValidateInteger(DomNode node, IntegerSchema schema)
        {
            if (node.GetValue() is not int val)
                return new[] { new ValidationError(node, "Expected integer") };
            if (schema.Min.HasValue && val < schema.Min.Value)
                return new[] { new ValidationError(node, $"Value {val} < minimum {schema.Min}") };
            if (schema.Max.HasValue && val > schema.Max.Value)
                return new[] { new ValidationError(node, $"Value {val} > maximum {schema.Max}") };
            return null;
        }

        private IEnumerable<IErrorStatusProvider>? ValidateString(DomNode node, StringSchema schema)
        {
            if (node.GetValue() is not string s)
                return new[] { new ValidationError(node, "Expected string") };
            return null;
        }

        private IEnumerable<IErrorStatusProvider>? ValidateEnum(DomNode node, EnumSchema schema)
        {
            if (node.GetValue() is not string val || !schema.AllowedValues.Contains(val))
                return new[] { new ValidationError(node, $"Value '{val}' not in enum") };
            return null;
        }
    }

    public interface IErrorStatusProvider
    {
        string Path { get; }
        string Message { get; }
        DomNode Node { get; }
    }

    public class ValidationError : IErrorStatusProvider
    {
        public string Path { get; }
        public string Message { get; }
        public DomNode Node { get; }

        public ValidationError(DomNode node, string message)
        {
            Node = node;
            Path = node.Path;
            Message = message;
        }

        public override string ToString() => $"[ValidationError] {Path}: {Message}";
    }
}

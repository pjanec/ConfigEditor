using System;
using System.Collections.Generic;

namespace ConfigEditor.Schema
{
    public interface ISchemaNode
    {
        string Name { get; }
        string? Description { get; }
        Dictionary<string, ISchemaNode> Flatten(string basePath = "");
    }

    public class ObjectSchema : ISchemaNode
    {
        public string Name { get; set; } = "object";
        public string? Description { get; set; }
        public Dictionary<string, ISchemaNode> Properties { get; set; } = new();

        public Dictionary<string, ISchemaNode> Flatten(string basePath = "")
        {
            var result = new Dictionary<string, ISchemaNode>();
            foreach (var (key, child) in Properties)
            {
                var path = string.IsNullOrEmpty(basePath) ? key : $"{basePath}/{key}";
                result[path] = child;
                foreach (var sub in child.Flatten(path))
                    result[sub.Key] = sub.Value;
            }
            return result;
        }
    }

    public class IntegerSchema : ISchemaNode
    {
        public string Name { get; set; } = "int";
        public string? Description { get; set; }
        public int? Min { get; set; }
        public int? Max { get; set; }

        public Dictionary<string, ISchemaNode> Flatten(string basePath = "") => new() { [basePath] = this };
    }

    public class StringSchema : ISchemaNode
    {
        public string Name { get; set; } = "string";
        public string? Description { get; set; }
        public string? Format { get; set; }

        public Dictionary<string, ISchemaNode> Flatten(string basePath = "") => new() { [basePath] = this };
    }

    public class BooleanSchema : ISchemaNode
    {
        public string Name { get; set; } = "bool";
        public string? Description { get; set; }

        public Dictionary<string, ISchemaNode> Flatten(string basePath = "") => new() { [basePath] = this };
    }

    public class EnumSchema : ISchemaNode
    {
        public string Name { get; set; } = "enum";
        public string? Description { get; set; }
        public List<string> AllowedValues { get; set; } = new();

        public Dictionary<string, ISchemaNode> Flatten(string basePath = "") => new() { [basePath] = this };
    }
}

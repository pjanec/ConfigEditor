using System;
using System.Collections.Generic;
using System.Text.Json;
using ConfigEditor.Dom;

namespace ConfigEditor.Util
{
    public class JsonMergeService
    {
        public ObjectNode MergeFilesToDom(IEnumerable<Json5SourceFile> sourceFiles)
        {
            var mergedRoot = new ObjectNode();

            foreach (var file in sourceFiles)
            {
                using var doc = JsonDocument.Parse(file.Text);
                var fragment = ParseJsonElement(doc.RootElement, file.FilePath);
                mergedRoot = DeepMerge(mergedRoot, fragment as ObjectNode ?? new ObjectNode()) as ObjectNode;
            }

            return mergedRoot;
        }

        public static ObjectNode ParseFileToDom(Json5SourceFile file)
        {
            using var doc = JsonDocument.Parse(file.Text);
            return ParseJsonElement(doc.RootElement, file.FilePath) as ObjectNode ?? new ObjectNode();
        }

        public static DomNode ParseJsonElement(JsonElement element, string sourceFile, int? lineHint = null)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var objNode = new ObjectNode { SourceFile = sourceFile, SourceLine = lineHint };
                    foreach (var prop in element.EnumerateObject())
                    {
                        var child = ParseJsonElement(prop.Value, sourceFile);
                        objNode.Add(prop.Name, child);
                    }
                    return objNode;
                case JsonValueKind.Array:
                    var arrNode = new ArrayNode { SourceFile = sourceFile, SourceLine = lineHint };
                    foreach (var item in element.EnumerateArray())
                        arrNode.Add(ParseJsonElement(item, sourceFile));
                    return arrNode;
                case JsonValueKind.String:
                    return new LeafNode(element.GetString()) { SourceFile = sourceFile, SourceLine = lineHint };
                case JsonValueKind.Number:
                    return new LeafNode(element.GetDecimal()) { SourceFile = sourceFile, SourceLine = lineHint };
                case JsonValueKind.True:
                    return new LeafNode(true) { SourceFile = sourceFile, SourceLine = lineHint };
                case JsonValueKind.False:
                    return new LeafNode(false) { SourceFile = sourceFile, SourceLine = lineHint };
                case JsonValueKind.Null:
                    return new LeafNode(null) { SourceFile = sourceFile, SourceLine = lineHint };
                default:
                    throw new InvalidOperationException($"Unsupported JSON value kind: {element.ValueKind}");
            }
        }

        private static DomNode DeepMerge(DomNode baseNode, DomNode overrideNode)
        {
            if (baseNode is ObjectNode baseObj && overrideNode is ObjectNode overrideObj)
            {
                foreach (var (key, value) in overrideObj.GetChildren())
                {
                    if (baseObj.GetChild(key!) is DomNode existingChild)
                    {
                        baseObj.Add(key!, DeepMerge(existingChild, value));
                    }
                    else
                    {
                        baseObj.Add(key!, value);
                    }
                }
                return baseObj;
            }

            // Arrays and primitives: full replace
            return overrideNode;
        }
    }
}

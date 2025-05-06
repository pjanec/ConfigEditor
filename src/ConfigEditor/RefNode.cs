using System;
using System.Text.Json;

namespace ConfigEditor.Dom
{
    public class RefNode : DomNode
    {
        public string RefPath { get; set; }

        // Resolved target after reference resolution
        public DomNode? ResolvedTarget { get; internal set; }

        // Optional preview of the resolved value
        public object? ResolvedPreviewValue { get; internal set; }

        public RefNode(string refPath)
        {
            RefPath = refPath;
        }

        public override JsonValueKind ValueKind => JsonValueKind.String;

        public override object? GetValue() => RefPath;

        public override void SetValue(object? value)
        {
            if (value is not string str)
                throw new ArgumentException("$ref value must be a string");

            if (RefPath != str)
            {
                RefPath = str;
                ResolvedTarget = null;
                ResolvedPreviewValue = null;
                MarkDirty();
            }
        }

        public override string ToString() => $"RefNode -> {RefPath}";
    }
}

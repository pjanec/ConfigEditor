using System;
using System.Collections.Generic;
using System.Text.Json;
using ConfigEditor.Schema;

namespace ConfigEditor.Dom
{
    public abstract class DomNode
    {
        public string Path { get; internal set; } = string.Empty;
        public DomNode? Parent { get; internal set; }

        // Optional original source location for traceability
        public string? SourceFile { get; internal set; }
        public int? SourceLine { get; internal set; }

        // Optional schema node attached to this DOM node
        public ISchemaNode? SchemaNode { get; set; }

        // Change tracking
        public bool IsDirty { get; internal set; }

        public abstract JsonValueKind ValueKind { get; }

        // Navigation helpers
        public virtual DomNode? GetChild(string key) => null;
        public virtual DomNode? GetChild(int index) => null;
        public virtual IEnumerable<(string? key, DomNode node)> GetChildren() => Array.Empty<(string?, DomNode)>();

        // For Leaf and RefNodes: value access
        public virtual object? GetValue() => null;
        public virtual void SetValue(object? value) => throw new InvalidOperationException("Not a leaf node");

        public void MarkDirty()
        {
            IsDirty = true;
            Parent?.MarkDirty();
        }

        // Debugging
        public override string ToString() => $"{GetType().Name} @ {Path}";
    }
} 

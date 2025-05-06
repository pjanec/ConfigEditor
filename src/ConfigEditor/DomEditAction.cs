using System;

namespace ConfigEditor.Dom
{
    public class DomEditAction
    {
        public string Path { get; }
        public object? OldValue { get; }
        public object? NewValue { get; }

        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public DomEditAction(string path, object? oldValue, object? newValue)
        {
            Path = path;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public void Apply(DomNode node)
        {
            if (node is LeafNode leaf)
                leaf.SetValue(NewValue);
            else
                throw new InvalidOperationException("EditAction can only be applied to LeafNode");
        }

        public void Undo(DomNode node)
        {
            if (node is LeafNode leaf)
                leaf.SetValue(OldValue);
            else
                throw new InvalidOperationException("Undo can only be applied to LeafNode");
        }

        public override string ToString() => $"EditAction[{Path}]: {OldValue} => {NewValue}";
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ConfigEditor.Dom
{
    public class ObjectNode : DomNode
    {
        private readonly Dictionary<string, DomNode> _children = new(StringComparer.Ordinal);

        public override JsonValueKind ValueKind => JsonValueKind.Object;

        public void Add(string key, DomNode node)
        {
            _children[key] = node;
            node.Parent = this;
            node.Path = Path.Length == 0 ? key : $"{Path}/{key}";
        }

        public override DomNode? GetChild(string key)
        {
            _children.TryGetValue(key, out var child);
            return child;
        }

        public override IEnumerable<(string? key, DomNode node)> GetChildren()
        {
            foreach (var kvp in _children)
                yield return (kvp.Key, kvp.Value);
        }

        public bool Remove(string key)
        {
            if (_children.Remove(key))
            {
                MarkDirty();
                return true;
            }
            return false;
        }

        public bool ContainsKey(string key) => _children.ContainsKey(key);

        public IReadOnlyDictionary<string, DomNode> Children => _children;
    }
}

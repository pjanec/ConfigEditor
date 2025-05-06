using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ConfigEditor.Dom
{
    public class ArrayNode : DomNode
    {
        private readonly List<DomNode> _items = new();

        public override JsonValueKind ValueKind => JsonValueKind.Array;

        public void Add(DomNode item)
        {
            _items.Add(item);
            UpdateItemMetadata(_items.Count - 1);
        }

        public void Insert(int index, DomNode item)
        {
            _items.Insert(index, item);
            UpdateAllMetadata();
        }

        public bool RemoveAt(int index)
        {
            if (index >= 0 && index < _items.Count)
            {
                _items.RemoveAt(index);
                UpdateAllMetadata();
                MarkDirty();
                return true;
            }
            return false;
        }

        public override DomNode? GetChild(int index)
        {
            return index >= 0 && index < _items.Count ? _items[index] : null;
        }

        public override IEnumerable<(string? key, DomNode node)> GetChildren()
        {
            for (int i = 0; i < _items.Count; i++)
                yield return (i.ToString(), _items[i]);
        }

        public IReadOnlyList<DomNode> Items => _items;

        private void UpdateItemMetadata(int index)
        {
            var item = _items[index];
            item.Parent = this;
            item.Path = Path.Length == 0 ? index.ToString() : $"{Path}/{index}";
        }

        private void UpdateAllMetadata()
        {
            for (int i = 0; i < _items.Count; i++)
                UpdateItemMetadata(i);
        }
    }
}

using System;
using System.Collections.Generic;
using ConfigEditor.Dom;
using ConfigEditor.Schema;

namespace ConfigEditor.ViewModel
{
    public class DomNodeViewModel
    {
        public DomNode Node { get; }

        public DomNodeViewModel? Parent { get; }
        private readonly List<DomNodeViewModel> _children = new();

        public string Path => Node.Path;
        public bool IsDirty => Node.IsDirty;
        public ISchemaNode? Schema => Node.SchemaNode;

        public IReadOnlyList<DomNodeViewModel> Children => _children;

        public DomNodeViewModel(DomNode node, DomNodeViewModel? parent = null)
        {
            Node = node;
            Parent = parent;
            BuildChildren();
        }

        private void BuildChildren()
        {
            foreach (var (key, child) in Node.GetChildren())
            {
                var childVm = child is RefNode
                    ? new RefNodeViewModel((RefNode)child, this)
                    : new DomNodeViewModel(child, this);
                _children.Add(childVm);
            }
        }

        public DomNodeViewModel? FindChildViewModelByKey(string key)
        {
            foreach (var child in _children)
            {
                var parts = child.Path.Split('/');
                if (parts[^1] == key)
                    return child;
            }
            return null;
        }
    }
}

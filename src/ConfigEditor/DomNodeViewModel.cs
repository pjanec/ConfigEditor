using System.Collections.Generic;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// ViewModel wrapper for a single DomNode.
    /// Supports binding, dirty tracking, and edit operations.
    /// </summary>
    public class DomNodeViewModel
    {
        public DomNode Node { get; }
        public string Path { get; }
        public DomNodeViewModel? Parent { get; }
        public List<DomNodeViewModel> Children { get; } = new();

        public bool IsDirty { get; private set; }
        public bool IsEditable { get; set; } = true;

        private readonly DomEditHistory _history;

        public DomNodeViewModel(DomNode node, string path, DomNodeViewModel? parent = null, DomEditHistory? history = null)
        {
            Node = node;
            Path = path;
            Parent = parent;
            _history = history ?? new DomEditHistory();
            RebuildChildren();
        }

        private void RebuildChildren()
        {
            if (Node is ObjectNode obj)
            {
                foreach (var child in obj.Children.Values)
                {
                    Children.Add(new DomNodeViewModel(child, Path + "/" + child.Name, this, _history));
                }
            }
            else if (Node is ArrayNode arr)
            {
                for (int i = 0; i < arr.Items.Count; i++)
                {
                    var item = arr.Items[i];
                    Children.Add(new DomNodeViewModel(item, Path + "/" + i, this, _history));
                }
            }
        }

        public void ApplyEdit(JsonElement newValue)
        {
            if (!IsEditable) return;

            var oldValue = ExportJsonSubtree.Get(Node, Path)??new();
            var edit = new DomEditAction(Path, newValue, oldValue);
            _history.Apply(edit);
            edit.Apply(Node);
            MarkDirty();
        }

        public void MarkDirty() => IsDirty = true;

        public JsonElement GetEffectiveValue() => Node.ExportJson();

        public JsonElement CurrentValue => Node.ExportJson();
    }
}

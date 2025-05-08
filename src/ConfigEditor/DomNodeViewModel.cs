using System.Collections.Generic;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// ViewModel wrapper for a single DomNode.
    /// Supports binding, dirty tracking, edit operations, schema metadata, and cascade origin tracking.
    /// Also supports ref-node resolution preview and navigation.
    /// </summary>
    public class DomNodeViewModel
    {
        public DomNode Node { get; }
        public string Path { get; }
        public DomNodeViewModel? Parent { get; }
        public List<DomNodeViewModel> Children { get; } = new();

        public bool IsDirty { get; private set; }
        public bool IsEditable { get; set; } = true;

        public SchemaNode? Schema { get; set; }

        /// <summary>
        /// Optional index indicating at which cascade level the value is defined (0 = base, higher = more specific).
        /// Null means unknown or not from a cascade.
        /// </summary>
        public int? CascadeLevel { get; set; }

        /// <summary>
        /// Optional source file name that defined this value (useful for editor diagnostics).
        /// </summary>
        public string? SourceFile { get; set; }

        /// <summary>
        /// Human-readable name of the cascade level (e.g. "Base", "Site", "Local").
        /// </summary>
        public string? CascadeLevelName { get; set; }

        /// <summary>
        /// Indicates if this value overrides a value from a lower cascade level.
        /// </summary>
        public bool IsOverride { get; set; }

        /// <summary>
        /// If this node is a reference, points to the resolved target node.
        /// </summary>
        public DomNode? ResolvedTargetNode { get; set; }

        /// <summary>
        /// Optional preview of the resolved target value, for UI hover or display.
        /// </summary>
        public string? ResolvedPreviewValue { get; set; }

        private readonly DomEditHistory _history;
        private readonly IMountedDomEditorContext? _context;

        public DomNodeViewModel(DomNode node, string path, DomNodeViewModel? parent = null, DomEditHistory? history = null, IMountedDomEditorContext? context = null)
        {
            Node = node;
            Path = path;
            Parent = parent;
            _history = history ?? new DomEditHistory();
            _context = context;
            RebuildChildren();
            UpdateOriginInfo();
        }

        private void UpdateOriginInfo()
        {
            if (_context is Json5CascadeEditorContext cascadeContext)
            {
                CascadeLevel = cascadeContext.GetLevelIndex(Path);
                CascadeLevelName = cascadeContext.GetLevelName(Path);
                if (cascadeContext.TryGetSourceFile(Path, out var file))
                {
                    SourceFile = file?.FilePath;
                }

                // Check if this is an override by comparing with parent's level
                if (Parent != null && Parent.CascadeLevel.HasValue && CascadeLevel.HasValue)
                {
                    IsOverride = CascadeLevel.Value > Parent.CascadeLevel.Value;
                }
            }
        }

        private void RebuildChildren()
        {
            Children.Clear();
            if (Node is ObjectNode obj)
            {
                foreach (var child in obj.Children.Values)
                {
                    Children.Add(new DomNodeViewModel(child, Path + "/" + child.Name, this, _history, _context));
                }
            }
            else if (Node is ArrayNode arr)
            {
                for (int i = 0; i < arr.Items.Count; i++)
                {
                    var item = arr.Items[i];
                    Children.Add(new DomNodeViewModel(item, Path + "/" + i, this, _history, _context));
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
            UpdateOriginInfo();
        }

        public void MarkDirty() => IsDirty = true;

        public JsonElement GetEffectiveValue() => Node.ExportJson();

        public JsonElement CurrentValue => Node.ExportJson();

        /// <summary>
        /// Traverses up the viewmodel tree and searches recursively for the viewmodel that wraps the resolved target node.
        /// </summary>
        public DomNodeViewModel? GotoResolvedTarget()
        {
            if (ResolvedTargetNode == null) return null;

            DomNodeViewModel? root = this;
            while (root.Parent != null)
                root = root.Parent;

            return FindByNodeRecursive(root, ResolvedTargetNode);
        }

        private static DomNodeViewModel? FindByNodeRecursive(DomNodeViewModel current, DomNode target)
        {
            if (current.Node == target)
                return current;

            foreach (var child in current.Children)
            {
                var found = FindByNodeRecursive(child, target);
                if (found != null) return found;
            }

            return null;
        }
    }
}

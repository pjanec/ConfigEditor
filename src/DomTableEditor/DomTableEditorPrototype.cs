using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;

namespace DomEditorPrototype
{
    public delegate Type DomSchemaTypeResolver(DomNode node);

    public interface IDomNodeValueRenderer
    {
        void Render(DomNode node, bool isSelected);
    }

    public interface IDomNodeValueEditor
    {
        bool IsEditing { get; }
        void BeginEdit(DomNode node);
        void RenderEditor(DomNode node);
        bool TryGetEditedValue(out object newValue);
        void CancelEdit();
        void ConfirmEdit();
    }

    public class DomTableEditorState
    {
        public DomNode SelectedNode { get; set; }
        public IDomNodeValueEditor ActiveEditor { get; set; }
        public HashSet<DomNode> ExpandedNodes { get; } = new();
        public string FilterText { get; set; } = string.Empty;

        public bool IsEditing => ActiveEditor?.IsEditing ?? false;
    }

    public class DomNode
    {
        public string Name { get; }
        public object Value { get; private set; }

        public DomNode(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public object GetValue() => Value;
        public void SetValue(object value) => Value = value;
    }

    public static class DomTableEditor
    {
        public static void Render(List<DomNode> nodes, DomTableEditorState state, DomSchemaTypeResolver typeResolver)
        {
            ImGui.InputText("Filter", ref state.FilterText, 100);

            int visibleCount = nodes.Count;
            var clipper = new ImGuiListClipper();
            clipper.Begin(visibleCount);

            while (clipper.Step())
            {
                for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    var node = nodes[i];
                    bool matchesFilter = node.Name.Contains(state.FilterText, StringComparison.OrdinalIgnoreCase);
                    if (!matchesFilter) continue;

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.TextWrapped(node.Name);

                    ImGui.TableSetColumnIndex(1);
                    if (state.IsEditing && state.SelectedNode == node)
                    {
                        state.ActiveEditor.RenderEditor(node);
                    }
                    else
                    {
                        ImGui.Text(node.GetValue().ToString());
                        if (ImGui.IsItemClicked() || (state.SelectedNode == node && ImGui.IsKeyPressed(ImGuiKey.Enter)))
                        {
                            var editor = new SimpleTextEditor();
                            editor.BeginEdit(node);
                            state.ActiveEditor = editor;
                            state.SelectedNode = node;
                        }
                    }
                }
            }

            clipper.End();
        }
    }

    public class SimpleTextEditor : IDomNodeValueEditor
    {
        private string _value;
        public bool IsEditing { get; private set; }

        public void BeginEdit(DomNode node)
        {
            _value = node.GetValue().ToString();
            IsEditing = true;
        }

        public void RenderEditor(DomNode node)
        {
            ImGui.InputText("##edit", ref _value, 100);
            if (ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                ConfirmEdit(node);
            }
            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                CancelEdit();
            }
        }

        public bool TryGetEditedValue(out object newValue)
        {
            newValue = _value;
            return true;
        }

        public void CancelEdit()
        {
            IsEditing = false;
        }

        public void ConfirmEdit(DomNode node)
        {
            node.SetValue(_value);
            IsEditing = false;
        }

        public void ConfirmEdit()
        {
            IsEditing = false;
        }
    }
}
using System;
using System.Collections.Generic;

namespace ConfigDom
{
    /// <summary>
    /// Represents a provider of a mounted editable DOM subtree from cascading JSON sources.
    /// Supports merging layers, reference resolution, and file tracking.
    /// </summary>
    public class Json5CascadeEditorContext : IMountedDomEditorContext
    {
        public string MountPath { get; }
        private ObjectNode _root;
        private readonly List<Json5SourceFile> _sourceFiles;
        private readonly DomEditHistory _editHistory = new();

        public Json5CascadeEditorContext(string mountPath, List<Json5SourceFile> sources, ObjectNode root)
        {
            MountPath = mountPath;
            _sourceFiles = sources;
            _root = root;
        }

        public void Load()
        {
            // No-op if already constructed externally.
        }

        public DomNode GetRoot() => _root;

        public bool TryGetSourceFile(string domPath, out Json5SourceFile? file)
        {
            foreach (var f in _sourceFiles)
            {
                if (domPath.StartsWith(MountPath))
                {
                    file = f;
                    return true;
                }
            }
            file = null;
            return false;
        }

        public void ApplyEdit(DomEditAction action)
        {
            _editHistory.Apply(action);
            action.Apply(_root);
        }

        public void Undo()
        {
            var undo = _editHistory.Undo();
            undo?.Apply(_root);
        }

        public void Redo()
        {
            var redo = _editHistory.Redo();
            redo?.Apply(_root);
        }

        public bool CanUndo => _editHistory.CanUndo;
        public bool CanRedo => _editHistory.CanRedo;

        public bool TryResolvePath(string absolutePath, out DomNode? node)
        {
            if (!absolutePath.StartsWith(MountPath.TrimEnd('/')))
            {
                node = null;
                return false;
            }

            node = DomTreePathHelper.FindNodeAtPath(_root, absolutePath);
            return node != null;
        }

    }
}

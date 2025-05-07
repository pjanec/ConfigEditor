using System.Collections.Generic;

namespace ConfigDom
{
    /// <summary>
    /// Represents an editable config context based on a single flat JSON file.
    /// Simpler than cascaded contexts, does not support layering.
    /// Useful for plugins or one-off files.
    /// </summary>
    public class FlatJsonEditorContext : IMountedDomEditorContext
    {
        public string MountPath { get; }
        private readonly string _filePath;
        private Json5SourceFile _file;
        private readonly DomEditHistory _history = new();

        public FlatJsonEditorContext(string mountPath, string filePath)
        {
            MountPath = mountPath;
            _filePath = filePath;
            _file = Json5SourceFileLoader.LoadAllFromFolder(System.IO.Path.GetDirectoryName(filePath))[0];
        }

        public void Load()
        {
            _file = Json5SourceFileLoader.LoadAllFromFolder(System.IO.Path.GetDirectoryName(_filePath))[0];
        }

        public DomNode GetRoot() => _file.DomRoot;

        public bool TryGetSourceFile(string domPath, out Json5SourceFile? file)
        {
            file = domPath.StartsWith(MountPath) ? _file : null;
            return file != null;
        }

        public void ApplyEdit(DomEditAction action)
        {
            _history.Apply(action);
            action.Apply(_file.DomRoot);
        }

        public void Undo()
        {
            var undo = _history.Undo();
            undo?.Apply(_file.DomRoot);
        }

        public void Redo()
        {
            var redo = _history.Redo();
            redo?.Apply(_file.DomRoot);
        }

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;

        public bool TryResolvePath(string absolutePath, out DomNode? node)
        {
            node = DomTreePathHelper.FindNodeAtPath(_file.DomRoot, absolutePath);
            return node != null;
        }
    }
}

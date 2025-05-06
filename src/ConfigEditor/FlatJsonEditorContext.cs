using System.Collections.Generic;
using ConfigEditor.Dom;
using ConfigEditor.Util;

namespace ConfigEditor.Context
{
    public class FlatJsonEditorContext : IMountedDomEditorContext
    {
        public string MountPath { get; }

        private readonly Json5SourceFile _file;
        private readonly DomFlatteningService _flatteningService;

        private ObjectNode _root = new();
        private Dictionary<string, DomNode> _flatMap = new();

        public FlatJsonEditorContext(string mountPath, Json5SourceFile file, DomFlatteningService flatteningService)
        {
            MountPath = mountPath;
            _file = file;
            _flatteningService = flatteningService;

            Reload();
        }

        public DomNode Root => _root;

        public IReadOnlyDictionary<string, DomNode> FlattenedMap => _flatMap;

        public void Reload()
        {
            _root = JsonMergeService.ParseFileToDom(_file);
            _flatMap = _flatteningService.Flatten(_root);
        }

        public bool IsDirty => _file.IsDirty;

        public IEnumerable<Json5SourceFile> GetEditableSourceFiles()
        {
            yield return _file;
        }

        public void Save()
        {
            if (_file.IsDirty)
                _file.Save();
        }
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConfigEditor.Dom;
using ConfigEditor.Util;

namespace ConfigEditor.Context
{
    public class Json5CascadeEditorContext : IMountedDomEditorContext
    {
        public string MountPath { get; }

        private readonly List<string> _cascadeFolders;
        private readonly List<Json5SourceFile> _sourceFiles = new();
        private readonly JsonMergeService _mergeService;
        private readonly DomFlatteningService _flatteningService;

        private ObjectNode _root = new();
        private Dictionary<string, DomNode> _flatMap = new();

        public Json5CascadeEditorContext(string mountPath, List<string> cascadeFolders, JsonMergeService mergeService, DomFlatteningService flatteningService)
        {
            MountPath = mountPath;
            _cascadeFolders = cascadeFolders;
            _mergeService = mergeService;
            _flatteningService = flatteningService;

            Reload();
        }

        public DomNode Root => _root;

        public IReadOnlyDictionary<string, DomNode> FlattenedMap => _flatMap;

        public void Reload()
        {
            _sourceFiles.Clear();
            foreach (var folder in _cascadeFolders)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories))
                {
                    var parsed = Json5SourceFileLoader.Load(file);
                    if (parsed != null)
                        _sourceFiles.Add(parsed);
                }
            }

            _root = _mergeService.MergeFilesToDom(_sourceFiles);
            _flatMap = _flatteningService.Flatten(_root);
        }

        public bool IsDirty => _sourceFiles.Any(f => f.IsDirty);

        public IEnumerable<Json5SourceFile> GetEditableSourceFiles() => _sourceFiles;

        public void Save()
        {
            foreach (var file in _sourceFiles)
            {
                if (file.IsDirty)
                    file.Save();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using ConfigEditor.Dom;
using ConfigEditor.Schema;

namespace ConfigEditor.Context
{
    public class EditorWorkspace
    {
        private readonly List<IMountedDomEditorContext> _contexts = new();
        private readonly Dictionary<string, DomNode> _globalPathMap = new();
        private readonly Dictionary<string, ISchemaNode> _schemaMap = new();

        public void RegisterContexts(IEnumerable<(string mountPath, IMountedDomEditorContext context)> contextDefs, RuntimeSchemaCatalog? schemaCatalog = null)
        {
            _contexts.Clear();
            _globalPathMap.Clear();
            _schemaMap.Clear();

            foreach (var (mountPath, context) in contextDefs)
            {
                _contexts.Add(context);
                foreach (var kvp in context.FlattenedMap)
                {
                    var fullPath = string.IsNullOrEmpty(mountPath) ? kvp.Key : $"{mountPath}/{kvp.Key}";
                    _globalPathMap[fullPath] = kvp.Value;
                }
            }

            if (schemaCatalog != null)
            {
                foreach (var (mountPath, schemaRoot) in schemaCatalog.Schemas)
                {
                    foreach (var kvp in schemaRoot.Flatten())
                    {
                        var fullPath = string.IsNullOrEmpty(mountPath) ? kvp.Key : $"{mountPath}/{kvp.Key}";
                        _schemaMap[fullPath] = kvp.Value;

                        if (_globalPathMap.TryGetValue(fullPath, out var node))
                            node.SchemaNode = kvp.Value;
                    }
                }
            }
        }

        public DomNode? TryGetNode(string fullPath) => _globalPathMap.TryGetValue(fullPath, out var node) ? node : null;

        public ISchemaNode? TryGetSchema(string fullPath) => _schemaMap.TryGetValue(fullPath, out var schema) ? schema : null;

        public IEnumerable<DomNode> AllNodes => _globalPathMap.Values;

        public bool AnyDirty => _contexts.Any(c => c.IsDirty);

        public void SaveAll()
        {
            foreach (var context in _contexts)
                context.Save();
        }
    }
}

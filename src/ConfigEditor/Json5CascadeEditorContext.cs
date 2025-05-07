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
        private readonly MergeOriginTracker _originTracker = new();

        public Json5CascadeEditorContext(string mountPath, List<Json5SourceFile> sources)
        {
            MountPath = mountPath;
            _sourceFiles = sources;
            _root = JsonMergeService.MergeCascade(sources, _originTracker);
        }

        public void Load()
        {
            // Reload all sources and rebuild the merged tree
            _root = JsonMergeService.MergeCascade(_sourceFiles, _originTracker);
        }

        public DomNode GetRoot() => _root;

        public bool TryGetSourceFile(string domPath, out Json5SourceFile? file)
        {
            var origin = _originTracker.GetOrigin(domPath);
            if (origin.HasValue)
            {
                file = origin.Value.file;
                return true;
            }
            file = null;
            return false;
        }

        public void ApplyEdit(DomEditAction action)
        {
            // Determine which level to write to
            var origin = _originTracker.GetOrigin(action.Path);
            var targetLevel = origin?.level ?? _sourceFiles.Count - 1; // Default to most specific

            // Find the appropriate source file
            var targetFile = _sourceFiles[targetLevel];
            
            // Apply the edit
            _editHistory.Apply(action);
            action.Apply(_root);
            
            // Update origin tracking
            _originTracker.TrackOrigin(action.Path, targetFile, targetLevel);
        }

        public void Undo()
        {
            var undo = _editHistory.Undo();
            if (undo != null)
            {
                undo.Apply(_root);
                // Note: We don't update origin tracking on undo/redo
                // as the original origins are preserved in the source files
            }
        }

        public void Redo()
        {
            var redo = _editHistory.Redo();
            if (redo != null)
            {
                redo.Apply(_root);
                // Note: We don't update origin tracking on undo/redo
                // as the original origins are preserved in the source files
            }
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

        /// <summary>
        /// Gets the cascade level name (base/site/local) for a given path.
        /// </summary>
        public string? GetLevelName(string path)
        {
            var origin = _originTracker.GetOrigin(path);
            return origin.HasValue ? _originTracker.GetLevelName(origin.Value.level) : null;
        }

        /// <summary>
        /// Gets the cascade level index (0=base, 1=site, 2=local) for a given path.
        /// </summary>
        public int? GetLevelIndex(string path)
        {
            return _originTracker.GetOrigin(path)?.level;
        }
    }
}

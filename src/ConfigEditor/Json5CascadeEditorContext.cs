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
        private readonly List<CascadeLayer> _layers;
        private readonly DomEditHistory _editHistory = new();
        private readonly MergeOriginTracker _originTracker = new();

        public Json5CascadeEditorContext(string mountPath, List<CascadeLayer> layers)
        {
            MountPath = mountPath;
            _layers = layers;
            _root = JsonMergeService.MergeCascade(layers, _originTracker);
        }

        public void Load()
        {
            // Reload all sources and rebuild the merged tree
            _root = JsonMergeService.MergeCascade(_layers, _originTracker);
        }

        public DomNode GetRoot() => _root;

        public bool TryGetSourceFile(string domPath, out Json5SourceFile? file, out int layerIndex)
        {
            var origin = _originTracker.GetOrigin(domPath);
            if (origin.HasValue)
            {
                file = origin.Value.file;
                layerIndex = origin.Value.layerIndex;
                return true;
            }
            file = null;
            layerIndex = -1;
            return false;
        }

        public bool TryGetOrCreateFileForLayer(int layerIndex, string path, out Json5SourceFile file)
        {
            if (layerIndex < 0 || layerIndex >= _layers.Count)
            {
                file = null!;
                return false;
            }

            var layer = _layers[layerIndex];
            foreach (var existingFile in layer.Files)
            {
                if (existingFile.RelativePath == path)
                {
                    file = existingFile;
                    return true;
                }
            }

            // Create new file in this layer
            file = new Json5SourceFile(path, path, new ObjectNode(path), "");
            layer.Files.Add(file);
            return true;
        }

        public void ApplyEdit(DomEditAction action)
        {
            // Determine which level to write to
            var origin = _originTracker.GetOrigin(action.Path);
            var targetLevel = origin?.layerIndex ?? _layers.Count - 1; // Default to most specific

            // Find or create the appropriate source file
            if (TryGetOrCreateFileForLayer(targetLevel, action.Path, out var targetFile))
            {
                // Apply the edit
                _editHistory.Apply(action);
                action.Apply(_root);
                
                // Update origin tracking
                _originTracker.TrackOrigin(action.Path, targetFile, targetLevel);
            }
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
        /// Gets the cascade level name for a given path.
        /// </summary>
        public string? GetLevelName(string path)
        {
            var origin = _originTracker.GetOrigin(path);
            return origin.HasValue ? _layers[origin.Value.layerIndex].Name : null;
        }

        /// <summary>
        /// Gets the cascade level index for a given path.
        /// </summary>
        public int? GetLevelIndex(string path)
        {
            return _originTracker.GetLayerIndex(path);
        }
    }
}

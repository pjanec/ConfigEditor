using System;
using System.Collections.Generic;

namespace ConfigEditor.Context
{
    public interface IMountedDomEditorContext
    {
        string MountPath { get; }

        // Returns the merged root node of the mounted context
        DomNode Root { get; }

        // Returns a flat path -> node dictionary for efficient lookup
        IReadOnlyDictionary<string, DomNode> FlattenedMap { get; }

        // Optionally reloads from disk or source
        void Reload();

        // Returns true if any node in the context is dirty
        bool IsDirty { get; }

        // Returns the source file list and their editable contents
        IEnumerable<Json5SourceFile> GetEditableSourceFiles();

        // Saves all dirty files back to disk (if applicable)
        void Save();
    }
}

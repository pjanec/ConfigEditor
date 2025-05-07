using ConfigDom.Editor;
using System;
using System.Collections.Generic;
using System.IO;

namespace ConfigDom
{
    /// <summary>
    /// Factory for creating Json5CascadeEditorContext instances from folders.
    /// Loads *.json files from each level and merges them by path.
    /// </summary>
    public static class Json5CascadeEditorContextFactory
    {
        /// <summary>
        /// Loads all *.json files from the given cascade level folders and constructs a cascading context.
        /// </summary>
        /// <param name="mountPath">The virtual mount path in the DOM tree where the context will reside.</param>
        /// <param name="cascadeFolders">List of folders, from least to most specific (e.g., base → site → local).</param>
        public static Json5CascadeEditorContext LoadCascadeFromFolders(string mountPath, List<string> cascadeFolders)
        {
            throw new NotImplementedException( "Loading from folders is not implemented yet." );
		}
    }
}

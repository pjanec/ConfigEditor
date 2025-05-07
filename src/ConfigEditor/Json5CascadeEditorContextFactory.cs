using ConfigDom.Editor;
using System.Collections.Generic;
using System.IO;

namespace ConfigDom
{
    /// <summary>
    /// Factory for constructing cascading editor contexts from multiple levels of JSON5 files.
    /// </summary>
    public static class Json5CascadeEditorContextFactory
    {
        /// <summary>
        /// Loads all *.json files from each folder in cascade order and mounts under the given path.
        /// </summary>
        public static Json5CascadeEditorContext LoadCascadeFromFolders(string mountPath, List<string> cascadeFolders)
        {
            var allSources = new List<Json5SourceFile>();

            foreach (var folder in cascadeFolders)
            {
                var rootLength = folder.TrimEnd(Path.DirectorySeparatorChar).Length + 1;
                foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories))
                {
                    var relative = file[rootLength..].Replace("\\", "/").Replace(".json", "");
                    var source = Json5SourceFileLoader.LoadSingleFile(file, relative);
                    allSources.Add(source);
                }
            }

            var merged = JsonMergeService.MergeCascade(allSources);
            return new Json5CascadeEditorContext(mountPath, allSources, merged);
        }
    }
}

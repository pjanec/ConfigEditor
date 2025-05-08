using ConfigDom.Editor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConfigDom
{
    /// <summary>
    /// Factory for constructing cascading editor contexts from multiple levels of JSON5 files.
    /// </summary>
    public static class Json5CascadeEditorContextFactory
    {
        /// <summary>
        /// Creates a context from pre-loaded layers.
        /// </summary>
        public static Json5CascadeEditorContext FromPreloadedLayers(string mountPath, List<(string Name, List<Json5SourceFile> Files)> layers)
        {
            var cascadeLayers = layers.Select(l => new CascadeLayer(l.Name, l.Files)).ToList();
            return new Json5CascadeEditorContext(mountPath, cascadeLayers);
        }

        public static Json5CascadeEditorContext LoadFromFolders(string mountPath, List<string> folders)
        {
			// use folder path as name
			var layers = folders.Select( folder => (Name: folder, FolderPath: folder) ).ToList();
			return LoadFromFolders( mountPath, layers );
		}

		/// <summary>
		/// Loads all *.json files from each folder in cascade order and mounts under the given path.
		/// </summary>
		public static Json5CascadeEditorContext LoadFromFolders(string mountPath, List<(string Name, string FolderPath)> folders)
        {
            var layers = new List<CascadeLayer>();
            foreach (var (name, folder) in folders)
            {
                var files = LoadJsonFilesFromFolder(folder);
                layers.Add(new CascadeLayer(name, files));
            }

            return new Json5CascadeEditorContext(mountPath, layers);
        }

        private static List<Json5SourceFile> LoadJsonFilesFromFolder(string folder)
        {
            var sources = new List<Json5SourceFile>();
            var rootLength = folder.TrimEnd(Path.DirectorySeparatorChar).Length + 1;
            foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories))
            {
                var relative = file[rootLength..].Replace("\\", "/").Replace(".json", "");
                var source = Json5SourceFileLoader.LoadSingleFile(file, relative);
                sources.Add(source);
            }
            return sources;
        }
    }
}

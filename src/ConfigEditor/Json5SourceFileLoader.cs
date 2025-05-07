using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ConfigDom.Editor
{
    /// <summary>
    /// Responsible for loading and parsing JSON files as editable source inputs.
    /// Supports folder-wide loading and targeted single-file loading.
    /// </summary>
    public static class Json5SourceFileLoader
    {
        /// <summary>
        /// Loads all .json files from the specified folder and parses them as editable source files.
        /// </summary>
        /// <param name="folder">Absolute or relative path to the folder containing .json files.</param>
        /// <returns>A list of parsed Json5SourceFile instances, one per file.</returns>
        public static List<Json5SourceFile> LoadAllFromFolder(string folder)
        {
            var result = new List<Json5SourceFile>();
            var files = Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories);

            foreach (var filePath in files)
            {
                try
                {
                    result.Add(LoadSingleFile(filePath, folder));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error parsing {filePath}: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Loads a single .json file and returns the parsed Json5SourceFile.
        /// </summary>
        /// <param name="filePath">Full path to the .json file.</param>
        /// <param name="baseFolder">Optional base folder for relative path construction.</param>
        /// <returns>The parsed source file.</returns>
        public static Json5SourceFile LoadSingleFile(string filePath, string? baseFolder = null)
        {
            string text = File.ReadAllText(filePath);
            JsonElement element = Json5Parser.Parse(text);
            DomNode dom = JsonDomBuilder.BuildFromJsonElement(Path.GetFileNameWithoutExtension(filePath), element);
            string relativePath = baseFolder != null
                ? Path.GetRelativePath(baseFolder, filePath).Replace("\\", "/")
                : Path.GetFileName(filePath);

            return new Json5SourceFile(filePath, relativePath, dom, text);
        }
    }
}

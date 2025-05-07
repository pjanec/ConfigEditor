using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Responsible for loading and parsing all *.json files in a folder as JSON5.
    /// Handles error capture, DOM construction, and source metadata extraction.
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
                    string text = File.ReadAllText(filePath);
                    JsonElement element = Json5Parser.Parse(text);
                    ObjectNode dom = JsonDomBuilder.BuildFromJsonElement(Path.GetFileNameWithoutExtension(filePath), element);
                    string relativePath = Path.GetRelativePath(folder, filePath).Replace("\\", "/");

                    result.Add(new Json5SourceFile(filePath, relativePath, dom, text));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error parsing {filePath}: {ex.Message}");
                }
            }

            return result;
        }
    }
}

using JsonConfigEditor.Core.Cascade;
using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.Core.Parsing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// Represents the structured result of loading all files for a single layer,
    /// before any merging has occurred.
    /// </summary>
    public record LayerLoadResult(LayerDefinitionModel Definition, IReadOnlyList<SourceFileInfo> SourceFiles);
    
    /// <summary>
    /// Represents a single layer definition as parsed from the project file.
    /// </summary>
    public record LayerDefinitionModel([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("folderPath")] string FolderPath);

    /// <summary>
    /// Orchestrates the loading of a cascade project. It reads the project definition file,
    /// discovers all source JSON files within each layer's folder, and parses them into
    /// SourceFileInfo objects. It does not perform any merging logic.
    /// </summary>
    public class ProjectLoader
    {
        private readonly IJsonDomParser _jsonParser;
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public ProjectLoader(IJsonDomParser jsonParser)
        {
            _jsonParser = jsonParser ?? throw new ArgumentNullException(nameof(jsonParser));
        }

        /// <summary>
        /// Loads a cascade project from a specified file path.
        /// </summary>
        /// <param name="projectFilePath">The absolute path to the *.cascade.jsonc project file.</param>
        /// <returns>A list of LayerLoadResult objects, each containing the raw materials for a single layer.</returns>
        public async Task<List<LayerLoadResult>> LoadProjectAsync(string projectFilePath)
        {
            var projectDirectory = Path.GetDirectoryName(projectFilePath) 
                ?? throw new ArgumentException("Could not determine project directory.", nameof(projectFilePath));

            // 1. Read and parse the main project definition file
            var projectModel = await ReadProjectFileAsync(projectFilePath);

            var loadedLayers = new List<LayerLoadResult>();

            // 2. Iterate through each layer definition and process its folder
            foreach (var layerDef in projectModel.Layers)
            {
                var absoluteLayerPath = Path.GetFullPath(Path.Combine(projectDirectory, layerDef.FolderPath));
                if (!Directory.Exists(absoluteLayerPath))
                {
                    // Optionally, log this as a warning instead of throwing
                    throw new DirectoryNotFoundException($"Layer folder not found: {absoluteLayerPath}");
                }
                
                // 3. Discover and parse all JSON files within the layer's folder
                var sourceFiles = await LoadAllFilesFromLayerFolderAsync(absoluteLayerPath);
                
                loadedLayers.Add(new LayerLoadResult(layerDef, sourceFiles));
            }
            
            return loadedLayers;
        }

        /// <summary>
        /// Reads the main *.cascade.jsonc file and deserializes it into a model.
        /// </summary>
        private async Task<ProjectFileModel> ReadProjectFileAsync(string projectFilePath)
        {
            var jsonContent = await File.ReadAllTextAsync(projectFilePath);
            var projectModel = JsonSerializer.Deserialize<ProjectFileModel>(jsonContent, _serializerOptions);

            if (projectModel == null || projectModel.Layers == null)
            {
                throw new JsonException($"Failed to parse project file or it contains no layers: {projectFilePath}");
            }

            return projectModel;
        }

        /// <summary>
        /// Finds all *.json files in a layer folder and parses each one into a SourceFileInfo object.
        /// </summary>
        private async Task<IReadOnlyList<SourceFileInfo>> LoadAllFilesFromLayerFolderAsync(string absoluteLayerPath)
        {
            var sourceFiles = new List<SourceFileInfo>();
            var jsonFiles = Directory.GetFiles(absoluteLayerPath, "*.json", SearchOption.AllDirectories);

            foreach (var filePath in jsonFiles.OrderBy(p => p)) // Order for determinism
            {
                try
                {
                    var fileContent = await File.ReadAllTextAsync(filePath);
                    // Use the existing parser service from WpfEditorGemini
                    var domRoot = _jsonParser.ParseFromString(fileContent); 
                    
                    var relativePath = Path.GetRelativePath(absoluteLayerPath, filePath).Replace('\\', '/');

                    sourceFiles.Add(new SourceFileInfo(filePath, relativePath, domRoot, fileContent));
                }
                catch(Exception ex)
                {
                    // In a real implementation, this would log to the "Issues" panel
                    Console.Error.WriteLine($"Error parsing file {filePath}: {ex.Message}");
                    // Decide whether to continue or throw. For now, we'll continue.
                }
            }

            return sourceFiles;
        }

        /// <summary>
        /// Internal model for deserializing the root of the project file.
        /// </summary>
        private record ProjectFileModel([property: JsonPropertyName("layers")] List<LayerDefinitionModel> Layers);
    }
}



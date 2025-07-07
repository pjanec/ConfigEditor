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
    public record LayerLoadResult(LayerDefinitionModel Definition, string AbsoluteFolderPath, IReadOnlyList<SourceFileInfo> SourceFiles, List<string> Errors);
    
    /// <summary>
    /// Represents the complete result of loading a project, including schema paths and errors.
    /// </summary>
    public record ProjectLoadResult(List<LayerLoadResult> LayerData, List<string> SchemaAssemblyPaths, List<string> Errors);
    
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
        /// <returns>A ProjectLoadResult object containing layer data, schema paths, and any errors.</returns>
        public async Task<ProjectLoadResult> LoadProjectAsync(string projectFilePath)
        {
            var projectDirectory = Path.GetDirectoryName(projectFilePath) 
                ?? throw new ArgumentException("Could not determine project directory.", nameof(projectFilePath));

            var projectModel = await ReadProjectFileAsync(projectFilePath);
            var loadedLayers = new List<LayerLoadResult>();
            var projectLoadErrors = new List<string>();

            // NEW: Resolve schema paths from the project file
            var schemaAssemblyPaths = ResolveSchemaSources(projectModel.SchemaSources, projectDirectory, projectLoadErrors);

            int layerIndex = 0;
            foreach (var layerDef in projectModel.Layers)
            {
                var absoluteLayerPath = Path.GetFullPath(Path.Combine(projectDirectory, layerDef.FolderPath));
                var layerErrors = new List<string>();

                if (!Directory.Exists(absoluteLayerPath))
                {
                    layerErrors.Add($"Layer '{layerDef.Name}' folder not found: {absoluteLayerPath}");
                    loadedLayers.Add(new LayerLoadResult(layerDef, absoluteLayerPath, new List<SourceFileInfo>(), layerErrors));
                    continue;
                }

                // MODIFIED: Pass the error list to the helper
                var sourceFiles = await LoadAllFilesFromLayerFolderAsync(absoluteLayerPath, layerIndex++, layerErrors);

                loadedLayers.Add(new LayerLoadResult(layerDef, absoluteLayerPath, sourceFiles, layerErrors));
            }

            // MODIFIED: Return the new, more comprehensive result object
            return new ProjectLoadResult(loadedLayers, schemaAssemblyPaths, projectLoadErrors);
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
        private async Task<IReadOnlyList<SourceFileInfo>> LoadAllFilesFromLayerFolderAsync(string absoluteLayerPath, int layerIndex, List<string> errors)
        {
            var jsonFiles = Directory.GetFiles(absoluteLayerPath, "*.json", SearchOption.AllDirectories);

            // *** NEW: Add fatal check for file path casing conflicts ***
            var pathGroups = jsonFiles.GroupBy(p => p, StringComparer.OrdinalIgnoreCase);
            foreach (var group in pathGroups)
            {
                if (group.Count() > 1)
                {
                    // This is a fatal error. The project cannot be loaded reliably.
                    var errorMessage = $"Fatal load error: Multiple files found with case-only differences in path in layer '{absoluteLayerPath}'. " +
                        $"Conflict between: {string.Join(" and ", group)}";
                    errors.Add(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }
            }
            // *** END NEW ***

            var sourceFiles = new List<SourceFileInfo>();

            foreach (var filePath in jsonFiles.OrderBy(p => p)) // Order for determinism
            {
                try
                {
                    var fileContent = await File.ReadAllTextAsync(filePath);
                    // Use the existing parser service from WpfEditorGemini
                    var domRoot = _jsonParser.ParseFromString(fileContent); 
                    
                    var relativePath = Path.GetRelativePath(absoluteLayerPath, filePath).Replace('\\', '/');

                    sourceFiles.Add(new SourceFileInfo(filePath, relativePath, domRoot, fileContent, layerIndex));
                }
                catch(Exception ex)
                {
                    // Add error to the list instead of just writing to console
                    errors.Add($"Error parsing file {filePath}: {ex.Message}");
                }
            }

            return sourceFiles;
        }

        // ADD these new methods to the ProjectLoader class
        private List<string> ResolveSchemaSources(List<string>? schemaSources, string projectDirectory, List<string> errors)
        {
            if (schemaSources == null)
            {
                return new List<string>();
            }

            var resolvedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sourceEntry in schemaSources)
            {
                try
                {
                    var paths = ResolveSchemaSourceEntry(sourceEntry, projectDirectory);
                    foreach (var path in paths)
                    {
                        resolvedPaths.Add(path);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Error resolving schema source '{sourceEntry}': {ex.Message}");
                }
            }
            return resolvedPaths.ToList();
        }

        private IEnumerable<string> ResolveSchemaSourceEntry(string sourcePath, string projectDirectory)
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(sourcePath);
            var fullPath = Path.IsPathRooted(expandedPath) ? expandedPath : Path.GetFullPath(Path.Combine(projectDirectory, expandedPath));

            if (!fullPath.Contains('*'))
            {
                if (File.Exists(fullPath)) return new[] { fullPath };
                if (Directory.Exists(fullPath)) return Directory.GetFiles(fullPath, "*.dll", SearchOption.TopDirectoryOnly);
                return Enumerable.Empty<string>();
            }

            var searchOption = sourcePath.Contains("**") ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var directoryToSearch = Path.GetDirectoryName(fullPath);
            var searchPattern = Path.GetFileName(fullPath);

            if (directoryToSearch != null && directoryToSearch.Contains("**"))
            {
                directoryToSearch = directoryToSearch.Substring(0, directoryToSearch.IndexOf("**", StringComparison.Ordinal)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            if (string.IsNullOrEmpty(directoryToSearch) || !Directory.Exists(directoryToSearch))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.GetFiles(directoryToSearch, searchPattern, searchOption);
        }

        /// <summary>
        /// Internal model for deserializing the root of the project file.
        /// </summary>
        private record ProjectFileModel(
            [property: JsonPropertyName("layers")] List<LayerDefinitionModel> Layers,
            [property: JsonPropertyName("schemaSources")] List<string>? SchemaSources);
    }
}



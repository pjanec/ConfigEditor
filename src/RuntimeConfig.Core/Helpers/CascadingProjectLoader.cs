using RuntimeConfig.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RuntimeConfig.Core.Helpers
{
    // Internal model for deserializing the layer definitions from the project file.
    internal record LayerDefinitionModel([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("folderPath")] string FolderPath);

    // Internal model for deserializing the root of the project file.
    internal record ProjectFileModel([property: JsonPropertyName("layers")] List<LayerDefinitionModel> Layers);

    /// <summary>
    /// A helper class to load layer definitions from a .cascade.jsonc project file.
    /// This simplifies the setup for applications consuming a cascading configuration.
    /// </summary>
    public class CascadingProjectLoader
    {
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        /// <summary>
        /// Asynchronously loads layer definitions from a specified project file.
        /// </summary>
        /// <param name="projectFilePath">The absolute or relative path to the *.cascade.jsonc project file.</param>
        /// <returns>A read-only list of LayerDefinition records with absolute paths.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the project file does not exist.</exception>
        /// <exception cref="JsonException">Thrown if the project file is malformed.</exception>
        public async Task<IReadOnlyList<LayerDefinition>> LoadLayersFromProjectFileAsync(string projectFilePath)
        {
            if (!File.Exists(projectFilePath))
            {
                throw new FileNotFoundException("Cascade project file not found.", projectFilePath);
            }

            var projectDirectory = Path.GetDirectoryName(projectFilePath)
                ?? throw new ArgumentException("Could not determine project directory.", nameof(projectFilePath));

            var jsonContent = await File.ReadAllTextAsync(projectFilePath);
            var projectModel = JsonSerializer.Deserialize<ProjectFileModel>(jsonContent, _serializerOptions);

            if (projectModel == null || projectModel.Layers == null)
            {
                throw new JsonException($"Failed to parse project file or it contains no layers: {projectFilePath}");
            }

            // Convert the relative folder paths from the project file into absolute paths
            // so the runtime provider can find them.
            return projectModel.Layers
                .Select(layerDef => new LayerDefinition(
                    layerDef.Name,
                    Path.GetFullPath(Path.Combine(projectDirectory, layerDef.FolderPath))
                ))
                .ToList();
        }
    }
}

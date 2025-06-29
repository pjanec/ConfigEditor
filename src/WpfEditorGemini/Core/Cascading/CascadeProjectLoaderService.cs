using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

// Namespace will align with other Core entities, e.g., JsonConfigEditor.Core.Cascading
namespace JsonConfigEditor.Core.Cascading
{
    public interface ICascadeProjectLoaderService
    {
        /// <summary>
        /// Asynchronously loads and parses a cascade project definition file.
        /// </summary>
        /// <param name="projectFilePath">The absolute path to the cascade project file (e.g., cascade_project.jsonc).</param>
        /// <returns>A list of LayerDefinition objects, or null if loading fails.</returns>
        Task<List<LayerDefinition>?> LoadCascadeProjectAsync(string projectFilePath);
    }

    public class CascadeProjectLoaderService : ICascadeProjectLoaderService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Asynchronously loads and parses a cascade project definition file.
        /// </summary>
        /// <param name="projectFilePath">The absolute path to the cascade project file (e.g., cascade_project.jsonc).</param>
        /// <returns>A list of LayerDefinition objects, or null if loading fails. Errors are logged internally.</returns>
        public async Task<List<LayerDefinition>?> LoadCascadeProjectAsync(string projectFilePath)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath))
            {
                // Log error: projectFilePath is null or empty
                Console.Error.WriteLine("[CascadeProjectLoaderService] Error: Project file path cannot be null or empty.");
                return null;
            }

            if (!File.Exists(projectFilePath))
            {
                // Log error: projectFilePath does not exist
                Console.Error.WriteLine($"[CascadeProjectLoaderService] Error: Project file not found at '{projectFilePath}'.");
                return null;
            }

            try
            {
                string jsonContent = await File.ReadAllTextAsync(projectFilePath);
                var layerDefinitions = JsonSerializer.Deserialize<List<LayerDefinition>>(jsonContent, _jsonOptions);

                if (layerDefinitions == null || !layerDefinitions.Any())
                {
                    // Log warning or error: project file is empty or does not define any layers
                    Console.Error.WriteLine($"[CascadeProjectLoaderService] Warning: Project file '{projectFilePath}' is empty or contains no layer definitions.");
                    return new List<LayerDefinition>(); // Return empty list
                }

                string projectDirectory = Path.GetDirectoryName(projectFilePath) ?? "";

                for (int i = 0; i < layerDefinitions.Count; i++)
                {
                    var layerDef = layerDefinitions[i];
                    layerDef.LayerIndex = i; // Assign layer index based on order in the file

                    if (string.IsNullOrWhiteSpace(layerDef.Name))
                    {
                        // Log warning: Layer name is missing, using a default
                        Console.Error.WriteLine($"[CascadeProjectLoaderService] Warning: Layer at index {i} in '{projectFilePath}' is missing a name. Using default.");
                        layerDef.Name = $"Unnamed Layer {i}";
                    }

                    if (string.IsNullOrWhiteSpace(layerDef.FolderPath))
                    {
                        // Log error: FolderPath is crucial and missing
                        Console.Error.WriteLine($"[CascadeProjectLoaderService] Error: Layer '{layerDef.Name}' (Index: {i}) in '{projectFilePath}' is missing 'folderPath'. Skipping this layer.");
                        // Optionally remove this layer or mark as invalid. For now, we'll keep it but ResolvedFolderPath will be empty.
                        layerDef.ResolvedFolderPath = string.Empty;
                        continue;
                    }

                    // Resolve FolderPath: if not absolute, assume it's relative to the project file's directory
                    if (Path.IsPathRooted(layerDef.FolderPath))
                    {
                        layerDef.ResolvedFolderPath = layerDef.FolderPath;
                    }
                    else
                    {
                        layerDef.ResolvedFolderPath = Path.GetFullPath(Path.Combine(projectDirectory, layerDef.FolderPath));
                    }

                    if (!Directory.Exists(layerDef.ResolvedFolderPath))
                    {
                        // Log warning: The resolved folder path for a layer does not exist.
                        // The layer will be loaded, but will likely contain no source files.
                        Console.Error.WriteLine($"[CascadeProjectLoaderService] Warning: Folder for layer '{layerDef.Name}' (Index: {i}) not found at resolved path '{layerDef.ResolvedFolderPath}'.");
                    }
                }

                // Filter out layers that couldn't have their paths resolved properly if strict
                // return layerDefinitions.Where(ld => !string.IsNullOrEmpty(ld.ResolvedFolderPath)).ToList();
                return layerDefinitions;
            }
            catch (JsonException jsonEx)
            {
                // Log error: JSON parsing error
                Console.Error.WriteLine($"[CascadeProjectLoaderService] Error parsing project file '{projectFilePath}': {jsonEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                // Log error: General error during loading
                Console.Error.WriteLine($"[CascadeProjectLoaderService] Unexpected error loading project file '{projectFilePath}': {ex.Message}");
                return null;
            }
        }
    }
}

using JsonConfigEditor.Core.Cascade;
using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.Core.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// Handles the logic for saving a modified CascadeLayer back to its constituent physical files.
    /// </summary>
    public class ProjectSaver
    {
        private readonly IDomNodeToJsonSerializer _serializer;

        public ProjectSaver(IDomNodeToJsonSerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>
        /// Saves all modified files within a given cascade layer.
        /// </summary>
        /// <param name="layer">The layer to save.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task SaveLayerAsync(CascadeLayer layer)
        {
            if (!layer.IsDirty)
            {
                return; // Nothing to save
            }

            // Phase 1: Build the Save Plan
            // The plan determines which top-level properties go into which file.
            var savePlan = BuildSavePlan(layer);

            // Phase 2: Reconstruct file content and write to disk if changed
            foreach (var (relativeFilePath, topLevelPropertyNames) in savePlan)
            {
                await ReconstructAndWriteFileAsync(layer, relativeFilePath, topLevelPropertyNames);
            }
            
            // After successful save, the layer is no longer dirty.
            // In a real app, this might be set after all tasks complete successfully.
            layer.IsDirty = false;
        }

        /// <summary>
        /// Creates a plan that maps each file to the list of top-level properties it should contain.
        /// </summary>
        private Dictionary<string, List<string>> BuildSavePlan(CascadeLayer layer)
        {
            var plan = new Dictionary<string, List<string>>();

            // We only need to consider the top-level children of the layer's root.
            foreach (var topLevelNode in layer.LayerConfigRootNode.GetChildren())
            {
                // Find the destination file for this top-level property from the origin map.
                if (layer.IntraLayerValueOrigins.TryGetValue(topLevelNode.Path, out var destinationFile))
                {
                    if (!plan.TryGetValue(destinationFile, out var propertyList))
                    {
                        propertyList = new List<string>();
                        plan[destinationFile] = propertyList;
                    }
                    propertyList.Add(topLevelNode.Name);
                }
                else
                {
                    // This is an edge case: a top-level property exists but has no assigned file.
                    // This could be logged as an error. For now, we'll ignore it.
                    Console.Error.WriteLine($"Warning: Property '{topLevelNode.Path}' has no source file assigned and will not be saved.");
                }
            }
            return plan;
        }

        /// <summary>
        /// Rebuilds the content of a single file from the save plan and writes it to disk if it has changed.
        /// </summary>
        private async Task ReconstructAndWriteFileAsync(CascadeLayer layer, string relativeFilePath, List<string> propertyNames)
        {
            // 1. Create a new root node to represent the content of the file we're about to write.
            var fileContentRoot = new ObjectNode("$root", null);

            // 2. Populate it by cloning the relevant properties from the layer's main tree.
            foreach (var propertyName in propertyNames)
            {
                if (layer.LayerConfigRootNode.GetChild(propertyName) is DomNode nodeToSave)
                {
                    // FIX: Use the shared cloning utility. The parent is null because this
                    // is a temporary root for serialization.
                    fileContentRoot.AddChild(nodeToSave.Name, DomCloning.CloneNode(nodeToSave, null));
                }
            }
            
            // 3. Serialize the reconstructed content to a JSON string.
            var newFileContent = _serializer.SerializeToString(fileContentRoot, indented: true);

            // 4. Compare with original content to see if a write is needed.
            var originalFile = layer.SourceFiles.FirstOrDefault(f => f.RelativePath == relativeFilePath);
            var originalContent = originalFile?.OriginalText;
            
            // Only write if the file is new or if the content has actually changed.
            if (originalFile == null || originalContent != newFileContent)
            {
                var absoluteFilePath = Path.Combine(layer.FolderPath, relativeFilePath);
                
                // Ensure the directory exists before writing.
                var directory = Path.GetDirectoryName(absoluteFilePath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory);
                }
                
                await File.WriteAllTextAsync(absoluteFilePath, newFileContent);
            }
        }
        

    }
}



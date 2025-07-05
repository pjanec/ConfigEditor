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
                return;
            }

            // Phase 1: Build the Save Plan.
            // The plan maps a relative file path to the single DomNode that should be its content.
            var savePlan = BuildSavePlan(layer);

            // Phase 2: Write files to disk.
            foreach (var (relativeFilePath, nodeToSave) in savePlan)
            {
                // Find the original source file info to compare content.
                var originalFile = layer.SourceFiles.FirstOrDefault(f => f.RelativePath.Equals(relativeFilePath, StringComparison.OrdinalIgnoreCase));
                string newContent = _serializer.SerializeToString(nodeToSave, indented: true);

                // Only write if the file is new or if the content has actually changed.
                if (originalFile == null || originalFile.OriginalText != newContent)
                {
                    var absoluteFilePath = Path.Combine(layer.FolderPath, relativeFilePath);
                    var directory = Path.GetDirectoryName(absoluteFilePath);
                    if (directory != null)
                    {
                        Directory.CreateDirectory(directory);
                    }
                    await File.WriteAllTextAsync(absoluteFilePath, newContent);
                }
            }

            layer.IsDirty = false;
        }

        /// <summary>
        /// Creates a plan that maps each file to the single top-level property it should contain.
        /// </summary>
        private Dictionary<string, DomNode> BuildSavePlan(CascadeLayer layer)
        {
            var plan = new Dictionary<string, DomNode>();

            foreach (var topLevelNode in layer.LayerConfigRootNode.GetChildren())
            {
                string nodePath = topLevelNode.Path;
                const string rootPrefix = "$root/";

                // Check for and remove the internal "$root/" prefix
                if (nodePath.StartsWith(rootPrefix))
                {
                    nodePath = nodePath.Substring(rootPrefix.Length);
                }

                // The remaining path is the correct relative file path
                string relativeFilePath = $"{nodePath}.json";
                plan[relativeFilePath] = topLevelNode;
            }
            return plan;
        }
    }
}



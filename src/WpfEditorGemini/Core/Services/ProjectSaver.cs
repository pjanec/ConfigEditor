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
        public async Task SaveLayerAsync(CascadeLayer layer)
        {
            if (!layer.IsDirty)
            {
                return;
            }

            var savePlan = BuildSavePlan(layer);

            foreach (var (relativeFilePath, nodeToSave) in savePlan)
            {
                var originalFile = layer.SourceFiles.FirstOrDefault(f => f.RelativePath.Equals(relativeFilePath, StringComparison.OrdinalIgnoreCase));
                
                // The serializer correctly handles writing the contents of the object node.
                string newContent = _serializer.SerializeToString(nodeToSave, indented: true);

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

            foreach (var fileToDelete in layer.FilesToDeleteOnSave)
            {
                var absolutePath = Path.Combine(layer.FolderPath, fileToDelete);
                if (File.Exists(absolutePath))
                {
                    try
                    {
                        File.Delete(absolutePath);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Failed to delete consolidated file '{absolutePath}': {ex.Message}");
                    }
                }
            }
            layer.FilesToDeleteOnSave.Clear();
            layer.IsDirty = false;
        }

        /// <summary>
        /// Creates a plan that maps each file to an ObjectNode containing its correct content.
        /// This is done by finding the corresponding "mount node" for each file in the merged DOM
        /// and copying only the children that originated from that specific file.
        /// </summary>
        private Dictionary<string, ObjectNode> BuildSavePlan(CascadeLayer layer)
        {
            var plan = new Dictionary<string, ObjectNode>();

            // Get a list of all unique files that contribute to the layer's content.
            var filesToProcess = new HashSet<string>(layer.IntraLayerValueOrigins.Values);
            // Also include original source files that might have become empty, to ensure they are overwritten.
            foreach (var sourceFile in layer.SourceFiles)
            {
                filesToProcess.Add(sourceFile.RelativePath);
            }

            foreach (var relativeFilePath in filesToProcess)
            {
                var fileContentRoot = new ObjectNode("$file_root", null);

                // Determine the file's mount point path from its name (e.g., "sub1/base.json" -> "$root/sub1/base").
                var pathWithoutExt = relativeFilePath.Replace(".json", "", StringComparison.OrdinalIgnoreCase).Replace('\\', '/');
                var mountPath = "$root/" + pathWithoutExt;

                // Find the node in the merged layer DOM that corresponds to this file's content root.
                var fileMountNode = FindNodeByPath(layer.LayerConfigRootNode, mountPath);

                if (fileMountNode is ObjectNode mountObject)
                {
                    // Copy children from the mount node into our new file root,
                    // but only if they originated from the current file.
                    foreach (var child in mountObject.GetChildren())
                    {
                        if (layer.IntraLayerValueOrigins.TryGetValue(child.Path, out var originFile) && originFile == relativeFilePath)
                        {
                            var clonedChild = DomCloning.CloneNode(child, fileContentRoot);
                            fileContentRoot.AddChild(clonedChild.Name, clonedChild);
                        }
                    }
                }
                
                plan[relativeFilePath] = fileContentRoot;
            }
            return plan;
        }

        /// <summary>
        /// Finds a node in a DOM tree by its full path.
        /// </summary>
        private DomNode? FindNodeByPath(DomNode rootNode, string path)
        {
            if (rootNode.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                return rootNode;
            }

            if (rootNode is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    if (path.StartsWith(child.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        var found = FindNodeByPath(child, path);
                        if (found != null) return found;
                    }
                }
            }
            else if (rootNode is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    if (path.StartsWith(item.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        var found = FindNodeByPath(item, path);
                        if (found != null) return found;
                    }
                }
            }

            return null;
        }
    }
}

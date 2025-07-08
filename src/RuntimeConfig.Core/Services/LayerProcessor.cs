using RuntimeConfig.Core.Dom;
using RuntimeConfig.Core.Models;
using RuntimeConfig.Core.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RuntimeConfig.Core.Services
{
    public class LayerProcessor
    {
        private readonly JsonDomDeserializer _deserializer;

        public LayerProcessor()
        {
            _deserializer = new JsonDomDeserializer();
        }

        public LayerProcessingResult Process(string layerBasePath, int layerIndex)
        {
            var errors = new List<string>();
            var sourceFiles = LoadAllFilesFromLayerFolder(layerBasePath, layerIndex, errors);

            var rootNode = new ObjectNode("$root", null);
            var origins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sourceFile in sourceFiles.OrderBy(f => f.RelativePath))
            {
                if (sourceFile.DomRoot is not ObjectNode sourceFileRoot)
                {
                    errors.Add($"Source file '{sourceFile.RelativePath}' in layer '{layerBasePath}' does not have a root JSON object and will be ignored.");
                    continue;
                }

                var targetNode = EnsurePathAndGetTarget(rootNode, sourceFile.RelativePath);
                MergeNodeRecursive(targetNode, sourceFileRoot, sourceFile, origins, errors);
            }

            return new LayerProcessingResult
            {
                MergedRootNode = rootNode,
                LoadedSourceFiles = sourceFiles,
                ValueOrigins = origins,
                Errors = errors
            };
        }

        // Logic moved from WpfEditorGemini.Core.Services.ProjectLoader
        private List<SourceFileInfo> LoadAllFilesFromLayerFolder(string absoluteLayerPath, int layerIndex, List<string> errors)
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
                    var fileContent = File.ReadAllText(filePath);
                    // Use the new deserializer from RuntimeConfig.Core
                    var domRoot = _deserializer.FromJson(fileContent);
                    
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

        // Logic moved from WpfEditorGemini.Core.Services.IntraLayerMerger
        private ObjectNode EnsurePathAndGetTarget(ObjectNode layerRoot, string relativePath)
        {
            var pathWithoutExtension = relativePath.Replace(".json", "", StringComparison.OrdinalIgnoreCase);
            var segments = pathWithoutExtension.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            ObjectNode currentParent = layerRoot;
            foreach (var segment in segments)
            {
                var childNode = currentParent.GetChild(segment);
                if (childNode is ObjectNode existingObject)
                {
                    currentParent = existingObject;
                }
                else
                {
                    var newNode = new ObjectNode(segment, currentParent);
                    currentParent.AddChild(segment, newNode);
                    currentParent = newNode;
                }
            }
            return currentParent;
        }

        // Logic moved from WpfEditorGemini.Core.Services.IntraLayerMerger
        private void MergeNodeRecursive(ObjectNode targetParent, ObjectNode sourceNode, SourceFileInfo sourceFile, Dictionary<string, string> origins, List<string> errors)
        {
            foreach (var (childKey, childNode) in sourceNode.Children)
            {
                if (targetParent.Children.TryGetValue(childKey, out var existingNode))
                {
                    if (existingNode is ObjectNode existingObject && childNode is ObjectNode childObject)
                    {
                        MergeNodeRecursive(existingObject, childObject, sourceFile, origins, errors);
                    }
                    else
                    {
                        var originalSourcePath = origins.GetValueOrDefault(existingNode.Path, "unknown file");
                        errors.Add($"Overlap detected for property '{existingNode.Path}'. It is defined in both '{originalSourcePath}' and '{sourceFile.RelativePath}'.");
                    }
                }
                else
                {
                    var clonedNode = DomTree.CloneNode(childNode, targetParent);
                    targetParent.AddChild(clonedNode.Name, clonedNode);
                    TrackOriginsRecursive(clonedNode, sourceFile.RelativePath, origins);
                }
            }
        }

        // Logic moved from WpfEditorGemini.Core.Services.IntraLayerMerger
        private void TrackOriginsRecursive(DomNode node, string relativeSourcePath, Dictionary<string, string> origins)
        {
            origins[node.Path] = relativeSourcePath;
            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    TrackOriginsRecursive(child, relativeSourcePath, origins);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.Items)
                {
                    TrackOriginsRecursive(item, relativeSourcePath, origins);
                }
            }
        }
    }
} 
using JsonConfigEditor.Core.Cascade;
using JsonConfigEditor.Core.Validation; // Add this
using JsonConfigEditor.Core.SchemaLoading; // Add this for ISchemaLoaderService
using System;
using System.Collections.Generic;
using System.Linq;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// Defines the types of integrity checks that can be performed on a project.
    /// </summary>
    [Flags]
    public enum IntegrityCheckType
    {
        None = 0,
        FilePathConsistency = 1,
        OverlappingDefinitions = 2,
        SchemaCompliance = 4,
        PropertyNameCasing = 8,
        EmptyFilesOrFolders = 16,
        // Add the new check type below
        FileSystemSchemaCasing = 32,
        All = FilePathConsistency | OverlappingDefinitions | SchemaCompliance | PropertyNameCasing | EmptyFilesOrFolders | FileSystemSchemaCasing
    }

    /// <summary>
    /// Represents a single warning or informational issue found during an integrity check.
    /// </summary>
    public record IntegrityIssue(ValidationSeverity Severity, string Message, string LayerName, string? DomPath = null, string? FilePath = null);

    /// <summary>
    /// Performs various integrity and consistency checks across a full cascade project.
    /// </summary>
    public class IntegrityChecker
    {
        /// <summary>
        /// Runs a series of selected checks against the loaded cascade layers.
        /// </summary>
        /// <param name="allLayers">The complete, ordered list of cascade layers.</param>
        /// <param name="schemaLoader">The schema loader service for schema-related checks.</param>
        /// <param name="checksToRun">A flag enum specifying which checks to perform.</param>
        /// <returns>A list of all found integrity issues.</returns>
        public List<IntegrityIssue> RunChecks(IReadOnlyList<CascadeLayer> allLayers, ISchemaLoaderService schemaLoader, IntegrityCheckType checksToRun)
        {
            var issues = new List<IntegrityIssue>();

            if (checksToRun.HasFlag(IntegrityCheckType.FilePathConsistency))
            {
                issues.AddRange(CheckFilePathConsistency(allLayers));
            }
            // Add stubs for other checks
            if (checksToRun.HasFlag(IntegrityCheckType.OverlappingDefinitions))
            {
                // This check is already performed by IntraLayerMerger on load,
                // but could be re-run here for an explicit check.
            }
            if (checksToRun.HasFlag(IntegrityCheckType.PropertyNameCasing))
            {
                issues.AddRange(CheckPropertyNameCasing(allLayers));
            }
            // Add this block to call the new checker
            if (checksToRun.HasFlag(IntegrityCheckType.FileSystemSchemaCasing))
            {
                issues.AddRange(CheckFileSystemVsSchemaCasing(allLayers, schemaLoader));
            }

            return issues;
        }

        /// <summary>
        /// Checks if a configuration section (e.g., /database) is consistently housed
        /// in a file with the same relative path across all layers where it is defined.
        /// </summary>
        private List<IntegrityIssue> CheckFilePathConsistency(IReadOnlyList<CascadeLayer> allLayers)
        {
            var issues = new List<IntegrityIssue>();
            // Key: DOM Path (e.g., "/database"). Value: Tuple of (Canonical Relative File Path, Canonical Layer Name)
            var canonicalPaths = new Dictionary<string, (string FilePath, string LayerName)>();

            foreach (var layer in allLayers)
            {
                // We only care about the origins of top-level nodes in each layer
                var topLevelPaths = layer.LayerConfigRootNode.GetChildren().Select(c => c.Path);

                foreach (var path in topLevelPaths)
                {
                    if (layer.IntraLayerValueOrigins.TryGetValue(path, out var currentRelativePath))
                    {
                        if (canonicalPaths.TryGetValue(path, out var canonical))
                        {
                            // Path has been seen before. Check if file path is consistent.
                            if (canonical.FilePath != currentRelativePath)
                            {
                                issues.Add(new IntegrityIssue(
                                    ValidationSeverity.Warning, // Specify a severity
                                    $"Path inconsistency for '{path}'. It is defined in '{canonical.FilePath}' in the '{canonical.LayerName}' layer, but in '{currentRelativePath}' in the '{layer.Name}' layer.",
                                    layer.Name,
                                    path
                                ));
                            }
                        }
                        else
                        {
                            // First time we've seen this path. Establish it as the canonical standard.
                            canonicalPaths[path] = (currentRelativePath, layer.Name);
                        }
                    }
                }
            }
            return issues;
        }

        /// <summary>
        /// Checks for properties with the same case-insensitive name but different casing.
        /// For example, warns if one layer uses 'timeout' and another uses 'timeOut'.
        /// </summary>
        private List<IntegrityIssue> CheckPropertyNameCasing(IReadOnlyList<CascadeLayer> allLayers)
        {
            var issues = new List<IntegrityIssue>();
            // Key: Case-insensitive DOM Path. Value: Tuple of (Original Cased Path, Original Layer Name)
            var canonicalCasing = new Dictionary<string, (string CasedPath, string LayerName)>(StringComparer.OrdinalIgnoreCase);

            foreach (var layer in allLayers)
            {
                foreach (var path in layer.IntraLayerValueOrigins.Keys)
                {
                    if (canonicalCasing.TryGetValue(path, out var canonical))
                    {
                        // A path with the same case-insensitive name exists. Check if the casing is different.
                        if (string.CompareOrdinal(path, canonical.CasedPath) != 0)
                        {
                             issues.Add(new IntegrityIssue(
                                ValidationSeverity.Warning, // Specify a severity
                                $"Property name casing mismatch for '{path}'. It is defined as '{canonical.CasedPath}' in the '{canonical.LayerName}' layer, but as '{path}' in the '{layer.Name}' layer.",
                                layer.Name,
                                path
                            ));
                        }
                    }
                    else
                    {
                        // First time seeing this path (case-insensitively). Record its exact casing.
                        canonicalCasing[path] = (path, layer.Name);
                    }
                }
            }

            return issues;
        }

        /// <summary>
        /// Checks if file and folder names in the file system match the casing of their corresponding schema properties.
        /// </summary>
        public List<IntegrityIssue> CheckFileSystemVsSchemaCasing(IReadOnlyList<CascadeLayer> allLayers, ISchemaLoaderService schemaLoader)
        {
            var issues = new List<IntegrityIssue>();
            var rootSchema = schemaLoader.GetRootSchema();
            if (rootSchema == null) return issues; // No schema to check against

            foreach (var layer in allLayers)
            {
                foreach (var sourceFile in layer.SourceFiles)
                {
                    var pathWithoutExtension = sourceFile.RelativePath.Replace(".json", "", StringComparison.OrdinalIgnoreCase);
                    var segments = pathWithoutExtension.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

                    var currentSchemaNode = rootSchema;
                    var currentDomPath = "$root";

                    foreach (var segment in segments)
                    {
                        // Cannot check casing if the path doesn't exist in the schema. This is a different error type.
                        if (currentSchemaNode.Properties == null || !currentSchemaNode.Properties.TryGetValue(segment, out var childSchema))
                        {
                            break;
                        }

                        currentDomPath += "/" + segment;

                        // Ordinal compare: "services" != "Services"
                        if (!string.Equals(segment, childSchema.Name, StringComparison.Ordinal))
                        {
                            issues.Add(new IntegrityIssue(
                                ValidationSeverity.Warning,
                                $"Path segment '{segment}' in file path '{sourceFile.RelativePath}' should be cased as '{childSchema.Name}' to match the schema.",
                                layer.Name,
                                currentDomPath,
                                sourceFile.RelativePath
                            ));
                        }
                          
                        currentSchemaNode = childSchema;
                    }
                }
            }
            return issues;
        }
    }
}



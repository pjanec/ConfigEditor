using JsonConfigEditor.Core.Cascade;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// Analyzes the structure of a cascade project to find opportunities for improvement,
    /// such as consolidating overlapping files within a layer.
    /// </summary>
    public class ProjectStructureAnalyzer
    {
        /// <summary>
        /// Scans a single layer and proposes actions to consolidate files.
        /// </summary>
        /// <param name="layer">The cascade layer to analyze.</param>
        /// <returns>A list of proposed consolidation actions for the layer.</returns>
        public List<ConsolidationAction> ProposeConsolidations(CascadeLayer layer)
        {
            var proposedActions = new List<ConsolidationAction>();
            var origins = layer.IntraLayerValueOrigins;
            var filePathsInLayer = origins.Values.Distinct().ToList();

            foreach (var descendantPath in filePathsInLayer)
            {
                // Example: descendantPath is "database/auditing.json"
                var tempPath = Path.GetDirectoryName(descendantPath)?.Replace('\\', '/');

                while (!string.IsNullOrEmpty(tempPath))
                {
                    // Example: ancestorPath becomes "database.json"
                    var ancestorPath = $"{tempPath}.json";

                    if (filePathsInLayer.Contains(ancestorPath))
                    {
                        // A consolidation opportunity is found!
                        var propertyPath = "/" + Path.GetDirectoryName(descendantPath)!.Replace('\\', '/');

                        proposedActions.Add(new ConsolidationAction(
                            ancestorPath, 
                            descendantPath, 
                            propertyPath, 
                            layer.Name));

                        // Found the highest-level conflict, no need to check further up this path.
                        break; 
                    }
                    tempPath = Path.GetDirectoryName(tempPath)?.Replace('\\', '/');
                }
            }
            return proposedActions;
        }
    }
}

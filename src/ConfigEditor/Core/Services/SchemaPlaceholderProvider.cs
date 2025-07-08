using RuntimeConfig.Core.Dom;
using RuntimeConfig.Core.Schema;
using JsonConfigEditor.ViewModels;
using System.Collections.Generic;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// A service responsible for generating "schema-only" placeholder ViewModels.
    /// It compares a DOM object node with its corresponding schema to find properties
    /// that are defined in the schema but are not yet present in the data, and creates
    /// placeholder UI items for them.
    /// </summary>
    public class SchemaPlaceholderProvider
    {
        /// <summary>
        /// Generates a list of placeholder DataGridRowItemViewModels for properties
        /// that are defined in the schema but missing from the DOM node.
        /// </summary>
        /// <param name="parentDomNode">The ObjectNode from the data model.</param>
        /// <param name="parentSchema">The corresponding SchemaNode for the data node.</param>
        /// <param name="mainViewModel">The MainViewModel, required for the placeholder's constructor.</param>
        /// <returns>A list of placeholder ViewModels to be added to the UI.</returns>
        public List<DataGridRowItemViewModel> GetPlaceholders(
            ObjectNode parentDomNode,
            SchemaNode parentSchema,
            MainViewModel mainViewModel)
        {
            var placeholders = new List<DataGridRowItemViewModel>();

            // If the schema has no properties defined, there's nothing to do.
            if (parentSchema.Properties == null)
            {
                return placeholders;
            }

            // Iterate through every property defined in the schema.
            foreach (var schemaProp in parentSchema.Properties)
            {
                // Check if a child with the same name already exists in the actual data node.
                if (!parentDomNode.HasProperty(schemaProp.Key))
                {
                    // If it doesn't exist, a placeholder is needed.
                    var childDepth = parentDomNode.Depth + 1;
                    
                    // Construct the unique path key for this placeholder.
                    var schemaPathKey = string.IsNullOrEmpty(parentDomNode.Path)
                        ? schemaProp.Key
                        : $"{parentDomNode.Path}/{schemaProp.Key}";
                    
                    // Create the special "schema-only" ViewModel.
                    var schemaOnlyChildVm = new DataGridRowItemViewModel(
                        schemaPropertyNode: schemaProp.Value,
                        propertyName: schemaProp.Key,
                        parentViewModel: mainViewModel,
                        depth: childDepth,
                        pathKey: schemaPathKey
                    );
                    
                    placeholders.Add(schemaOnlyChildVm);
                }
            }

            return placeholders;
        }
    }
} 
using System;
using System.Collections.Generic;

namespace ConfigDom
{
    /// <summary>
    /// Provides schema binding functionality by walking the DOM viewmodel tree
    /// and attaching ISchemaNode metadata to each compatible node.
    /// </summary>
    public static class SchemaBinder
    {
        /// <summary>
        /// Recursively assigns schema metadata to the corresponding viewmodel tree.
        /// Logs warnings for mismatches if verbose = true.
        /// </summary>
        public static void BindSchemaTree(DomNodeViewModel viewModel, ISchemaNode schemaRoot, bool verbose = false)
        {
            viewModel.Schema = schemaRoot;

            if (schemaRoot is ObjectSchemaNode objSchema)
            {
                foreach (var child in viewModel.Children)
                {
                    if (child.Node is ObjectNode or LeafNode)
                    {
                        if (objSchema.ChildrenByName.TryGetValue(child.Node.Name, out var matchingSchema))
                        {
                            BindSchemaTree(child, matchingSchema, verbose);
                        }
                        else if (verbose)
                        {
                            Console.WriteLine($"[SchemaBinder] No schema for child: {child.Path}");
                        }
                    }
                }
            }
            else if (schemaRoot is ArraySchemaNode arraySchema)
            {
                for (int i = 0; i < viewModel.Children.Count; i++)
                {
                    BindSchemaTree(viewModel.Children[i], arraySchema.ItemSchema, verbose);
                }
            }
        }
    }
}

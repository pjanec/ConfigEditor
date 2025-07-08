using RuntimeConfig.Core.Dom;
using RuntimeConfig.Core.Schema;
using JsonConfigEditor.ViewModels;
using System.Collections.Generic;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// A service responsible for building the flat list of DataGridRowItemViewModels
    /// from a hierarchical DomNode tree. It encapsulates the complex recursive logic
    /// for traversing the tree, creating ViewModels, and generating schema placeholders.
    /// </summary>
    public class ViewModelBuilderService
    {
        private readonly SchemaPlaceholderProvider _placeholderProvider;
        private readonly MainViewModel _mainViewModel;

        // Dependencies are passed in for loose coupling.
        public ViewModelBuilderService(SchemaPlaceholderProvider placeholderProvider, MainViewModel mainViewModel)
        {
            _placeholderProvider = placeholderProvider;
            _mainViewModel = mainViewModel;
        }

        /// <summary>
        /// The main public method to generate the flat list for the UI.
        /// </summary>
        /// <param name="rootNode">The root of the DomNode tree to display.</param>
        /// <param name="persistentVmMap">A map to look up existing VMs and preserve their state (e.g., IsExpanded).</param>
        /// <param name="domToSchemaMap">A map to find the correct schema for each DomNode.</param>
        /// <param name="valueOrigins">A map from DOM path to the index of the layer that provided its final, effective value.</param>
        /// <param name="showSchemaNodes">A flag indicating whether to generate schema-only placeholders.</param>
        /// <param name="visiblePaths">Optional set of visible paths for filtering.</param>
        /// <returns>A complete, flat list of DataGridRowItemViewModels for the DataGrid.</returns>
        public List<DataGridRowItemViewModel> BuildList(
            DomNode rootNode,
            Dictionary<string, DataGridRowItemViewModel> persistentVmMap,
            Dictionary<string, SchemaNode?> domToSchemaMap,
            Dictionary<string, int> valueOrigins,
            bool showSchemaNodes,
            HashSet<string>? visiblePaths = null)
        {
            var flatList = new List<DataGridRowItemViewModel>();
            
            if (visiblePaths != null)
            {
                BuildFilteredListRecursive(rootNode, flatList, persistentVmMap, domToSchemaMap, valueOrigins, showSchemaNodes, visiblePaths);
            }
            else
            {
                BuildListRecursive(rootNode, flatList, persistentVmMap, domToSchemaMap, valueOrigins, showSchemaNodes);
                
                // Add root schema nodes if needed (this was in the original implementation)
                if (showSchemaNodes && _mainViewModel.SchemaLoader?.RootSchemas != null)
                {
                    var primaryRootSchema = _mainViewModel.SchemaLoader.GetRootSchema();

                    foreach (var schemaEntry in _mainViewModel.SchemaLoader.RootSchemas)
                    {
                        string mountPath = schemaEntry.Key;
                        SchemaNode schemaRoot = schemaEntry.Value;

                        if (schemaRoot == primaryRootSchema && string.IsNullOrEmpty(mountPath))
                        {
                            continue;
                        }

                        DomNode? existingDomForMountPath = _mainViewModel.FindDomNodeByPath(mountPath);
                        if (existingDomForMountPath != null)
                        {
                            continue;
                        }

                        if (flatList.Any(vm => vm.IsSchemaOnlyNode && vm.SchemaNodePathKey == mountPath && vm.Indentation.Left == 20))
                        {
                            continue;
                        }

                        int depth = 1;
                        string propertyName = schemaRoot.Name;

                        var rootSchemaVm = new DataGridRowItemViewModel(schemaRoot, propertyName, _mainViewModel, depth, mountPath);
                        flatList.Add(rootSchemaVm);
                        if (rootSchemaVm.IsExpanded && rootSchemaVm.IsExpandable)
                        {
                            AddSchemaOnlyChildrenRecursive(rootSchemaVm, flatList, persistentVmMap, domToSchemaMap, valueOrigins);
                        }
                    }
                }
            }
            
            return flatList;
        }

        /// <summary>
        /// The core recursive method that traverses the DOM tree and builds the flat list.
        /// This method was moved directly from MainViewModel.
        /// </summary>
        private void BuildListRecursive(
            DomNode node,
            List<DataGridRowItemViewModel> flatItems,
            Dictionary<string, DataGridRowItemViewModel> persistentVmMap,
            Dictionary<string, SchemaNode?> domToSchemaMap,
            Dictionary<string, int> valueOrigins,
            bool showSchemaNodes)
        {
            // Try to find an existing ViewModel for this node's path to preserve its state.
            // But only reuse it if it's a DOM node ViewModel (not a schema-only one).
            if (!persistentVmMap.TryGetValue(node.Path, out var viewModel) || !viewModel.IsDomNodePresent)
            {
                // If none exists or it's not a DOM node ViewModel, create a new one.
                domToSchemaMap.TryGetValue(node.Path, out var schema);
                
                // Debug output
                System.Diagnostics.Debug.WriteLine($"ViewModelBuilder - Creating VM for node: {node.Path}, Schema found: {schema?.Name}, Schema Type: {schema?.ClrType?.Name}");
                
                // FALLBACK: If schema not found in domToSchemaMap, try to find it from the schema loader
                if (schema == null && !string.IsNullOrEmpty(node.Path))
                {
                    schema = _mainViewModel.SchemaLoader?.FindSchemaForPath(node.Path);
                    if (schema != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"ViewModelBuilder - FALLBACK: Found schema for {node.Path}: {schema.Name}, Type: {schema.ClrType?.Name}");
                    }
                }
                
                // Get the origin layer index for this node's path. Default to -1 if not found.
                valueOrigins.TryGetValue(node.Path, out int originIndex);
                
                // Pass the origin index to the constructor.
                viewModel = new DataGridRowItemViewModel(node, schema, _mainViewModel, originIndex);
            }
            flatItems.Add(viewModel);

            // If the node is expanded, recurse into its children and add placeholders.
            if (viewModel.IsExpanded)
            {
                if (node is ObjectNode objectNode)
                {
                    // 1. Create a single, combined list of all potential children (both real and placeholder).
                    var combinedChildrenVms = new List<DataGridRowItemViewModel>();

                    // 2. Add view models for real DOM children to the list.
                    foreach (var childDomNode in objectNode.GetChildren())
                    {
                        if (!persistentVmMap.TryGetValue(childDomNode.Path, out var childVm) || !childVm.IsDomNodePresent)
                        {
                            domToSchemaMap.TryGetValue(childDomNode.Path, out var schema1);
                            valueOrigins.TryGetValue(childDomNode.Path, out int originIndex);
                            childVm = new DataGridRowItemViewModel(childDomNode, schema1, _mainViewModel, originIndex);
                        }
                        combinedChildrenVms.Add(childVm);
                    }

                    // 3. Add view models for schema-only placeholders to the same list.
                    if (showSchemaNodes && domToSchemaMap.TryGetValue(objectNode.Path, out var schema) && schema?.NodeType == SchemaNodeType.Object)
                    {
                        var placeholders = _placeholderProvider.GetPlaceholders(objectNode, schema, _mainViewModel);
                        combinedChildrenVms.AddRange(placeholders);
                    }

                    // 4. Sort the combined list alphabetically by the displayed node name.
                    var sortedChildren = combinedChildrenVms.OrderBy(vm => vm.NodeName).ToList();

                    // 5. Process the sorted children.
                    foreach (var childVm in sortedChildren)
                    {
                        if (childVm.IsDomNodePresent && childVm.DomNode != null)
                        {
                            // For real nodes, we simply make the recursive call.
                            // That call itself is responsible for adding the item to the flat list.
                            // This prevents the duplication.
                            BuildListRecursive(childVm.DomNode, flatItems, persistentVmMap, domToSchemaMap, valueOrigins, showSchemaNodes);
                        }
                        else if (childVm.IsSchemaOnlyNode)
                        {
                            // For schema-only placeholders, there is no DomNode to recurse on.
                            // We must add the placeholder VM directly, and then manually handle
                            // adding its children if it's expanded.
                            flatItems.Add(childVm);
                            if (childVm.IsExpanded)
                            {
                                AddSchemaOnlyChildrenRecursive(childVm, flatItems, persistentVmMap, domToSchemaMap, valueOrigins);
                            }
                        }
                    }
                }
                else if (node is ArrayNode arrayNode)
                {
                    foreach (var itemDomNode in arrayNode.GetItems())
                    {
                        // Pass the maps down in the recursive call
                        BuildListRecursive(itemDomNode, flatItems, persistentVmMap, domToSchemaMap, valueOrigins, showSchemaNodes);
                    }
                    // Add the "Add new item" placeholder for arrays.
                    if (domToSchemaMap.TryGetValue(arrayNode.Path, out var schema))
                    {
                        flatItems.Add(new DataGridRowItemViewModel(arrayNode, schema?.ItemSchema, _mainViewModel, arrayNode.Depth + 1));
                    }
                }
            }
        }

        /// <summary>
        /// Builds a filtered list based on visible paths.
        /// This method was moved from MainViewModel to handle filtering.
        /// </summary>
        private void BuildFilteredListRecursive(
            DomNode node,
            List<DataGridRowItemViewModel> flatItems,
            Dictionary<string, DataGridRowItemViewModel> persistentVmMap,
            Dictionary<string, SchemaNode?> domToSchemaMap,
            Dictionary<string, int> valueOrigins,
            bool showSchemaNodes,
            HashSet<string> visiblePaths)
        {
            // If the set of visible paths does not contain this node's path, skip it.
            if (!visiblePaths.Contains(node.Path))
            {
                return;
            }

            if (!persistentVmMap.TryGetValue(node.Path, out var viewModel) || !viewModel.IsDomNodePresent)
            {
                domToSchemaMap.TryGetValue(node.Path, out var schema);
                // Get the origin layer index for this node's path. Default to -1 if not found.
                valueOrigins.TryGetValue(node.Path, out int originIndex);
                viewModel = new DataGridRowItemViewModel(node, schema, _mainViewModel, originIndex);
            }

            // Force expansion for all nodes that are part of the revealed path
            viewModel.SetExpansionStateInternal(true);

            flatItems.Add(viewModel);

            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren().OrderBy(c => c.Name))
                {
                    BuildFilteredListRecursive(child, flatItems, persistentVmMap, domToSchemaMap, valueOrigins, showSchemaNodes, visiblePaths);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    BuildFilteredListRecursive(item, flatItems, persistentVmMap, domToSchemaMap, valueOrigins, showSchemaNodes, visiblePaths);
                }
            }
        }

        /// <summary>
        /// Recursively adds children for an expanded schema-only placeholder.
        /// This method was also moved directly from MainViewModel.
        /// </summary>
        private void AddSchemaOnlyChildrenRecursive(
            DataGridRowItemViewModel parentVm,
            List<DataGridRowItemViewModel> flatItems,
            Dictionary<string, DataGridRowItemViewModel> persistentVmMap,
            Dictionary<string, SchemaNode?> domToSchemaMap,
            Dictionary<string, int> valueOrigins)
        {
            if (parentVm.SchemaContextNode?.Properties == null) return;
              
            int parentDepth = parentVm.DomNode?.Depth ?? (int)(parentVm.Indentation.Left / 20);

            // Add sorting here to ensure children are displayed alphabetically
            foreach (var propEntry in parentVm.SchemaContextNode.Properties.OrderBy(pe => pe.Key))
            {
                var childDepth = parentDepth + 1;
                var childSchemaPathKey = $"{parentVm.SchemaNodePathKey}/{propEntry.Key}";

                // Try to get a persistent VM to maintain expansion state.
                if (!persistentVmMap.TryGetValue(childSchemaPathKey, out var childSchemaVm))
                {
                     childSchemaVm = new DataGridRowItemViewModel(propEntry.Value, propEntry.Key, _mainViewModel, childDepth, childSchemaPathKey);
                }

                flatItems.Add(childSchemaVm);
                if (childSchemaVm.IsExpanded && childSchemaVm.IsExpandable)
                {
                    AddSchemaOnlyChildrenRecursive(childSchemaVm, flatItems, persistentVmMap, domToSchemaMap, valueOrigins);
                }
            }
        }
    }
} 
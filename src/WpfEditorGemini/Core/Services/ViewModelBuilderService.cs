using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.Core.Schema;
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
        /// <param name="showSchemaNodes">A flag indicating whether to generate schema-only placeholders.</param>
        /// <param name="visiblePaths">Optional set of visible paths for filtering.</param>
        /// <returns>A complete, flat list of DataGridRowItemViewModels for the DataGrid.</returns>
        public List<DataGridRowItemViewModel> BuildList(
            DomNode rootNode,
            Dictionary<string, DataGridRowItemViewModel> persistentVmMap,
            Dictionary<string, SchemaNode?> domToSchemaMap,
            bool showSchemaNodes,
            HashSet<string>? visiblePaths = null)
        {
            var flatList = new List<DataGridRowItemViewModel>();
            
            if (visiblePaths != null)
            {
                BuildFilteredListRecursive(rootNode, flatList, persistentVmMap, domToSchemaMap, showSchemaNodes, visiblePaths);
            }
            else
            {
                BuildListRecursive(rootNode, flatList, persistentVmMap, domToSchemaMap, showSchemaNodes);
                
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
                            AddSchemaOnlyChildrenRecursive(rootSchemaVm, flatList, persistentVmMap, domToSchemaMap);
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
            bool showSchemaNodes)
        {
            // Try to find an existing ViewModel for this node's path to preserve its state.
            // But only reuse it if it's a DOM node ViewModel (not a schema-only one).
            if (!persistentVmMap.TryGetValue(node.Path, out var viewModel) || !viewModel.IsDomNodePresent)
            {
                // If none exists or it's not a DOM node ViewModel, create a new one.
                domToSchemaMap.TryGetValue(node.Path, out var schema);
                viewModel = new DataGridRowItemViewModel(node, schema, _mainViewModel);
            }
            flatItems.Add(viewModel);

            // If the node is expanded, recurse into its children and add placeholders.
            if (viewModel.IsExpanded)
            {
                if (node is ObjectNode objectNode)
                {
                    foreach (var childDomNode in objectNode.GetChildren())
                    {
                        BuildListRecursive(childDomNode, flatItems, persistentVmMap, domToSchemaMap, showSchemaNodes);
                    }

                    // Use the placeholder provider to get schema-only nodes.
                    if (showSchemaNodes && domToSchemaMap.TryGetValue(objectNode.Path, out var schema) && schema?.NodeType == SchemaNodeType.Object)
                    {
                        var placeholders = _placeholderProvider.GetPlaceholders(objectNode, schema, _mainViewModel);
                        flatItems.AddRange(placeholders);

                        foreach (var p in placeholders)
                        {
                            if (p.IsExpanded && p.IsExpandable)
                            {
                                AddSchemaOnlyChildrenRecursive(p, flatItems, persistentVmMap, domToSchemaMap);
                            }
                        }
                    }
                }
                else if (node is ArrayNode arrayNode)
                {
                    foreach (var itemDomNode in arrayNode.GetItems())
                    {
                        BuildListRecursive(itemDomNode, flatItems, persistentVmMap, domToSchemaMap, showSchemaNodes);
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
                viewModel = new DataGridRowItemViewModel(node, schema, _mainViewModel);
            }

            // Force expansion for all nodes that are part of the revealed path
            viewModel.SetExpansionStateInternal(true);

            flatItems.Add(viewModel);

            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren().OrderBy(c => c.Name))
                {
                    BuildFilteredListRecursive(child, flatItems, persistentVmMap, domToSchemaMap, showSchemaNodes, visiblePaths);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    BuildFilteredListRecursive(item, flatItems, persistentVmMap, domToSchemaMap, showSchemaNodes, visiblePaths);
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
            Dictionary<string, SchemaNode?> domToSchemaMap)
        {
            if (parentVm.SchemaContextNode?.Properties == null) return;
              
            int parentDepth = parentVm.DomNode?.Depth ?? (int)(parentVm.Indentation.Left / 20);

            foreach (var propEntry in parentVm.SchemaContextNode.Properties)
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
                    AddSchemaOnlyChildrenRecursive(childSchemaVm, flatItems, persistentVmMap, domToSchemaMap);
                }
            }
        }
    }
} 
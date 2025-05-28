using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.Core.Parsing;
using JsonConfigEditor.Core.Schema;
using JsonConfigEditor.Core.SchemaLoading;
using JsonConfigEditor.Core.Serialization;
using JsonConfigEditor.Core.Validation;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using JsonConfigEditor.Wpf.Services;

namespace JsonConfigEditor.ViewModels
{
    /// <summary>
    /// The main ViewModel for the application. Orchestrates interactions, data flow,
    /// manages DOM, schema, validation, undo/redo, filtering, search, and UI state.
    /// (From specification document, various sections)
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        // --- Private Fields: Services ---
        private readonly IJsonDomParser _jsonParser;
        private readonly IDomNodeToJsonSerializer _jsonSerializer;
        private readonly ISchemaLoaderService _schemaLoader;
        private readonly ValidationService _validationService;
        private readonly CustomUIRegistryService _uiRegistry;

        // --- Private Fields: Core Data State ---
        private DomNode? _rootDomNode;
        private readonly Dictionary<DomNode, SchemaNode?> _domToSchemaMap = new();
        private readonly Dictionary<DomNode, List<ValidationIssue>> _validationIssuesMap = new();
        private readonly Dictionary<DomNode, DataGridRowItemViewModel> _persistentVmMap = new();

        // --- Private Fields: UI State & Filters/Search ---
        private string? _currentFilePath;
        private bool _isDirty;
        private string _filterText = string.Empty;
        private bool _showOnlyInvalidNodes;
        private bool _showSchemaNodes = true; // Toggle for DOM vs DOM+Schema view
        private string _searchText = string.Empty;
        private DataGridRowItemViewModel? _currentlyEditedItem;
        private List<SearchResult> _searchResults = new();
        private int _currentSearchIndex = -1;

        // --- Private Fields: Undo/Redo ---
        private readonly Stack<EditOperation> _undoStack = new();
        private readonly Stack<EditOperation> _redoStack = new();

        // --- Commands ---
        public ICommand NewFileCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand SaveAsFileCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand FocusSearchCommand { get; }
        public ICommand FindNextCommand { get; }
        public ICommand FindPreviousCommand { get; }
        public ICommand LoadSchemaCommand { get; }

        // --- Public Properties ---

        /// <summary>
        /// Gets the registry for custom UI components.
        /// </summary>
        public CustomUIRegistryService UiRegistry => _uiRegistry;

        /// <summary>
        /// Gets the flat list of items to display in the DataGrid.
        /// This is the main data source for the UI.
        /// </summary>
        public ObservableCollection<DataGridRowItemViewModel> FlatItemsSource { get; } = new();

        /// <summary>
        /// Gets or sets the filter text for node names.
        /// </summary>
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    RefreshFlatList();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to show only nodes with validation issues.
        /// </summary>
        public bool ShowOnlyInvalidNodes
        {
            get => _showOnlyInvalidNodes;
            set
            {
                if (SetProperty(ref _showOnlyInvalidNodes, value))
                {
                    RefreshFlatList();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to show schema-only nodes in addition to DOM nodes.
        /// </summary>
        public bool ShowSchemaNodes
        {
            get => _showSchemaNodes;
            set
            {
                if (SetProperty(ref _showSchemaNodes, value))
                {
                    RefreshFlatList();
                }
            }
        }

        /// <summary>
        /// Gets or sets the search text.
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    PerformSearch();
                }
            }
        }

        /// <summary>
        /// Gets whether the document has unsaved changes.
        /// </summary>
        public bool IsDirty
        {
            get => _isDirty;
            private set => SetProperty(ref _isDirty, value);
        }

        /// <summary>
        /// Gets the current file path.
        /// </summary>
        public string? CurrentFilePath
        {
            get => _currentFilePath;
            private set => SetProperty(ref _currentFilePath, value);
        }

        /// <summary>
        /// Gets the window title including file name and dirty indicator.
        /// </summary>
        public string WindowTitle
        {
            get
            {
                var fileName = string.IsNullOrEmpty(CurrentFilePath) ? "Untitled" : Path.GetFileName(CurrentFilePath);
                var dirtyIndicator = IsDirty ? "*" : "";
                return $"JSON Configuration Editor - {fileName}{dirtyIndicator}";
            }
        }

        // --- Private class EditOperation ---
        private abstract class EditOperation
        {
            public abstract void Undo(MainViewModel vm);
            public abstract void Redo(MainViewModel vm);
        }

        private sealed class ValueEditOperation : EditOperation
        {
            private readonly DomNode _node;
            private readonly System.Text.Json.JsonElement _oldValue;
            private readonly System.Text.Json.JsonElement _newValue;

            public ValueEditOperation(DomNode node, System.Text.Json.JsonElement oldValue, System.Text.Json.JsonElement newValue)
            {
                _node = node;
                _oldValue = oldValue;
                _newValue = newValue;
            }

            public override void Undo(MainViewModel vm) => vm.SetNodeValue(_node, _oldValue);
            public override void Redo(MainViewModel vm) => vm.SetNodeValue(_node, _newValue);
        }

        private sealed class AddNodeOperation : EditOperation
        {
            private readonly DomNode _parent;
            private readonly DomNode _newNode;
            private readonly string _name;

            public AddNodeOperation(DomNode parent, DomNode newNode, string name)
            {
                _parent = parent;
                _newNode = newNode;
                _name = name;
            }

            public override void Undo(MainViewModel vm) => vm.RemoveNode(_newNode);
            public override void Redo(MainViewModel vm) => vm.AddNode(_parent, _newNode, _name);
        }

        private sealed class RemoveNodeOperation : EditOperation
        {
            private readonly DomNode _parent;
            private readonly DomNode _removedNode;
            private readonly string _name;

            public RemoveNodeOperation(DomNode parent, DomNode removedNode, string name)
            {
                _parent = parent;
                _removedNode = removedNode;
                _name = name;
            }

            public override void Undo(MainViewModel vm) => vm.AddNode(_parent, _removedNode, _name);
            public override void Redo(MainViewModel vm) => vm.RemoveNode(_removedNode);
        }

        // --- Constructor ---

        public MainViewModel()
        {
            // Initialize services
            _jsonParser = new JsonDomParser();
            _jsonSerializer = new DomNodeToJsonSerializer();
            _validationService = new ValidationService();
            _uiRegistry = new CustomUIRegistryService();
            _schemaLoader = new SchemaLoaderService(_uiRegistry);

            // Initialize commands
            NewFileCommand = new RelayCommand(ExecuteNewFile);
            OpenFileCommand = new RelayCommand(ExecuteOpenFile);
            SaveFileCommand = new RelayCommand(ExecuteSaveFile, CanExecuteSaveFile);
            SaveAsFileCommand = new RelayCommand(ExecuteSaveAsFile);
            ExitCommand = new RelayCommand(ExecuteExit);
            UndoCommand = new RelayCommand(ExecuteUndo, CanExecuteUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, CanExecuteRedo);
            FocusSearchCommand = new RelayCommand(ExecuteFocusSearch);
            FindNextCommand = new RelayCommand(ExecuteFindNext, CanExecuteFind);
            FindPreviousCommand = new RelayCommand(ExecuteFindPrevious, CanExecuteFind);
            LoadSchemaCommand = new RelayCommand(ExecuteLoadSchema);

            // Initialize with empty document
            InitializeEmptyDocument();
        }

        // --- Command Implementations ---

        private void ExecuteNewFile()
        {
            if (CheckUnsavedChanges())
            {
                NewDocument();
            }
        }

        private async void ExecuteOpenFile()
        {
            if (!CheckUnsavedChanges())
                return;

            var openDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    await LoadFileAsync(openDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open file: {ex.Message}", "Open Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ExecuteSaveFile()
        {
            try
            {
                if (string.IsNullOrEmpty(CurrentFilePath))
                {
                    ExecuteSaveAsFile();
                }
                else
                {
                    await SaveFileAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file: {ex.Message}", "Save Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteSaveFile()
        {
            return _rootDomNode != null;
        }

        private async void ExecuteSaveAsFile()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    await SaveFileAsync(saveDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save file: {ex.Message}", "Save Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteExit()
        {
            Application.Current.Shutdown();
        }

        private void ExecuteUndo()
        {
            if (_undoStack.Count == 0) return;
            var op = _undoStack.Pop();
            op.Undo(this);
            _redoStack.Push(op);
            RefreshFlatList();
            OnPropertyChanged(nameof(WindowTitle));
        }

        private bool CanExecuteUndo() => _undoStack.Count > 0;

        private void ExecuteRedo()
        {
            if (_redoStack.Count == 0) return;
            var op = _redoStack.Pop();
            op.Redo(this);
            _undoStack.Push(op);
            RefreshFlatList();
            OnPropertyChanged(nameof(WindowTitle));
        }

        private bool CanExecuteRedo() => _redoStack.Count > 0;

        private void ExecuteFocusSearch()
        {
            // TODO: Focus the search textbox
        }

        private void ExecuteFindNext()
        {
            if (_searchResults.Count == 0) return;
            _currentSearchIndex = (_currentSearchIndex + 1) % _searchResults.Count;
            HighlightSearchResults();
            ScrollToCurrentSearchResult();
        }

        private void ExecuteFindPrevious()
        {
            if (_searchResults.Count == 0) return;
        }

        private bool CanExecuteFind()
        {
            return !string.IsNullOrEmpty(SearchText);
        }

        private async void ExecuteLoadSchema()
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "Assembly files (*.dll)|*.dll|All files (*.*)|*.*",
                DefaultExt = "dll",
                Multiselect = true
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    await LoadSchemasAsync(openDialog.FileNames);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load schemas: {ex.Message}", "Schema Loading Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Checks for unsaved changes and prompts the user.
        /// </summary>
        /// <returns>True if it's safe to proceed, false if the operation should be cancelled</returns>
        private bool CheckUnsavedChanges()
        {
            if (!IsDirty)
                return true;

            var result = MessageBox.Show("You have unsaved changes. Do you want to save before continuing?", 
                "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    ExecuteSaveFile();
                    return !IsDirty; // Only proceed if save was successful
                case MessageBoxResult.No:
                    return true;
                case MessageBoxResult.Cancel:
                default:
                    return false;
            }
        }

        // --- Public Methods ---

        /// <summary>
        /// Loads a JSON file asynchronously.
        /// </summary>
        public async Task LoadFileAsync(string filePath)
        {
            try
            {
                _rootDomNode = await _jsonParser.ParseFromFileAsync(filePath);
                CurrentFilePath = filePath;
                IsDirty = false;

                // Rebuild DOM to schema mapping
                RebuildDomToSchemaMapping();

                // Refresh the flat list
                RefreshFlatList();

                // Validate the document
                await ValidateDocumentAsync();
            }
            catch (Exception ex)
            {
                // Handle error (in a real app, show message box or status)
                throw new InvalidOperationException($"Failed to load file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Saves the current document to a file asynchronously.
        /// </summary>
        public async Task SaveFileAsync(string? filePath = null)
        {
            if (_rootDomNode == null)
                return;

            var targetPath = filePath ?? CurrentFilePath;
            if (string.IsNullOrEmpty(targetPath))
                throw new InvalidOperationException("No file path specified for save operation");

            try
            {
                await _jsonSerializer.SerializeToFileAsync(_rootDomNode, targetPath);
                CurrentFilePath = targetPath;
                IsDirty = false;
                OnPropertyChanged(nameof(WindowTitle));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a new empty document.
        /// </summary>
        public void NewDocument()
        {
            InitializeEmptyDocument();
            CurrentFilePath = null;
            IsDirty = false;
            OnPropertyChanged(nameof(WindowTitle));
        }

        /// <summary>
        /// Loads schemas from assemblies asynchronously.
        /// </summary>
        public async Task LoadSchemasAsync(IEnumerable<string> assemblyPaths)
        {
            await _schemaLoader.LoadSchemasFromAssembliesAsync(assemblyPaths);
            RebuildDomToSchemaMapping();
            RefreshFlatList();
            await ValidateDocumentAsync();
        }

        // --- Internal Methods for DataGridRowItemViewModel callbacks ---

        /// <summary>
        /// Called when a node's expansion state changes.
        /// </summary>
        internal void OnExpansionChanged(DataGridRowItemViewModel item)
        {
            RefreshFlatList();
        }

        /// <summary>
        /// Sets the currently edited item.
        /// </summary>
        internal void SetCurrentlyEditedItem(DataGridRowItemViewModel? item)
        {
            _currentlyEditedItem = item;
        }

        /// <summary>
        /// Called when a node's value changes.
        /// </summary>
        internal void OnNodeValueChanged(DataGridRowItemViewModel item)
        {
            IsDirty = true;
            OnPropertyChanged(nameof(WindowTitle));
            
            // Trigger validation for the changed node
            _ = Task.Run(async () => await ValidateDocumentAsync());
        }

        /// <summary>
        /// Checks if a reference path can be resolved within the current DOM.
        /// </summary>
        internal bool IsRefPathResolvable(string referencePath)
        {
            // Simple implementation - in a real app this would traverse the DOM
            return !string.IsNullOrEmpty(referencePath) && !referencePath.Contains("://");
        }

        /// <summary>
        /// Adds a new item to an array.
        /// </summary>
        internal bool AddArrayItem(ArrayNode parentArray, string editValue, SchemaNode? itemSchema)
        {
            try
            {
                var newNode = CreateNodeFromValue(editValue, parentArray.Items.Count.ToString(), parentArray, itemSchema);
                RecordEditOperation(new AddNodeOperation(parentArray, newNode, newNode.Name));
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Materializes a schema-only node into the DOM.
        /// </summary>
        internal bool MaterializeSchemaOnlyNode(DataGridRowItemViewModel schemaOnlyItem, string editValue)
        {
            if (schemaOnlyItem.DomNode?.Parent == null || schemaOnlyItem.SchemaContextNode == null) return false;

            try
            {
                var newNode = CreateNodeFromValue(editValue, schemaOnlyItem.NodeName, schemaOnlyItem.DomNode.Parent, schemaOnlyItem.SchemaContextNode);
                RecordEditOperation(new AddNodeOperation(schemaOnlyItem.DomNode.Parent, newNode, schemaOnlyItem.NodeName));
                return true;
            }
            catch
            {
                return false;
            }
        }

        // --- Private Methods ---

        /// <summary>
        /// Initializes an empty document with a root object.
        /// </summary>
        private void InitializeEmptyDocument()
        {
            _rootDomNode = new ObjectNode("$root", null);
            _domToSchemaMap.Clear();
            _validationIssuesMap.Clear();
            _persistentVmMap.Clear();
            RefreshFlatList();
        }

        /// <summary>
        /// Rebuilds the mapping from DOM nodes to their schemas.
        /// </summary>
        private void RebuildDomToSchemaMapping()
        {
            _domToSchemaMap.Clear();
            if (_rootDomNode != null)
            {
                MapDomNodeToSchemaRecursive(_rootDomNode);
            }
        }

        /// <summary>
        /// Recursively maps DOM nodes to their schemas.
        /// </summary>
        private void MapDomNodeToSchemaRecursive(DomNode node)
        {
            // Find schema for this node's path
            var schema = _schemaLoader.FindSchemaForPath(node.Path);
            _domToSchemaMap[node] = schema;

            // Recursively map children
            switch (node)
            {
                case ObjectNode objectNode:
                    foreach (var child in objectNode.GetChildren())
                    {
                        MapDomNodeToSchemaRecursive(child);
                    }
                    break;
                case ArrayNode arrayNode:
                    foreach (var item in arrayNode.GetItems())
                    {
                        MapDomNodeToSchemaRecursive(item);
                    }
                    break;
            }
        }

        /// <summary>
        /// Refreshes the flat list of items for the DataGrid.
        /// </summary>
        private void RefreshFlatList()
        {
            FlatItemsSource.Clear();

            if (_rootDomNode == null)
                return;

            // Build the flat list
            var flatItems = new List<DataGridRowItemViewModel>();
            BuildFlatListRecursive(_rootDomNode, flatItems);

            // Apply filtering
            var filteredItems = ApplyFiltering(flatItems);

            // Add to observable collection
            foreach (var item in filteredItems)
            {
                FlatItemsSource.Add(item);
            }
        }

        /// <summary>
        /// Recursively builds the flat list of view models.
        /// </summary>
        private void BuildFlatListRecursive(DomNode node, List<DataGridRowItemViewModel> flatItems)
        {
            // Get or create view model for this node
            if (!_persistentVmMap.TryGetValue(node, out var viewModel))
            {
                _domToSchemaMap.TryGetValue(node, out var schema);
                viewModel = new DataGridRowItemViewModel(node, schema, this);
                _persistentVmMap[node] = viewModel;
            }

            flatItems.Add(viewModel);

            // Add children if expanded
            if (viewModel.IsExpanded)
            {
                switch (node)
                {
                    case ObjectNode objectNode:
                        foreach (var child in objectNode.GetChildren())
                        {
                            BuildFlatListRecursive(child, flatItems);
                        }

                        // Add schema-only nodes if enabled
                        if (_showSchemaNodes && _domToSchemaMap.TryGetValue(node, out var objectSchema) && objectSchema?.Properties != null)
                        {
                            foreach (var schemaProp in objectSchema.Properties)
                            {
                                if (!objectNode.HasProperty(schemaProp.Key))
                                {
                                    var schemaOnlyVm = new DataGridRowItemViewModel(schemaProp.Value, schemaProp.Key, this, node.Depth + 1);
                                    flatItems.Add(schemaOnlyVm);
                                }
                            }
                        }
                        break;

                    case ArrayNode arrayNode:
                        foreach (var item in arrayNode.GetItems())
                        {
                            BuildFlatListRecursive(item, flatItems);
                        }

                        // Add "Add item" placeholder
                        if (_domToSchemaMap.TryGetValue(node, out var arraySchema))
                        {
                            var placeholderVm = new DataGridRowItemViewModel(arrayNode, arraySchema?.ItemSchema, this, node.Depth + 1);
                            flatItems.Add(placeholderVm);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Applies filtering to the flat list.
        /// </summary>
        private List<DataGridRowItemViewModel> ApplyFiltering(List<DataGridRowItemViewModel> items)
        {
            if (string.IsNullOrEmpty(FilterText) && !ShowOnlyInvalidNodes)
                return items;

            var filtered = new List<DataGridRowItemViewModel>();

            foreach (var item in items)
            {
                bool includeItem = true;

                // Apply name filter
                if (!string.IsNullOrEmpty(FilterText))
                {
                    includeItem = item.NodeName.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
                }

                // Apply validation filter
                if (ShowOnlyInvalidNodes)
                {
                    includeItem = includeItem && !item.IsValid;
                }

                if (includeItem)
                {
                    filtered.Add(item);
                }
            }

            return filtered;
        }

        // Comprehensive search logic for DOM and schema-only nodes
        private void BuildSearchIndex(string searchText, List<SearchResult> results)
        {
            if (_rootDomNode == null) return;
            var visited = new HashSet<DataGridRowItemViewModel>();
            // Traverse DOM tree
            void TraverseDom(DomNode node)
            {
                if (_persistentVmMap.TryGetValue(node, out var vm))
                {
                    if (!visited.Contains(vm) && vm.NodeName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new SearchResult(vm, node is ObjectNode or ArrayNode ? node.Name : vm.NodeName, false));
                        visited.Add(vm);
                    }
                }
                switch (node)
                {
                    case ObjectNode obj:
                        foreach (var child in obj.GetChildren()) TraverseDom(child);
                        break;
                    case ArrayNode arr:
                        foreach (var item in arr.GetItems()) TraverseDom(item);
                        break;
                }
            }
            TraverseDom(_rootDomNode);
            // Traverse schema-only nodes if enabled
            if (ShowSchemaNodes)
            {
                foreach (var vm in _persistentVmMap.Values)
                {
                    if (vm.IsSchemaOnlyNode && !visited.Contains(vm) && vm.NodeName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new SearchResult(vm, vm.NodeName, true));
                        visited.Add(vm);
                    }
                }
            }
        }

        private void PerformSearch()
        {
            // Clear previous highlights
            foreach (var item in _persistentVmMap.Values)
                item.IsHighlightedInSearch = false;
            _searchResults.Clear();
            _currentSearchIndex = -1;
            if (string.IsNullOrEmpty(SearchText)) return;
            BuildSearchIndex(SearchText, _searchResults);
            if (_searchResults.Count > 0)
            {
                _currentSearchIndex = 0;
                HighlightSearchResults();
                ScrollToCurrentSearchResult();
            }
        }

        private void HighlightSearchResults()
        {
            for (int i = 0; i < _searchResults.Count; i++)
            {
                var vm = _searchResults[i].Item;
                vm.IsHighlightedInSearch = (i == _currentSearchIndex);
            }
        }

        private void ScrollToCurrentSearchResult()
            {
            if (_currentSearchIndex >= 0 && _currentSearchIndex < _searchResults.Count)
                {
                var vm = _searchResults[_currentSearchIndex].Item;
                // Expand parents if needed
                ExpandAncestors(vm);
                // Scroll into view (if UI supports it)
                // (You may need to raise an event or call a method in the view)
                }
            }

        private void ExpandAncestors(DataGridRowItemViewModel vm)
        {
            var parent = vm.DomNode?.Parent;
            while (parent != null)
            {
                if (_persistentVmMap.TryGetValue(parent, out var parentVm))
                    parentVm.IsExpanded = true;
                parent = parent.Parent;
            }
        }

        /// <summary>
        /// Validates the entire document asynchronously.
        /// </summary>
        private async Task ValidateDocumentAsync()
        {
            if (_rootDomNode == null)
                return;

            await Task.Run(() =>
            {
                _validationIssuesMap.Clear();
                var issues = _validationService.ValidateTree(_rootDomNode, _domToSchemaMap);
                
                foreach (var issue in issues)
                {
                    _validationIssuesMap[issue.Key] = issue.Value;
                }

                // Update view models with validation results
                foreach (var vm in _persistentVmMap.Values)
                {
                    if (vm.DomNode != null && _validationIssuesMap.TryGetValue(vm.DomNode, out var nodeIssues))
                    {
                        var firstIssue = nodeIssues.FirstOrDefault();
                        vm.SetValidationState(false, firstIssue?.Message ?? "Validation failed");
                    }
                    else
                    {
                        vm.SetValidationState(true, "");
                    }
                }
            });
        }

        /// <summary>
        /// Creates a DOM node from a string value and schema.
        /// </summary>
        private DomNode CreateNodeFromValue(string value, string name, DomNode parent, SchemaNode? schema)
        {
            // Simple implementation - in a real app this would be more sophisticated
            try
            {
                var jsonDoc = System.Text.Json.JsonDocument.Parse(value);
                return _jsonParser.ParseFromJsonElement(jsonDoc.RootElement, name, parent);
            }
            catch
            {
                // If parsing fails, treat as string
                var stringJson = $"\"{value}\"";
                var jsonDoc = System.Text.Json.JsonDocument.Parse(stringJson);
                return _jsonParser.ParseFromJsonElement(jsonDoc.RootElement, name, parent);
            }
        }

        private class SearchResult
        {
            public DataGridRowItemViewModel Item { get; set; }
            public string Path { get; set; }
            public bool IsSchemaOnly { get; set; }
            public SearchResult(DataGridRowItemViewModel item, string path, bool isSchemaOnly)
            {
                Item = item;
                Path = path;
                IsSchemaOnly = isSchemaOnly;
            }
        }

        // --- Undo/Redo Methods ---
        private void RecordEditOperation(EditOperation op)
        {
            _undoStack.Push(op);
            _redoStack.Clear();
            IsDirty = true;
            OnPropertyChanged(nameof(WindowTitle));
        }

        // --- Node Edit Methods (used by EditOperation) ---
        private void SetNodeValue(DomNode node, System.Text.Json.JsonElement value)
        {
            if (node is ValueNode valueNode)
            {
                var oldValue = valueNode.Value;
                valueNode.Value = value;
                RecordEditOperation(new ValueEditOperation(node, oldValue, value));
                OnNodeValueChanged(_persistentVmMap[node]);
            }
        }

        private void AddNode(DomNode parent, DomNode newNode, string name)
        {
            if (parent is ObjectNode obj)
            {
                obj.AddChild(name, newNode);
                _domToSchemaMap[newNode] = _schemaLoader.FindSchemaForPath(newNode.Path);
                var vm = new DataGridRowItemViewModel(newNode, _domToSchemaMap[newNode], this);
                _persistentVmMap[newNode] = vm;
                RefreshFlatList();
            }
            else if (parent is ArrayNode arr)
            {
                arr.AddItem(newNode);
                _domToSchemaMap[newNode] = _schemaLoader.FindSchemaForPath(newNode.Path);
                var vm = new DataGridRowItemViewModel(newNode, _domToSchemaMap[newNode], this);
                _persistentVmMap[newNode] = vm;
                RefreshFlatList();
            }
        }

        private void RemoveNode(DomNode node)
        {
            if (node.Parent is ObjectNode obj)
            {
                obj.RemoveChild(node.Name);
                _persistentVmMap.Remove(node);
                _domToSchemaMap.Remove(node);
                RefreshFlatList();
            }
            else if (node.Parent is ArrayNode arr)
            {
                arr.RemoveItem(node);
                _persistentVmMap.Remove(node);
                _domToSchemaMap.Remove(node);
                RefreshFlatList();
            }
        }
    }
} 
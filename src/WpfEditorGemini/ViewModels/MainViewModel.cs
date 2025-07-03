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
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using JsonConfigEditor.Wpf.Services;
using JsonConfigEditor.Contracts.Editors;
using System.Threading; // Added for Timer
using JsonConfigEditor.Core.Cascade;
using JsonConfigEditor.Core.Services;
using JsonConfigEditor.Core.History;

namespace JsonConfigEditor.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // --- Private Fields: Services ---
        private readonly IJsonDomParser _jsonParser;
        private readonly IDomNodeToJsonSerializer _jsonSerializer;
        private readonly ISchemaLoaderService _schemaLoader;
        private readonly ValidationService _validationService;
        private readonly CustomUIRegistryService _uiRegistry;
        // ADD the new factory service
        private readonly DomNodeFactory _nodeFactory;
        // ADD the new history service
        private readonly EditHistoryService _historyService;
        private readonly DomFilterService _filterService;
        private readonly DomSearchService _searchService;
        private readonly SchemaPlaceholderProvider _placeholderProvider;
        private readonly ViewModelBuilderService _viewModelBuilder;
        // ADD the new intra-layer merger service
        private readonly IntraLayerMerger _intraLayerMerger;

        // --- Private Fields: Core Data State ---
        // REMOVE the old root node field:
        // private DomNode? _rootDomNode;
        // ADD the new list for cascade layers:
        private List<CascadeLayer> _cascadeLayers = new();
        public CascadeLayer? ActiveEditorLayer { get; private set; } // Add this property
        // Dictionaries are now keyed by the node's unique string path for stability
        private readonly Dictionary<string, SchemaNode?> _domToSchemaMap = new();
        private readonly Dictionary<string, List<ValidationIssue>> _validationIssuesMap = new();
        private readonly Dictionary<string, DataGridRowItemViewModel> _persistentVmMap = new();
        private readonly Dictionary<string, bool> _schemaNodeExpansionState = new(); // For schema-only node states

        // --- Private Fields: UI State & Filters/Search ---
        private string? _currentFilePath;
        private bool _isDirty;
        private string _filterText = string.Empty;
        private bool _showOnlyInvalidNodes;
        private bool _showSchemaNodes = true; // Toggle for DOM vs DOM+Schema view
        private string _searchText = string.Empty;
        private DataGridRowItemViewModel? _currentlyEditedItem;
        private DataGridRowItemViewModel? _selectedGridItem; // For TwoWay binding with DataGrid
        private int _currentSearchIndex = -1;

        // --- Private Fields: Search Session State ---
        private List<DomNode> _searchResultNodes = new();

        // --- Private Fields: Search ---
        private System.Threading.Timer? _searchDebounceTimer;
        private const int SearchDebounceMilliseconds = 500;
        private readonly object _searchLock = new object(); // For thread safety with timer

        // --- Serializer Options for converting object to JsonElement ---
        private static readonly JsonSerializerOptions _defaultSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = false, // Not strictly necessary for element conversion but good practice
            PropertyNamingPolicy = null, // Or JsonNamingPolicy.CamelCase if needed by schema representation
        };

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
        public ICommand OpenModalEditorCommand { get; }
        public ICommand ClearFilterCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand DeleteSelectedNodesCommand { get; }
        public ICommand ExpandSelectedRecursiveCommand { get; }
        public ICommand CollapseSelectedRecursiveCommand { get; }

        // --- Public Properties ---

        public CustomUIRegistryService UiRegistry => _uiRegistry;
        public ISchemaLoaderService SchemaLoader => _schemaLoader;
        public EditHistoryService HistoryService => _historyService; // Expose the service

        public ObservableCollection<DataGridRowItemViewModel> FlatItemsSource { get; } = new();

        private System.Threading.Timer? _filterDebounceTimer;

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    _filterDebounceTimer?.Dispose();
                    _filterDebounceTimer = new System.Threading.Timer(DebouncedFilterAction, null, SearchDebounceMilliseconds, System.Threading.Timeout.Infinite);
                    OnPropertyChanged(nameof(CanClearFilter));
                }
            }
        }

        private void DebouncedFilterAction(object? state)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                RefreshFlatList();
            });
        }

        public bool CanClearSearch => !string.IsNullOrEmpty(SearchText);

        public bool CanClearFilter => !string.IsNullOrEmpty(FilterText);

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

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _searchDebounceTimer?.Dispose();
                    _searchDebounceTimer = new System.Threading.Timer(DebouncedSearchAction, null, SearchDebounceMilliseconds, System.Threading.Timeout.Infinite);
                    OnPropertyChanged(nameof(CanClearSearch));
                }
            }
        }

        public bool IsDirty
        {
            get => _isDirty;
            private set => SetProperty(ref _isDirty, value);
        }

        public string? CurrentFilePath
        {
            get => _currentFilePath;
            private set => SetProperty(ref _currentFilePath, value);
        }

        public DataGridRowItemViewModel? SelectedGridItem
        {
            get => _selectedGridItem;
            set => SetProperty(ref _selectedGridItem, value);
        }

        public string WindowTitle
        {
            get
            {
                var fileName = string.IsNullOrEmpty(CurrentFilePath) ? "Untitled" : Path.GetFileName(CurrentFilePath);
                var dirtyIndicator = IsDirty ? "*" : "";
                return $"JSON Configuration Editor - {fileName}{dirtyIndicator}";
            }
        }

        public MainViewModel()
        {
            _jsonParser = new JsonDomParser();
            _jsonSerializer = new DomNodeToJsonSerializer();
            _searchService = new DomSearchService();
            _placeholderProvider = new SchemaPlaceholderProvider();
            _validationService = new ValidationService();
            _uiRegistry = new CustomUIRegistryService();
            _schemaLoader = new SchemaLoaderService(_uiRegistry);
            // INSTANTIATE the new factory in the constructor
            _nodeFactory = new DomNodeFactory(_schemaLoader, this);
            // INSTANTIATE the new history service in the constructor
            _historyService = new EditHistoryService(this);
            _historyService.ModelChanged += OnHistoryModelChanged; // Subscribe to the event
            _filterService = new DomFilterService();
            _intraLayerMerger = new IntraLayerMerger();
            _viewModelBuilder = new ViewModelBuilderService(_placeholderProvider, this);

            NewFileCommand = new RelayCommand(ExecuteNewFile);
            OpenFileCommand = new RelayCommand(ExecuteOpenFile);
            SaveFileCommand = new RelayCommand(ExecuteSaveFile, CanExecuteSaveFile);
            SaveAsFileCommand = new RelayCommand(ExecuteSaveAsFile);
            ExitCommand = new RelayCommand(ExecuteExit);
            // UPDATE ICommand properties to use the service
            UndoCommand = new RelayCommand(ExecuteUndo, () => _historyService.CanUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, () => _historyService.CanRedo);
            FocusSearchCommand = new RelayCommand(ExecuteFocusSearch);
            FindNextCommand = new RelayCommand(ExecuteFindNext, CanExecuteFind);
            FindPreviousCommand = new RelayCommand(ExecuteFindPrevious, CanExecuteFind);
            LoadSchemaCommand = new RelayCommand(ExecuteLoadSchema);
            OpenModalEditorCommand = new RelayCommand(param => ExecuteOpenModalEditor(param as DataGridRowItemViewModel), param => CanExecuteOpenModalEditor(param as DataGridRowItemViewModel));
            ClearFilterCommand = new RelayCommand(ExecuteClearFilter, () => CanClearFilter);
            ClearSearchCommand = new RelayCommand(ExecuteClearSearch, () => CanClearSearch);
            DeleteSelectedNodesCommand = new RelayCommand(param => ExecuteDeleteSelectedNodes(param as DataGridRowItemViewModel), param => CanExecuteDeleteSelectedNodes(param as DataGridRowItemViewModel));
            ExpandSelectedRecursiveCommand = new RelayCommand(param => ExecuteExpandSelectedRecursive(param as DataGridRowItemViewModel), param => CanExecuteExpandCollapseSelectedRecursive(param as DataGridRowItemViewModel));
            CollapseSelectedRecursiveCommand = new RelayCommand(param => ExecuteCollapseSelectedRecursive(param as DataGridRowItemViewModel), param => CanExecuteExpandCollapseSelectedRecursive(param as DataGridRowItemViewModel));

            InitializeEmptyDocument();
        }

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
            return _cascadeLayers.Count > 0;
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

        // This is the new, intelligent event handler
        private void OnHistoryModelChanged(EditOperation operation)
        {
            IsDirty = true;
            OnPropertyChanged(nameof(WindowTitle));
            _ = Task.Run(() => ValidateDocumentAsync());

            if (operation.RequiresFullRefresh)
            {
                RefreshFlatList();
            }
            else if (operation.NodePath != null)
            {
                // Find the specific ViewModel for the node that changed
                // and tell it to refresh its display properties from the updated model.
                if (_persistentVmMap.TryGetValue(operation.NodePath, out var vmToUpdate))
                {
                    vmToUpdate.RefreshDisplayProperties();
                }
            }
        }

        // UPDATE command handlers to be simple delegations
        private void ExecuteUndo() => _historyService.Undo();
        private void ExecuteRedo() => _historyService.Redo();

        private void ExecuteFocusSearch() { }

        private void ExecuteFindNext()
        {
            if (!_searchResultNodes.Any()) return;

            _currentSearchIndex = (_currentSearchIndex + 1) % _searchResultNodes.Count;
            var nodeToFind = _searchResultNodes[_currentSearchIndex];

            // Use existing logic to expand the tree and select the item
            EnsurePathIsExpandedInFlatItemsSource(nodeToFind.Path);
            if (_persistentVmMap.TryGetValue(nodeToFind.Path, out var vmToSelect))
            {
                SelectedGridItem = vmToSelect;
            }
        }

        private void ExecuteFindPrevious()
        {
            if (!_searchResultNodes.Any()) return;

            _currentSearchIndex = (_currentSearchIndex - 1 + _searchResultNodes.Count) % _searchResultNodes.Count;
            var nodeToFind = _searchResultNodes[_currentSearchIndex];

            // Use existing logic to expand the tree and select the item
            EnsurePathIsExpandedInFlatItemsSource(nodeToFind.Path);
            if (_persistentVmMap.TryGetValue(nodeToFind.Path, out var vmToSelect))
            {
                SelectedGridItem = vmToSelect;
            }
        }

        private bool CanExecuteFind()
        {
            return !string.IsNullOrEmpty(SearchText) && _searchResultNodes.Any();
        }

        private bool CanExecuteOpenModalEditor(DataGridRowItemViewModel? item)
        {
            return item?.ModalEditorInstance != null;
        }

        private void ExecuteOpenModalEditor(DataGridRowItemViewModel? parameter)
        {
            var vm = parameter as DataGridRowItemViewModel;
            if (vm == null || vm.ModalEditorInstance == null)
                return;

            IValueEditor editor = vm.ModalEditorInstance;
            object viewModelForEditor = vm;

            MessageBox.Show($"Attempting to open modal editor for: {vm.NodeName}\nEditor Type: {editor.GetType().Name}\nThis is a placeholder. Actual modal dialog logic needs to be implemented here.",
                            "Open Modal Editor", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void ExecuteClearFilter()
        {
            FilterText = string.Empty;
        }

        private void ExecuteClearSearch()
        {
            SearchText = string.Empty;
        }

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
                    return !IsDirty;
                case MessageBoxResult.No:
                    return true;
                case MessageBoxResult.Cancel:
                default:
                    return false;
            }
        }

        public async Task LoadFileAsync(string filePath)
        {
            _historyService.Clear();
            try
            {
                // --- NEW LOGIC ---
                // Treat the single file as a project with one layer.
                var fileContent = await File.ReadAllTextAsync(filePath);
                var domRoot = _jsonParser.ParseFromString(fileContent);

                // Create the necessary info objects.
                var sourceFile = new SourceFileInfo(filePath, Path.GetFileName(filePath), domRoot, fileContent);
                var layerDef = new LayerDefinitionModel("Default", Path.GetDirectoryName(filePath)!);
                var layerLoadResult = new LayerLoadResult(layerDef, new List<SourceFileInfo> { sourceFile });

                // Perform the intra-layer merge (which is trivial for one file).
                var mergeResult = _intraLayerMerger.Merge(layerLoadResult);
                  
                // Create the single CascadeLayer.
                var singleLayer = new CascadeLayer(0, "Default", Path.GetDirectoryName(filePath)!, 
                    layerLoadResult.SourceFiles, mergeResult.LayerConfigRootNode, mergeResult.IntraLayerValueOrigins);

                // Set the new data model.
                _cascadeLayers = new List<CascadeLayer> { singleLayer };
                ActiveEditorLayer = _cascadeLayers.FirstOrDefault(); // Set the active layer
                // --- END NEW LOGIC ---

                CurrentFilePath = filePath;
                IsDirty = false;

                ResetSearchState();
                RebuildDomToSchemaMapping();
                RefreshFlatList();
                await ValidateDocumentAsync();
                OnPropertyChanged(nameof(WindowTitle));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load file: {ex.Message}", ex);
            }
        }

        public async Task SaveFileAsync(string? filePath = null)
        {
            // Get the root node from the first layer to save it.
            var rootNodeToSave = _cascadeLayers.FirstOrDefault()?.LayerConfigRootNode;
            if (rootNodeToSave == null) return;

            var targetPath = filePath ?? CurrentFilePath;
            if (string.IsNullOrEmpty(targetPath))
                throw new InvalidOperationException("No file path specified for save operation");

            try
            {
                await _jsonSerializer.SerializeToFileAsync(rootNodeToSave, targetPath);
                CurrentFilePath = targetPath;
                IsDirty = false;
                OnPropertyChanged(nameof(WindowTitle));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save file: {ex.Message}", ex);
            }
        }

        public void NewDocument()
        {
            InitializeEmptyDocument();
            CurrentFilePath = null;
            IsDirty = false;
            OnPropertyChanged(nameof(WindowTitle));
        }

        public async Task LoadSchemasAsync(IEnumerable<string> assemblyPaths)
        {
            await _schemaLoader.LoadSchemasFromAssembliesAsync(assemblyPaths);
            RebuildDomToSchemaMapping();
            RefreshFlatList();
            await ValidateDocumentAsync();
        }

        internal void OnExpansionChanged(DataGridRowItemViewModel item)
        {
            RefreshFlatList();
        }

        internal void SetCurrentlyEditedItem(DataGridRowItemViewModel? item)
        {
            _currentlyEditedItem = item;
        }

        internal void OnNodeValueChanged(DataGridRowItemViewModel item)
        {
            IsDirty = true;
            OnPropertyChanged(nameof(WindowTitle));

            _ = Task.Run(async () => await ValidateDocumentAsync());
        }

        internal bool IsRefPathResolvable(string referencePath)
        {
            return !string.IsNullOrEmpty(referencePath) && !referencePath.Contains("://");
        }

        internal bool AddArrayItem(ArrayNode parentArray, string editValue, SchemaNode? itemSchema)
        {
            try
            {
                // 1. Create the new node from the user's value.
                var newNode = _nodeFactory.CreateFromValue(editValue, parentArray.Items.Count.ToString(), parentArray, itemSchema);
                if (newNode == null) return false;

                // 2. Create the operation that knows how to add the node.
                // FIX: Pass the active layer index to the operation.
                var activeLayerIndex = ActiveEditorLayer?.LayerIndex ?? 0;
                var operation = new AddNodeOperation(activeLayerIndex, parentArray, newNode);

                // 3. (This is the fix) Execute the operation to modify the data model NOW.
                operation.Redo(this);

                // 4. Now that the model is updated, record the operation for undo/redo.
                //    This will trigger the ModelChanged event, which will correctly refresh the UI.
                _historyService.Record(operation);

                // After the list has been refreshed, find the ViewModel for the node we just added.
                if (_persistentVmMap.TryGetValue(newNode.Path, out var newVm))
                {
                    // Set it as the selected item. This will trigger the PropertyChanged event handler,
                    // which contains all the logic to scroll to and focus the new row.
                    SelectedGridItem = newVm;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        internal bool MaterializeSchemaNodeAndBeginEdit(DataGridRowItemViewModel schemaOnlyVm, string initialEditValue)
        {
            if (!schemaOnlyVm.IsSchemaOnlyNode || schemaOnlyVm.SchemaContextNode == null)
            {
                return false;
            }

            var targetPathKey = schemaOnlyVm.SchemaNodePathKey;
            var targetSchemaNode = schemaOnlyVm.SchemaContextNode;

            // DELEGATE materialization to the factory
            DomNode? materializedDomNode = _nodeFactory.MaterializeDomPathRecursive(targetPathKey, targetSchemaNode);

            if (materializedDomNode == null)
            {
                return false;
            }

            if (materializedDomNode is ValueNode valueNode)
            {
                try
                {
                    JsonElement newJsonValue;
                    SchemaNode? vnSchema = schemaOnlyVm.SchemaContextNode;
                    Type targetType = vnSchema?.ClrType ?? typeof(string);

                    if (targetType == typeof(bool) && bool.TryParse(initialEditValue, out bool bVal))
                        newJsonValue = bVal ? JsonDocument.Parse("true").RootElement : JsonDocument.Parse("false").RootElement;
                    else if ((targetType == typeof(int) || targetType == typeof(long)) && long.TryParse(initialEditValue, out long lVal))
                        newJsonValue = JsonDocument.Parse(lVal.ToString()).RootElement;
                    else if ((targetType == typeof(double) || targetType == typeof(float) || targetType == typeof(decimal)) && double.TryParse(initialEditValue, out double dVal))
                        newJsonValue = JsonDocument.Parse(dVal.ToString(System.Globalization.CultureInfo.InvariantCulture)).RootElement;
                    else
                        newJsonValue = JsonDocument.Parse($"\"{JsonEncodedText.Encode(initialEditValue)}\"").RootElement;

                    if (valueNode.Value.ValueKind != newJsonValue.ValueKind || valueNode.Value.ToString() != newJsonValue.ToString())
                    {
                        var oldValue = valueNode.Value;
                        // FIX: Get the active layer index from the parent ViewModel.
                        var activeLayerIndex = ActiveEditorLayer?.LayerIndex ?? 0;
                        var valueEditOp = new ValueEditOperation(activeLayerIndex, valueNode, oldValue, newJsonValue.Clone());

                        // 1. Execute the operation now to update the model from the default value to the new value.
                        valueEditOp.Redo(this);

                        _historyService.Record(valueEditOp);
                    }
                }
                catch (JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to parse initialEditValue '{initialEditValue}' for materialized node '{valueNode.Path}': {ex.Message}");
                }
            }

            // Ensure the newly materialized node is mapped to its schema
            MapDomNodeToSchemaRecursive(materializedDomNode);
            
            // Debug output
            if (materializedDomNode is ValueNode debugValueNode)
            {
                System.Diagnostics.Debug.WriteLine($"Materialization - Created node: {debugValueNode.Path}, JSON Type: {debugValueNode.Value.ValueKind}, JSON Value: {debugValueNode.Value}");
                var schema = _domToSchemaMap.TryGetValue(debugValueNode.Path, out var s) ? s : null;
                System.Diagnostics.Debug.WriteLine($"Materialization - Schema mapping: {schema?.Name}, Schema Type: {schema?.ClrType?.Name}");
                
                // Debug: Check if the schema mapping is actually in the map
                System.Diagnostics.Debug.WriteLine($"Materialization - domToSchemaMap contains key '{debugValueNode.Path}': {_domToSchemaMap.ContainsKey(debugValueNode.Path)}");
                System.Diagnostics.Debug.WriteLine($"Materialization - All keys in domToSchemaMap: {string.Join(", ", _domToSchemaMap.Keys)}");
            }

            RefreshFlatList();

            if (_persistentVmMap.TryGetValue(materializedDomNode.Path, out var newVm))
            {
                SelectedGridItem = newVm;
                if (materializedDomNode is ValueNode || materializedDomNode is RefNode)
                {
                    newVm.IsInEditMode = true;
                    SetCurrentlyEditedItem(newVm);
                }
            }
            return true;
        }

        private void InitializeEmptyDocument()
        {
            var root = new ObjectNode("$root", null);
            var layer = new CascadeLayer(0, "Default", "", new List<SourceFileInfo>(), root, new Dictionary<string, string>());
            _cascadeLayers = new List<CascadeLayer> { layer };
            ActiveEditorLayer = _cascadeLayers.FirstOrDefault(); // Set the active layer

            _domToSchemaMap.Clear();
            _validationIssuesMap.Clear();
            _persistentVmMap.Clear();
            RefreshFlatList();
        }

        private void RebuildDomToSchemaMapping()
        {
            _domToSchemaMap.Clear();
            var rootNode = GetRootDomNode();
            if (rootNode != null)
            {
                MapDomNodeToSchemaRecursive(rootNode);
            }
        }

        public void MapDomNodeToSchemaRecursive(DomNode node)
        {
            var schema = _schemaLoader.FindSchemaForPath(node.Path);
            _domToSchemaMap[node.Path] = schema;

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

        private void RefreshFlatList()
        {
            var rootNode = GetRootDomNode();
            if (rootNode == null)
            {
                FlatItemsSource.Clear();
                _persistentVmMap.Clear();
                SelectedGridItem = null;
                return;
            }

            // 1. Preserve selection
            string? selectedPathIdentifier = SelectedGridItem?.DomNode?.Path ?? (SelectedGridItem?.IsSchemaOnlyNode == true ? SelectedGridItem.SchemaNodePathKey : null);
              
            // 2. DELEGATE list building to the new service
            List<DataGridRowItemViewModel> tempFlatList;
            
            if (!string.IsNullOrEmpty(FilterText))
            {
                // Use filtering service to get visible paths
                var visiblePaths = _filterService.GetVisibleNodePaths(rootNode, FilterText);
                tempFlatList = _viewModelBuilder.BuildList(
                    rootNode, 
                    _persistentVmMap, 
                    _domToSchemaMap, 
                    ShowSchemaNodes,
                    visiblePaths
                );
            }
            else
            {
                tempFlatList = _viewModelBuilder.BuildList(
                    rootNode, 
                    _persistentVmMap, 
                    _domToSchemaMap, 
                    ShowSchemaNodes
                );
            }

            // 3. Apply view filters (like "Show Only Invalid")
            var processedList = ApplyFiltering(tempFlatList);
              
            // 4. Update the UI collection and the persistent map
            FlatItemsSource.Clear();
            var newPersistentMap = new Dictionary<string, DataGridRowItemViewModel>();
            DataGridRowItemViewModel? itemToReselect = null;

            foreach (var itemVm in processedList)
            {
                FlatItemsSource.Add(itemVm);
                string itemPathKey = itemVm.DomNode?.Path ?? itemVm.SchemaNodePathKey;

                if (!string.IsNullOrEmpty(itemPathKey))
                {
                    newPersistentMap[itemPathKey] = itemVm;
                    if (itemPathKey == selectedPathIdentifier)
                    {
                        itemToReselect = itemVm;
                    }
                }
            }

            _persistentVmMap.Clear();
            foreach (var entry in newPersistentMap) _persistentVmMap.Add(entry.Key, entry.Value);

            // 5. Finalize UI state
            HighlightSearchResults();
            SelectedGridItem = itemToReselect;
        }

        private List<DataGridRowItemViewModel> ApplyFiltering(List<DataGridRowItemViewModel> items)
        {
            if (!ShowOnlyInvalidNodes)
                return items;

            var filtered = new List<DataGridRowItemViewModel>();

            foreach (var item in items)
            {
                if (!item.IsValid)
                {
                    filtered.Add(item);
                }
            }

            return filtered;
        }

        private void DebouncedSearchAction(object? state)
        {
            lock (_searchLock)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var rootNode = GetRootDomNode();
                    if (string.IsNullOrEmpty(SearchText) || rootNode == null)
                    {
                        ResetSearchState();
                        return;
                    }

                    // DELEGATE the search to the new service
                    _searchResultNodes = _searchService.FindAllMatches(rootNode, SearchText);
                    _currentSearchIndex = -1;

                    // Update UI state based on results
                    if (_searchResultNodes.Any())
                    {
                        SearchStatusText = $"{_searchResultNodes.Count} matches found";
                        // Automatically select the first result
                        ExecuteFindNext();   
                    }
                    else
                    {
                        SearchStatusText = "No matches found";
                    }
                      
                    HighlightSearchResults(); // Update highlighting on all visible nodes
                });
            }
        }

        private void ResetSearchState()
        {
            _searchResultNodes.Clear();
            _currentSearchIndex = -1;
            SearchStatusText = "";
            // ... logic to clear highlights ...
        }

        private string _searchStatusText = string.Empty;
        public string SearchStatusText
        {
            get => _searchStatusText;
            private set => SetProperty(ref _searchStatusText, value);
        }

        private void HighlightSearchResults()
        {
            // This method now uses the `_searchResultNodes` list to determine highlighting
            var searchResultPaths = new HashSet<string>(_searchResultNodes.Select(n => n.Path));
              
            foreach (var vm in FlatItemsSource)
            {
                bool isHighlighted = !string.IsNullOrEmpty(vm.DomNode?.Path) && searchResultPaths.Contains(vm.DomNode.Path);
                // This assumes DataGridRowItemViewModel has a property to control its highlight state
                // vm.IsHighlightedInSearch = isHighlighted;   
            }
        }

        public bool IsDomNodeInSearchResults(DomNode node)
        {
            return _searchResultNodes.Contains(node);
        }

        private async Task ValidateDocumentAsync()
        {
            var rootNode = GetRootDomNode();
            if (rootNode == null) return;

            await Task.Run(() =>
            {
                _validationIssuesMap.Clear();
                // CHANGE: This now uses a temporary DomNode-keyed dictionary for the service call.
                var tempDomToSchemaMap = new Dictionary<DomNode, SchemaNode?>();
                // This helper function would need to be created or adapted to build the temp map.
                BuildTemporaryDomToSchemaMap(rootNode, tempDomToSchemaMap);
                var issuesByNode = _validationService.ValidateTree(rootNode, tempDomToSchemaMap);

                foreach (var issueEntry in issuesByNode)
                {
                    // CHANGE: Store validation issues by path.
                    _validationIssuesMap[issueEntry.Key.Path] = issueEntry.Value;
                }
            });

            // Update UI on the main thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Update validation state for all ViewModels in the persistent map
                foreach (var vm in _persistentVmMap.Values)
                {
                    string? pathKey = vm.DomNode?.Path;
                    if (pathKey != null && _validationIssuesMap.TryGetValue(pathKey, out var nodeIssues))
                    {
                        vm.SetValidationState(false, nodeIssues.FirstOrDefault()?.Message ?? "Validation failed");
                    }
                    else
                    {
                        vm.SetValidationState(true, "");
                    }
                }
                
                // Also update validation state for schema-only nodes
                foreach (var vm in FlatItemsSource)
                {
                    if (vm.IsSchemaOnlyNode)
                    {
                        string? pathKey = vm.SchemaNodePathKey;
                        if (pathKey != null && _validationIssuesMap.TryGetValue(pathKey, out var nodeIssues))
                        {
                            vm.SetValidationState(false, nodeIssues.FirstOrDefault()?.Message ?? "Validation failed");
                        }
                        else
                        {
                            vm.SetValidationState(true, "");
                        }
                    }
                }
            });
        }

        // Helper to create the temporary map needed by the existing validation service
        private void BuildTemporaryDomToSchemaMap(DomNode node, Dictionary<DomNode, SchemaNode?> map)
        {
            if (_domToSchemaMap.TryGetValue(node.Path, out var schema))
            {
                map[node] = schema;
            }
            if (node is ObjectNode o) foreach (var child in o.GetChildren()) BuildTemporaryDomToSchemaMap(child, map);
            else if (node is ArrayNode a) foreach (var item in a.GetItems()) BuildTemporaryDomToSchemaMap(item, map);
        }



        private void EnsurePathIsExpandedInFlatItemsSource(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            var segments = path.Split('/');
            var rootNode = GetRootDomNode();
            DomNode? currentNode = rootNode;
            DataGridRowItemViewModel? currentVm = null;

            if (rootNode != null && _persistentVmMap.TryGetValue(rootNode.Path, out var rootVm))
            {
                currentVm = rootVm;
            }

            if (currentVm != null && !currentVm.IsExpanded)
            {
                currentVm.SetExpansionStateInternal(true);
                RefreshFlatList();
            }

            foreach (var segment in segments)
            {
                if (currentNode == null) break;

                if (currentNode is ObjectNode objectNode)
                {
                    currentNode = objectNode.GetChild(segment);
                }
                else if (currentNode is ArrayNode arrayNode)
                {
                    if (int.TryParse(segment, out int index))
                    {
                        currentNode = arrayNode.GetItem(index);
                    }
                    else
                    {
                        currentNode = null;
                    }
                }
                else
                {
                    currentNode = null;
                }

                if (currentNode != null && _persistentVmMap.TryGetValue(currentNode.Path, out var childVm))
                {
                    if (!childVm.IsExpanded)
                    {
                        childVm.SetExpansionStateInternal(true);
                        RefreshFlatList();
                    }
                }
            }
        }

        private void ClearMappingsRecursive(DomNode node)
        {
            _domToSchemaMap.Remove(node.Path);
            _validationIssuesMap.Remove(node.Path);
            _persistentVmMap.Remove(node.Path);

            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren().ToList())
                {
                    ClearMappingsRecursive(child);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems().ToList())
                {
                    ClearMappingsRecursive(item);
                }
            }
        }

        private void ExecuteDeleteSelectedNodes(DataGridRowItemViewModel? item)
        {
            if (item?.DomNode != null)
            {
                var nodeToRemove = item.DomNode;
                var parent = nodeToRemove.Parent;
                if (parent != null)
                {
                    int originalIndex = -1;
                    if (parent is ArrayNode arrayParent)
                    {
                        originalIndex = arrayParent.Items.ToList().IndexOf(nodeToRemove);
                    }
                    // FIX: Pass the active layer index to the operation.
                    var activeLayerIndex = ActiveEditorLayer?.LayerIndex ?? 0;
                    var operation = new RemoveNodeOperation(activeLayerIndex, parent, nodeToRemove, nodeToRemove.Name, originalIndex);
                    operation.Redo(this); // Execute immediately for structural changes
                    _historyService.Record(operation);
                }
            }
        }

        private bool CanExecuteDeleteSelectedNodes(DataGridRowItemViewModel? item)
        {
            return item?.DomNode != null && item.DomNode.Parent != null;
        }

        private void ExecuteExpandSelectedRecursive(DataGridRowItemViewModel? item)
        {
            if (item != null)
            {
                SetExpansionRecursive(item, true);
            }
        }

        private void ExecuteCollapseSelectedRecursive(DataGridRowItemViewModel? item)
        {
            if (item != null)
            {
                SetExpansionRecursive(item, false);
            }
        }

        private bool CanExecuteExpandCollapseSelectedRecursive(DataGridRowItemViewModel? item)
        {
            return item != null && item.IsExpandable;
        }

        private void SetExpansionRecursive(DataGridRowItemViewModel vm, bool expand)
        {
            vm.SetExpansionStateInternal(expand);
            if (vm.DomNode is ObjectNode objectNode)
            {
                foreach (var childNode in objectNode.GetChildren())
                {
                    if (_persistentVmMap.TryGetValue(childNode.Path, out var childVm))
                    {
                        SetExpansionRecursive(childVm, expand);
                    }
                }
            }
            else if (vm.DomNode is ArrayNode arrayNode)
            {
                foreach (var childNode in arrayNode.GetItems())
                {
                    if (_persistentVmMap.TryGetValue(childNode.Path, out var childVm))
                    {
                        SetExpansionRecursive(childVm, expand);
                    }
                }
            }
            RefreshFlatList();
        }

        public bool? GetSchemaNodeExpansionState(string pathKey)
        {
            if (_schemaNodeExpansionState.TryGetValue(pathKey, out bool isExpanded))
            {
                return isExpanded;
            }
            return null;
        }

        public void SetSchemaNodeExpansionState(string pathKey, bool isExpanded)
        {
            _schemaNodeExpansionState[pathKey] = isExpanded;
        }

        /// <summary>
        /// [Placeholder] Navigates the UI to the source of a given integrity issue.
        /// </summary>
        public void NavigateToIssue(IssueViewModel issue)
        {
            // Logic will be implemented in a later stage.
        }

        /// <summary>
        /// [Placeholder] Sets the active editor layer by its index.
        /// </summary>
        public void SetSelectedEditorLayerByIndex(int layerIndex)
        {
            // Logic will be implemented in a later stage.
        }

        /// <summary>
        /// [Placeholder] Runs the selected integrity checks.
        /// </summary>
        public void ExecuteIntegrityCheck(IntegrityCheckType checksToRun)
        {
            // Logic will be implemented in a later stage.
        }

        // This method replaces direct access to the old _rootDomNode field.
        public DomNode? GetRootDomNode()
        {
            // For now, in single-file mode, always return the root of the first and only layer.
            return _cascadeLayers.FirstOrDefault()?.LayerConfigRootNode;
        }

        public DomNode? FindDomNodeByPath(string path)
        {
            var rootNode = GetRootDomNode();
            if (rootNode == null) return null;

            if (string.IsNullOrEmpty(path) || path == "$root") return rootNode;

            string normalizedPathKey = path.StartsWith("$root/") ? path.Substring("$root/".Length) : path;
            if (string.IsNullOrEmpty(normalizedPathKey)) return rootNode;

            string[] segments = normalizedPathKey.Split('/');
            DomNode? current = rootNode;

            foreach (string segment in segments)
            {
                if (current == null) return null;

                if (current is ObjectNode objNode)
                {
                    current = objNode.GetChild(segment);
                    if (current == null) return null;
                }
                else if (current is ArrayNode arrNode)
                {
                    if (int.TryParse(segment, out int index))
                    {
                        current = arrNode.GetItem(index);
                        if (current == null) return null;
                    }
                    else return null;
                }
                else return null;
            }
            return current;
        }
        internal void AddNodeWithHistory(ObjectNode parent, DomNode newNode, string name)
        {
            // FIX: Pass the active layer index to the operation.
            var activeLayerIndex = ActiveEditorLayer?.LayerIndex ?? 0;
            var addOperation = new AddNodeOperation(activeLayerIndex, parent, newNode);
            addOperation.Redo(this); // Execute immediately for structural changes
            _historyService.Record(addOperation);
        }
        
        public void ReplaceRootWithHistory(DomNode newRoot)
        {
            var oldRoot = GetRootDomNode();
            // FIX: Pass the active layer index to the operation.
            var activeLayerIndex = ActiveEditorLayer?.LayerIndex ?? 0;
            var op = new ReplaceRootOperation(activeLayerIndex, oldRoot, newRoot);
            op.Redo(this); // Execute immediately
            _historyService.Record(op);
        }
        
        // This helper method is now needed for operations to call back into the ViewModel
        public void SetNodeValue(ValueNode node, JsonElement value)
        {
            node.Value = value;
            if (_persistentVmMap.TryGetValue(node.Path, out var vm))
            {
                OnNodeValueChanged(vm);
            }
            else
            {
                IsDirty = true;
                OnPropertyChanged(nameof(WindowTitle));
                _ = ValidateDocumentAsync();
            }
        }
          
        // This helper method is now needed for operations to call back into the ViewModel
        public void AddNodeToParent(DomNode parent, DomNode child)
        {
             if (parent is ObjectNode obj) obj.AddChild(child.Name, child);
             else if (parent is ArrayNode arr) arr.InsertItem(arr.Items.Count, child);
        }
          
        // This helper method is now needed for operations to call back into the ViewModel
        public void RemoveNodeFromParent(DomNode child)
        {
            if (child == null) return;

            var parent = child.Parent;
            ClearMappingsRecursive(child);

            if (parent is ObjectNode objectParent)
            {
                objectParent.RemoveChild(child.Name);
            }
            else if (parent is ArrayNode arrayParent)
            {
                arrayParent.RemoveItem(child);
            }
            else if (child == GetRootDomNode())
            {
                // This case should not happen in normal operation, but handle it gracefully
                var rootNode = GetRootDomNode();
                if (rootNode == child) 
                {
                    // Clear the cascade layers if we're removing the root
                    _cascadeLayers.Clear();
                }
            }
        }
        
		/// <summary>
		/// This helper method is now needed for operations to call back into the ViewModel.
		/// It replaces the root node of the currently active layer (in this stage, always the first layer).
		/// </summary>
		public void SetRootNode(DomNode? newRoot)
		{
		    // Ensure we have a valid ObjectNode. If the new root is null or not an object,
		    // create a new empty root. This handles the "New File" scenario.
		    var newObjectRoot = newRoot as ObjectNode ?? new ObjectNode("$root", null);

		    // Get the currently active layer. In later stages, this will use `ActiveEditorLayer`.
		    // For now, we assume a single-file context, so we operate on the first layer.
		    var activeLayer = _cascadeLayers.FirstOrDefault();
		    if (activeLayer != null)
		    {
		        // Replace the root node of the specific layer.
		        activeLayer.LayerConfigRootNode = newObjectRoot;
		    }
		    else
		    {
		        // If no layers exist, create a new one to hold the new root.
		        var newLayer = new CascadeLayer(0, "Default", "", new List<SourceFileInfo>(), newObjectRoot, new Dictionary<string, string>());
		        _cascadeLayers = new List<CascadeLayer> { newLayer };
		    }

		    IsDirty = true;
		    RebuildDomToSchemaMapping();
		    RefreshFlatList();
    
		    // After refresh, find the ViewModel for the new root to select it in the UI
		    if (_persistentVmMap.TryGetValue(newObjectRoot.Path, out var newRootVm))
		    {
		        SelectedGridItem = newRootVm;
		    }
		    else
		    {
		        SelectedGridItem = FlatItemsSource.FirstOrDefault();
		    }
		}
    }
}

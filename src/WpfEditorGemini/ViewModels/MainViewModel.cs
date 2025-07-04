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
        private readonly ProjectLoader _projectLoader;
        private readonly CascadedDomDisplayMerger _displayMerger;

        // --- Private Fields: Core Data State ---
        // REMOVE the old root node field:
        // private DomNode? _rootDomNode;
        // ADD the new list for cascade layers:
        private List<CascadeLayer> _cascadeLayers = new();

        // NEW: Add fields to store the full result of a display merge
        private ObjectNode? _displayRoot;
        private Dictionary<string, int> _valueOrigins = new();
        private Dictionary<string, List<int>> _overrideSources = new();

        // --- Add a new region for Cascade State ---
        #region Cascade State
        private CascadeLayer? _activeEditorLayer;
        public CascadeLayer? ActiveEditorLayer
        {
            get => _activeEditorLayer;
            set
            {
                if (SetProperty(ref _activeEditorLayer, value))
                {
                    // When the active layer changes, dispatch a refresh to the UI thread.
                    // This ensures the data binding for the ComboBox's SelectedItem has completed
                    // before we try to re-read the value and rebuild the list.
                    Application.Current?.Dispatcher.BeginInvoke(
                        new Action(() => RefreshDisplay()), 
                        System.Windows.Threading.DispatcherPriority.DataBind
                    );
                }
            }
        }

        private bool _isMergedViewActive = true;
        public bool IsMergedViewActive
        {
            get => _isMergedViewActive;
            set
            {
                if (SetProperty(ref _isMergedViewActive, value))
                {
                    // When the view mode changes, refresh the display
                    Application.Current?.Dispatcher.BeginInvoke(
                        new Action(() => RefreshDisplay()), 
                        System.Windows.Threading.DispatcherPriority.DataBind
                    );
                }
            }
        }

        // This will be bound to the new ComboBox
        public ObservableCollection<CascadeLayer> AllLayers { get; } = new();

        // A helper to determine if cascade UI should be visible
        public bool IsCascadeModeActive => AllLayers.Count > 1;
        #endregion
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
        public ICommand LoadCascadeProjectCommand { get; }

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
            _projectLoader = new ProjectLoader(_jsonParser); // Add this
            _displayMerger = new CascadedDomDisplayMerger(); // Add this
            _viewModelBuilder = new ViewModelBuilderService(_placeholderProvider, this);

            NewFileCommand = new RelayCommand(ExecuteNewFile);
            OpenFileCommand = new RelayCommand(ExecuteOpenFile);
            LoadCascadeProjectCommand = new RelayCommand(ExecuteLoadCascadeProject);
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
                // Get the unique path of the node that was just added or structurally changed.
                string? pathToSelect = operation.NodePath;

                RefreshDisplay(); // Rebuilds the list and the persistent VM map.

                // After the refresh, find the new ViewModel and select it.
                if (pathToSelect != null && _persistentVmMap.TryGetValue(pathToSelect, out var vmToSelect))
                {
                    SelectedGridItem = vmToSelect;
                }
            }
            else if (operation.NodePath != null)
            {
                // This is for simple value changes that don't require a full refresh.
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

        private async void ExecuteLoadCascadeProject()
        {
            if (!CheckUnsavedChanges()) return;

            var openDialog = new OpenFileDialog
            {
                Filter = "Cascade Project files (*.cascade.jsonc)|*.cascade.jsonc|All files (*.*)|*.*",
                DefaultExt = "cascade.jsonc"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    // Use the ProjectLoader service
                    var loadedLayersData = await _projectLoader.LoadProjectAsync(openDialog.FileName);
                      
                    _cascadeLayers.Clear();
                    AllLayers.Clear();

                    int layerIndex = 0;
                    foreach (var layerData in loadedLayersData)
                    {
                        var mergeResult = _intraLayerMerger.Merge(layerData);
                        // Handle errors from intra-layer merge if necessary
                        var cascadeLayer = new CascadeLayer(
                            layerIndex++,
                            layerData.Definition.Name,
                            layerData.Definition.FolderPath,
                            layerData.SourceFiles,
                            mergeResult.LayerConfigRootNode,
                            mergeResult.IntraLayerValueOrigins
                        );
                        _cascadeLayers.Add(cascadeLayer);
                        AllLayers.Add(cascadeLayer);
                    }

                    // Set initial state
                    CurrentFilePath = openDialog.FileName;
                    IsDirty = false;
                    ActiveEditorLayer = _cascadeLayers.LastOrDefault(); // Default to highest priority layer
                    OnPropertyChanged(nameof(IsCascadeModeActive)); // Notify UI to show cascade controls
                      
                    RefreshDisplay(); // This will handle schema mapping, validation, and list building
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open cascade project: {ex.Message}", "Open Project Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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
                AllLayers.Clear();
                AllLayers.Add(singleLayer);
                ActiveEditorLayer = _cascadeLayers.FirstOrDefault(); // Set the active layer
                OnPropertyChanged(nameof(IsCascadeModeActive)); // Notify UI to show cascade controls
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
                if (ActiveEditorLayer == null) return false;

                // Find the "real" parent array in the source layer's data model.
                // The 'parentArray' parameter is from the display clone.
                var realParentArray = FindNodeInSourceLayer(parentArray.Path, ActiveEditorLayer.LayerIndex) as ArrayNode;

                // If the array doesn't exist in the active layer, we can't add to it.
                // A full implementation would need to create an override of the array first.
                if (realParentArray == null)
                {
                    // For now, this operation fails if the array is inherited.
                    return false;
                }

                // 1. Create the new node from the user's value.
                var newNode = _nodeFactory.CreateFromValue(editValue, realParentArray.Items.Count.ToString(), realParentArray, itemSchema);
                if (newNode == null) return false;

                // Create the operation using the real parent array from the source model.
                var operation = new AddNodeOperation(ActiveEditorLayer.LayerIndex, realParentArray, newNode);
                operation.Redo(this);
                _historyService.Record(operation);
                return true;

                return true;
            }
            catch
            {
                return false;
            }
        }

        internal bool CreateNodeFromSchemaWithValue(DataGridRowItemViewModel schemaOnlyVm, string finalValueStr)
        {
            if (ActiveEditorLayer == null || schemaOnlyVm.SchemaContextNode == null) return false;

            try
            {
                var parentNode = ActiveEditorLayer.LayerConfigRootNode;
                var pathSegments = schemaOnlyVm.SchemaNodePathKey.Split('/');
                var parentPathSegments = pathSegments.Take(pathSegments.Length - 1);

                // 1. Manually walk the path and create parent nodes in the source model directly,
                // without triggering any history events or refreshes.
                foreach (var segment in parentPathSegments)
                {
                    if (string.IsNullOrEmpty(segment) || segment == "$root") continue;

                    var childNode = parentNode.GetChild(segment);
                    if (childNode is ObjectNode existingObject)
                    {
                        parentNode = existingObject;
                    }
                    else
                    {
                        // If a parent doesn't exist, create it and add it directly to the source tree.
                        // The Undo for this entire action will be the removal of the final leaf node.
                        var newParent = new ObjectNode(segment, parentNode);
                        parentNode.AddChild(segment, newParent);
                        parentNode = newParent;
                    }
                }

                // 2. Create the new leaf node with its final value.
                string nodeName = pathSegments.Last();
                JsonElement finalValue = CreateJsonElementFromString(finalValueStr, schemaOnlyVm.SchemaContextNode.ClrType);
                var newNode = new ValueNode(nodeName, parentNode, finalValue);

                // 3. Now, perform the SINGLE Add operation for the completed node and record it.
                // This will trigger the one-and-only refresh on the final, correct data model.
                AddNodeWithHistory(parentNode, newNode, nodeName);
                
                return true;
            }
            catch (Exception)
            {
                // This can fail if a property already exists (e.g. from a parallel edit)
                return false;
            }
        }

        // Helper method to create a JsonElement from a string, based on a target type.
        private JsonElement CreateJsonElementFromString(string stringValue, Type targetType)
        {
            if (targetType == typeof(bool) && bool.TryParse(stringValue, out bool bVal))
            {
                return bVal ? JsonDocument.Parse("true").RootElement : JsonDocument.Parse("false").RootElement;
            }
            if ((targetType == typeof(int) || targetType == typeof(long)) && long.TryParse(stringValue, out long lVal))
            {
                return JsonDocument.Parse(lVal.ToString()).RootElement;
            }
            if ((targetType == typeof(double) || targetType == typeof(float) || targetType == typeof(decimal)) && double.TryParse(stringValue, System.Globalization.CultureInfo.InvariantCulture, out double dVal))
            {
                return JsonDocument.Parse(dVal.ToString(System.Globalization.CultureInfo.InvariantCulture)).RootElement;
            }
            // Fallback to a JSON string if no other type matches.
            return JsonDocument.Parse($"\"{JsonEncodedText.Encode(stringValue)}\"").RootElement;
        }

        // Helper to find/create the parent node. We can reuse the node factory for this.
        private ObjectNode? FindOrCreateParentInSourceLayer(string childPath)
        {
            if (ActiveEditorLayer == null) return null;

            string parentPathKey;
            if (!childPath.Contains('/'))
            {
                parentPathKey = "$root";
            }
            else
            {
                parentPathKey = childPath.Substring(0, childPath.LastIndexOf('/'));
            }

            var parentNode = _nodeFactory.MaterializeDomPathRecursive(parentPathKey, SchemaLoader.FindSchemaForPath(parentPathKey));
            return parentNode as ObjectNode;
        }

        private void InitializeEmptyDocument()
        {
            var root = new ObjectNode("$root", null);
            var layer = new CascadeLayer(0, "Default", "", new List<SourceFileInfo>(), root, new Dictionary<string, string>());
            _cascadeLayers = new List<CascadeLayer> { layer };
            AllLayers.Clear();
            AllLayers.Add(layer);
            ActiveEditorLayer = _cascadeLayers.FirstOrDefault(); // Set the active layer
            OnPropertyChanged(nameof(IsCascadeModeActive)); // Notify UI to show cascade controls

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

        private void RefreshDisplay()
        {
            // Determine the current root node for display
            if (IsMergedViewActive && IsCascadeModeActive && ActiveEditorLayer != null)
            {
                // MERGED VIEW
                // Get all layers that should be part of the merge
                var layersToMerge = _cascadeLayers.Where(l => l.LayerIndex <= ActiveEditorLayer.LayerIndex).ToList();
        
                // For now, we create an empty schema defaults node (Layer 0).
                // This will be replaced by a real implementation later.
                var schemaDefaultsRoot = new ObjectNode("$root", null);

                // Use the merger to get the complete result
                var mergeResult = _displayMerger.MergeForDisplay(layersToMerge, schemaDefaultsRoot);

                _displayRoot = mergeResult.MergedRoot;
                _valueOrigins = mergeResult.ValueOrigins;
                _overrideSources = mergeResult.OverrideSources;
            }
            else
            {
                // SINGLE LAYER VIEW
                _displayRoot = ActiveEditorLayer?.LayerConfigRootNode;
                // Clear the origin maps as they don't apply in single-layer view
                _valueOrigins.Clear();
                _overrideSources.Clear();

                // Populate the origins map so that every node is correctly
                // associated with the single active layer.
                if (_displayRoot != null && ActiveEditorLayer != null)
                {
                    PopulateOriginsForSingleLayer(_displayRoot, ActiveEditorLayer.LayerIndex, _valueOrigins);
                }
            }
    
            // Now that the display tree is set, update the rest of the UI
            RebuildDomToSchemaMapping();
            RefreshFlatList();
            _ = ValidateDocumentAsync();
            OnPropertyChanged(nameof(WindowTitle));
        }

        /// <summary>
        /// Recursively populates the value origins map for a single layer's view.
        /// </summary>
        private void PopulateOriginsForSingleLayer(DomNode node, int layerIndex, Dictionary<string, int> origins)
        {
            origins[node.Path] = layerIndex;
            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    PopulateOriginsForSingleLayer(child, layerIndex, origins);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    PopulateOriginsForSingleLayer(item, layerIndex, origins);
                }
            }
        }

    private void RefreshFlatList()
        {
            // Persist the expansion state from the current ViewModels into the state dictionary.
            foreach (var vm in _persistentVmMap.Values)
            {
                string pathKey = vm.DomNode?.Path ?? vm.SchemaNodePathKey;
                if (!string.IsNullOrEmpty(pathKey))
                {
                    _schemaNodeExpansionState[pathKey] = vm.IsExpanded;
                }
            }

            // Now, we can safely clear the ViewModel instances. New ones will be created
            // and will read their state from the dictionary we just populated.
            _persistentVmMap.Clear();

            var rootNode = GetRootDomNode();
            if (rootNode == null)
            {
                FlatItemsSource.Clear();
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
                    _valueOrigins,
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
                    _valueOrigins,
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
            if (item?.DomNode == null || ActiveEditorLayer == null) return;

            // The item.DomNode is from the display clone. Find the real node in the source model.
            var nodeToRemove = FindNodeInSourceLayer(item.DomNode.Path, ActiveEditorLayer.LayerIndex);

            // If the node isn't in the active layer, it's inherited and cannot be deleted from here.
            if (nodeToRemove == null || nodeToRemove.Parent == null)
            {
                return;
            }

            var parent = nodeToRemove.Parent;
            int originalIndex = -1;
            if (parent is ArrayNode arrayParent)
            {
                originalIndex = arrayParent.Items.ToList().IndexOf(nodeToRemove);
            }

            // Create the operation with the real nodes from the source model.
            var operation = new RemoveNodeOperation(ActiveEditorLayer.LayerIndex, parent, nodeToRemove, nodeToRemove.Name, originalIndex);
            operation.Redo(this);
      
            _historyService.Record(operation);
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
            if (layerIndex >= 0 && layerIndex < _cascadeLayers.Count)
            {
                ActiveEditorLayer = _cascadeLayers[layerIndex];
            }
        }

        /// <summary>
        /// [Placeholder] Runs the selected integrity checks.
        /// </summary>
        public void ExecuteIntegrityCheck(IntegrityCheckType checksToRun)
        {
            // Logic will be implemented in a later stage.
        }

        public DomNode? GetRootDomNode()
        {
            return _displayRoot;
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
		    _displayRoot = newRoot as ObjectNode;
		    RefreshDisplay();
		}

        /// <summary>
        /// Creates an override for an inherited value in the currently active editor layer.
        /// </summary>
        /// <param name="path">The full path of the node to override.</param>
        /// <param name="value">The new value as a string.</param>
        /// <param name="schema">The schema for the node being created.</param>
        public void CreateOverride(string path, string value, SchemaNode? schema)
        {
            if (ActiveEditorLayer == null) return;

            // 1. Ensure the parent path exists in the active layer's tree.
            // This will create any necessary parent ObjectNodes.
            var segments = path.Split('/');
            var parentPath = string.Join('/', segments.Take(segments.Length - 1));
            var nodeName = segments.Last();
              
            // We need a method to do this, let's call it EnsurePathExistsInLayer
            var parentNode = EnsurePathExistsInLayer(parentPath, ActiveEditorLayer);
            if (parentNode == null) return;

            // 2. Use the factory to create the new ValueNode.
            var newNode = _nodeFactory.CreateFromValue(value, nodeName, parentNode, schema);

            // 3. Add the new node to the parent in the active layer's tree.
            // This action should be recorded for undo/redo.
            AddNodeWithHistory(parentNode, newNode, nodeName);
              
            // 4. Mark the layer as dirty and refresh the UI.
            ActiveEditorLayer.IsDirty = true;
            RefreshDisplay();
        }

        /// <summary>
        /// A helper method to recursively create parent nodes in a specific layer's DOM tree.
        /// </summary>
/// <summary>
        /// A helper method to recursively create parent nodes in a specific layer's DOM tree.
        /// </summary>
        private ObjectNode EnsurePathExistsInLayer(string path, CascadeLayer layer)
        {
            var current = layer.LayerConfigRootNode;

            // If the path is just the root, return the layer's root itself.
            if (string.IsNullOrEmpty(path) || path == "$root")
            {
                return current;
            }

            // Normalize the path to be relative to the root, removing any leading "$root/"
            string normalizedPath = path.StartsWith("$root/") ? path.Substring("$root/".Length) : path;
            
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return current;
            }

            var segments = normalizedPath.Split('/');
            foreach (var segment in segments)
            {
                if (string.IsNullOrEmpty(segment)) continue;
                var child = current.GetChild(segment);
                if (child is ObjectNode childObject)
                {
                    current = childObject;
                }
                else if (child != null)
                {
                    // A node exists at this path but it's not an object (e.g., a ValueNode).
                    // We cannot create children under it. This is an error condition.
                    // Returning null to indicate failure.
                    return null;
                }
                else
                {
                    // The segment doesn't exist, so create it.
                    var newParent = new ObjectNode(segment, current);
                    // Use the ViewModel method that correctly handles history.
                    AddNodeWithHistory(current, newParent, segment); 
                    current = newParent;
                }
            }
            return current;
        }

        public DomNode? FindNodeInSourceLayer(string path, int originLayerIndex)
        {
            // The originLayerIndex is -1 for schema, or 0, 1, 2... for file layers.
            if (originLayerIndex < 0 || originLayerIndex >= _cascadeLayers.Count)
            {
                return null; // It's a schema node or an invalid index
            }

            var sourceLayer = _cascadeLayers[originLayerIndex];
            var layerRoot = sourceLayer.LayerConfigRootNode;

            // Find the node by path within this layer's specific tree.
            if (string.IsNullOrEmpty(path) || path == "$root") return layerRoot;

            string normalizedPath = path.StartsWith("$root/") ? path.Substring("$root/".Length) : path;
            if (string.IsNullOrEmpty(normalizedPath)) return layerRoot;

            string[] segments = normalizedPath.Split('/');
            DomNode? current = layerRoot;
            foreach (string segment in segments)
            {
                if (current == null) return null;
                if (current is ObjectNode objNode)
                {
                    current = objNode.GetChild(segment);
                }
                else if (current is ArrayNode arrNode)
                {
                    if (int.TryParse(segment, out int index))
                    {
                        current = arrNode.GetItem(index);
                    }
                    else return null;
                }
                else return null;
            }
            return current;
        }
    }
}

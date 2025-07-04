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

            string? pathToSelect = null;

            // Determine which path to select AFTER the refresh.
            if (operation is RemoveNodeOperation)
            {
                // For a delete, we find the next logical item based on the current UI state.
                var itemToSelect = FindNextItemToSelectForDeletion(SelectedGridItem);
                // We get its path BEFORE the refresh so we can find the new VM after the refresh.
                pathToSelect = itemToSelect?.DomNode?.Path ?? itemToSelect?.SchemaNodePathKey;
            }
            else
            {
                // For Add, Edit, or other operations, we want to select the node the operation affected.
                pathToSelect = operation.NodePath;
            }

            // Now, perform the required UI refresh.
            if (operation.RequiresFullRefresh)
            {
                RefreshDisplay();
            }
            else if (operation.NodePath != null && _persistentVmMap.TryGetValue(operation.NodePath, out var vmToUpdate))
            {
                // This lightweight update works for simple value changes.
                vmToUpdate.RefreshDisplayProperties();
            }

            // Finally, apply the focus to the determined item.
            if (pathToSelect != null && _persistentVmMap.TryGetValue(pathToSelect, out var vmToSelect))
            {
                SelectedGridItem = vmToSelect;
            }
            else
            {
                SelectedGridItem = null;
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

        internal bool AddArrayItem(ArrayNode parentArrayFromClone, string editValue, SchemaNode? itemSchema)
        {
            if (ActiveEditorLayer == null) return false;

            var realParentArray = FindNodeInSourceLayer(parentArrayFromClone.Path, ActiveEditorLayer.LayerIndex) as ArrayNode;
            
            // Case 1: The array already exists in the active layer. Add the item directly.
            if (realParentArray != null)
            {
                var newNode = _nodeFactory.CreateFromValue(editValue, realParentArray.Items.Count.ToString(), realParentArray, itemSchema);
                if (newNode == null) return false;
                AddNodeWithHistory(realParentArray, newNode, newNode.Name);
            }
            // Case 2: The array is inherited. Use the helper to create an override.
            else 
            {
                Action<ArrayNode> addAction = (array) => {
                    var newNode = _nodeFactory.CreateFromValue(editValue, array.Items.Count.ToString(), array, itemSchema);
                    if (newNode != null) array.AddItem(newNode);
                };
                CreateArrayOverride(parentArrayFromClone, addAction, "add");
            }
            return true;
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

            var originIndex = item.OriginLayerIndex;

            if (originIndex == ActiveEditorLayer.LayerIndex)
            {
                var nodeToRemove = FindNodeInSourceLayer(item.DomNode.Path, ActiveEditorLayer.LayerIndex);
                if (nodeToRemove?.Parent == null) return;

                var parent = nodeToRemove.Parent;
                int originalIndex = (parent is ArrayNode ap) ? ap.IndexOf(nodeToRemove) : -1;
                var operation = new RemoveNodeOperation(ActiveEditorLayer.LayerIndex, parent, nodeToRemove, nodeToRemove.Name, originalIndex);
                
                operation.Redo(this);
                _historyService.Record(operation);
            }
            else if (item.DomNode.Parent is ArrayNode inheritedParentArray)
            {
                Action<ArrayNode> deleteAction = (array) => {
                    var itemToRemove = array.Items.FirstOrDefault(i => i.Path.EndsWith(item.DomNode.Name));
                    if (itemToRemove != null) array.RemoveItem(itemToRemove);
                };
                CreateArrayOverride(inheritedParentArray, deleteAction, "remove");
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
            return FindNodeByPathFromRoot(GetRootDomNode(), path);
        }

        // Change the type of the 'parent' parameter from ObjectNode to DomNode
        internal void AddNodeWithHistory(DomNode parent, DomNode newNode, string name)
        {
            // FIX: Pass the active layer index to the operation.
            var activeLayerIndex = ActiveEditorLayer?.LayerIndex ?? 0;
            var addOperation = new AddNodeOperation(activeLayerIndex, parent, newNode);
            addOperation.Redo(this);
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

        public void CreateOverride(string path, string value, SchemaNode? schema)
        {
            if (ActiveEditorLayer == null) return;

            string parentPath = path.Substring(0, path.LastIndexOf('/'));
            var parentSchema = SchemaLoader.FindSchemaForPath(parentPath);

            if (parentSchema?.NodeType == SchemaNodeType.Array)
            {
                var inheritedArray = FindDomNodeByPath(parentPath) as ArrayNode;
                if (inheritedArray == null) return;

                Action<ArrayNode> editAction = (array) => {
                    var itemToEdit = array.Items.FirstOrDefault(i => i.Path.EndsWith(path.Split('/').Last()));
                    if (itemToEdit is ValueNode valueNode)
                    {
                        valueNode.TryUpdateFromString(value);
                    }
                };
                CreateArrayOverride(inheritedArray, editAction, "edit");
            }
            else
            {
                var parentNode = FindOrCreateParentInSourceLayer_SchemaAware(path);
                if (parentNode is not ObjectNode parentObject) return;

                var nodeName = path.Split('/').Last();
                var newNode = _nodeFactory.CreateFromValue(value, nodeName, parentObject, schema);
                AddNodeWithHistory(parentObject, newNode, nodeName);
                
                ActiveEditorLayer.IsDirty = true;
                RefreshDisplay();
            }
        }

        private DomNode? FindOrCreateParentInSourceLayer_SchemaAware(string leafNodePath)
        {
            if (ActiveEditorLayer == null) return null;

            var segments = leafNodePath.Split('/').Where(s => !string.IsNullOrEmpty(s) && s != "$root").ToList();
            if (segments.Count <= 1) return ActiveEditorLayer.LayerConfigRootNode; 

            var parentSegments = segments.Take(segments.Count - 1);
            ObjectNode currentParent = ActiveEditorLayer.LayerConfigRootNode;

            foreach (var segment in parentSegments)
            {
                var childNode = currentParent.GetChild(segment);
                if (childNode == null)
                {
                    // This is the line to fix:
                    string newPath = currentParent.Path == "$root" ? $"$root/{segment}" : $"{currentParent.Path}/{segment}";
                    
                    var segmentSchema = SchemaLoader.FindSchemaForPath(newPath);

                    if (segmentSchema?.NodeType == SchemaNodeType.Array)
                    {
                        var newArray = new ArrayNode(segment, currentParent);
                        AddNodeWithHistory(currentParent, newArray, segment);
                        return newArray; 
                    }
                    else
                    {
                        var newObject = new ObjectNode(segment, currentParent);
                        AddNodeWithHistory(currentParent, newObject, segment);
                        currentParent = newObject;
                    }
                }
                else if (childNode is ObjectNode obj)
                {
                    currentParent = obj;
                }
                else if (childNode is ArrayNode arr)
                {
                    return arr;
                }
                else
                {
                    return null;
                }
            }
            return currentParent;
        }

        public DomNode? FindNodeInSourceLayer(string path, int originLayerIndex)
        {
            if (originLayerIndex < 0 || originLayerIndex >= _cascadeLayers.Count)
            {
                return null; // It's a schema node or an invalid index
            }

            var sourceLayer = _cascadeLayers[originLayerIndex];
            return FindNodeByPathFromRoot(sourceLayer.LayerConfigRootNode, path);
        }

        private DataGridRowItemViewModel? FindNextItemToSelectForDeletion(DataGridRowItemViewModel? deletedItem)
        {
            if (deletedItem == null || !FlatItemsSource.Any()) return null;

            int deletedIndex = FlatItemsSource.IndexOf(deletedItem);
            if (deletedIndex == -1) return null;

            int deletedDepth = deletedItem.DomNode?.Depth ?? (int)(deletedItem.Indentation.Left / 20);

            // 1. Look forward in the list to find the next item that is NOT a descendant.
            //    A non-descendant will have a depth less than or equal to the deleted item.
            for (int i = deletedIndex + 1; i < FlatItemsSource.Count; i++)
            {
                var nextItem = FlatItemsSource[i];
                int nextDepth = nextItem.DomNode?.Depth ?? (int)(nextItem.Indentation.Left / 20);
                if (nextDepth <= deletedDepth)
                {
                    return nextItem; // Found the next sibling or an "uncle" node.
                }
            }

            // 2. If we didn't find a "next" item (i.e., we deleted the last node in a group or the last overall),
            //    simply select the item that is now at the deleted item's original index, which will be its
            //    former last child's new sibling, or the previous item. This is a safe fallback.
            if (deletedIndex > 0)
            {
                // After deletion, the list will be smaller. The item at the previous index is a safe bet.
                return FlatItemsSource[deletedIndex - 1];
            }

            // 3. If all else fails (e.g. deleting the first and only item), try to select the parent.
            if (deletedItem.DomNode?.Parent?.Path != null)
            {
                return _persistentVmMap.GetValueOrDefault(deletedItem.DomNode.Parent.Path);
            }

            return null;
        }

        private void CreateArrayOverride(ArrayNode inheritedArray, Action<ArrayNode> modificationAction, string actionVerb)
        {
            if (ActiveEditorLayer == null) return;

            // 1. Show a confirmation dialog with a specific message based on the action.
            var layerName = ActiveEditorLayer.Name;
            var arrayName = inheritedArray.Name;
            var message = $"To {actionVerb} an item in this inherited array, a new, overriding version of the entire '{arrayName}' array will be created in the active '{layerName}' layer.\n\nDo you want to proceed?";
            
            var result = MessageBox.Show(message, "Confirm Array Override", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            // 2. Ensure the array's parent object exists in the active layer.
            var grandparentNode = FindOrCreateParentInSourceLayer_SchemaAware(inheritedArray.Path);
            if (grandparentNode is not ObjectNode parentObject) return;

            // 3. Create the new array that will live in the active layer.
            var newArrayInActiveLayer = new ArrayNode(inheritedArray.Name, parentObject);

            // 4. Populate the new array by cloning the items from the inherited array.
            foreach (var itemToClone in inheritedArray.Items)
            {
                newArrayInActiveLayer.AddItem(DomCloning.CloneNode(itemToClone, newArrayInActiveLayer));
            }
            
            // 5. Apply the specific modification (add, edit, or delete) to the new array.
            modificationAction(newArrayInActiveLayer);

            // 6. Add the final, modified array to the active layer's data model.
            AddNodeWithHistory(parentObject, newArrayInActiveLayer, newArrayInActiveLayer.Name);
            
            // 7. Mark dirty and refresh the UI (this might be better outside the helper)
            ActiveEditorLayer.IsDirty = true;
            RefreshDisplay();
        }


        private DomNode? FindNodeByPathFromRoot(DomNode? rootNode, string path)
        {
            if (rootNode == null) return null;

            if (string.IsNullOrEmpty(path) || path == rootNode.Path) return rootNode;

            string normalizedPath = path.StartsWith("$root/") ? path.Substring("$root/".Length) : path;
            if (string.IsNullOrEmpty(normalizedPath)) return rootNode;

            string[] segments = normalizedPath.Split('/');
            DomNode? current = rootNode;
            foreach (string segment in segments)
            {
                if (current == null) return null;

                if (current is ObjectNode objNode)
                {
                    current = objNode.GetChild(segment);
                }
                else if (current is ArrayNode arrNode)
                {
                    current = arrNode.Items.FirstOrDefault(item => item.Name == segment);
                }
                else
                {
                    // Cannot traverse into a ValueNode or RefNode.
                    return null;
                }
            }
            return current;
        }
    }
}

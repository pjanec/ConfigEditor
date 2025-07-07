using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.Core.Parsing;
using JsonConfigEditor.Core.Schema;
using JsonConfigEditor.Core.SchemaLoading;
using JsonConfigEditor.Core.Serialization;
using JsonConfigEditor.Core.Settings;
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
using JsonConfigEditor.Views; // Add this for IntegrityCheckDialog
using JsonConfigEditor.ViewModels; // Add this for new ViewModels

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
        private readonly ProjectSaver _projectSaver; // Add this field
        // ADD the new intra-layer merger service
        private readonly IntraLayerMerger _intraLayerMerger;
        private readonly ProjectLoader _projectLoader;
        private readonly CascadedDomDisplayMerger _displayMerger;
        private readonly CriticalErrorScanner _errorScanner;
        // Add the new service as a private field
        private readonly CaseMismatchChecker _caseMismatchChecker;
        private readonly UserSettingsService _userSettingsService;
        public UserSettingsModel UserSettings { get; private set; }

        // --- Private Fields: Core Data State ---
        // REMOVE the old root node field:
        // private DomNode? _rootDomNode;
        // ADD the new list for cascade layers:
        private List<CascadeLayer> _cascadeLayers = new();

        // NEW: Add fields to store the full result of a display merge
        private ObjectNode? _displayRoot;
        private Dictionary<string, int> _authoritativeValueOrigins = new();
        private Dictionary<string, List<int>> _authoritativeOverrideSources = new();

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
        private readonly Dictionary<string, SchemaNode?> _domToSchemaMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ValidationIssue>> _validationIssuesMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DataGridRowItemViewModel> _persistentVmMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _schemaNodeExpansionState = new(StringComparer.OrdinalIgnoreCase); // For schema-only node states

        // --- Private Fields: UI State & Filters/Search ---
        private string? _currentFilePath;
        // REMOVE the old private field for IsDirty:
        // private bool _isDirty;
        private string _filterText = string.Empty;
        private bool _showOnlyInvalidNodes;
        private bool _showSchemaNodes = true; // Toggle for DOM vs DOM+Schema view
        private string _searchText = string.Empty;
        private DataGridRowItemViewModel? _currentlyEditedItem;
        private DataGridRowItemViewModel? _selectedGridItem; // For TwoWay binding with DataGrid
        private bool _isDiagnosticsPanelVisible;
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
        public ICommand RunIntegrityCheckCommand { get; }
        public ICommand DismissCriticalErrorCommand { get; }
        public ICommand CheckProjectStructureCommand { get; }
        public ICommand ToggleDiagnosticsPanelCommand { get; }
        public ICommand ToggleMergedViewCommand { get; }
        public ICommand ToggleSchemaNodesCommand { get; }
        public ICommand ToggleInvalidOnlyCommand { get; }
        public ICommand AddNewSiblingNodeCommand { get; }
        public ICommand AddNewChildNodeCommand { get; }
        public ICommand AddItemAboveCommand { get; }
        public ICommand AddItemBelowCommand { get; }

        // --- Public Properties ---

        public CustomUIRegistryService UiRegistry => _uiRegistry;
        public ISchemaLoaderService SchemaLoader => _schemaLoader;
        public EditHistoryService HistoryService => _historyService; // Expose the service

        public ObservableCollection<DataGridRowItemViewModel> FlatItemsSource { get; } = new();
        public ObservableCollection<IssueViewModel> Issues { get; } = new();
        public ObservableCollection<string> LogMessages { get; } = new();

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
                RefreshFlatList(GetCurrentViewSpecificOrigins());
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
                    RefreshFlatList(GetCurrentViewSpecificOrigins());
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
                    RefreshFlatList(GetCurrentViewSpecificOrigins());
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
            // The project is considered dirty if ANY layer has unsaved changes.
            get => _cascadeLayers.Any(l => l.IsDirty);
        }

        public bool IsDiagnosticsPanelVisible
        {
            get => _isDiagnosticsPanelVisible;
            set => SetProperty(ref _isDiagnosticsPanelVisible, value);
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

        private bool _hasCriticalErrors;
        public bool HasCriticalErrors
        {
            get => _hasCriticalErrors;
            private set => SetProperty(ref _hasCriticalErrors, value);
        }

        private bool _showCriticalErrorNotification = true;
        public bool ShowCriticalErrorNotification
        {
            get => _showCriticalErrorNotification;
            set => SetProperty(ref _showCriticalErrorNotification, value);
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
            _projectSaver = new ProjectSaver(_jsonSerializer); // Initialize the saver
            _viewModelBuilder = new ViewModelBuilderService(_placeholderProvider, this);
            _errorScanner = new CriticalErrorScanner();
            _caseMismatchChecker = new CaseMismatchChecker();
            _userSettingsService = new UserSettingsService();
            // Load settings synchronously in the constructor for simplicity,
            // or adapt to an async initialization pattern if preferred.
            UserSettings = Task.Run(() => _userSettingsService.LoadSettingsAsync()).GetAwaiter().GetResult();

            NewFileCommand = new RelayCommand(ExecuteNewFile);
            OpenFileCommand = new RelayCommand(ExecuteOpenFile);
            LoadCascadeProjectCommand = new RelayCommand(ExecuteLoadCascadeProject);
            RunIntegrityCheckCommand = new RelayCommand(ExecuteRunIntegrityCheck, () => IsCascadeModeActive);
            DismissCriticalErrorCommand = new RelayCommand(ExecuteDismissCriticalError);
            CheckProjectStructureCommand = new RelayCommand(ExecuteCheckProjectStructure, () => IsCascadeModeActive);
            ToggleDiagnosticsPanelCommand = new RelayCommand(ExecuteToggleDiagnosticsPanel);
            ToggleMergedViewCommand = new RelayCommand(ExecuteToggleMergedView);
            ToggleSchemaNodesCommand = new RelayCommand(ExecuteToggleSchemaNodes);
            ToggleInvalidOnlyCommand = new RelayCommand(ExecuteToggleInvalidOnly);
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
            AddNewSiblingNodeCommand = new RelayCommand(param => ExecuteAddNewNode(param as DataGridRowItemViewModel, isChild: false), param => CanExecuteAddNewNode(param as DataGridRowItemViewModel));
            AddNewChildNodeCommand = new RelayCommand(param => ExecuteAddNewNode(param as DataGridRowItemViewModel, isChild: true), param => CanExecuteAddNewChildNode(param as DataGridRowItemViewModel));
            AddItemAboveCommand = new RelayCommand(param => ExecuteAddItem(param as DataGridRowItemViewModel, insertAbove: true), param => param is DataGridRowItemViewModel);
            AddItemBelowCommand = new RelayCommand(param => ExecuteAddItem(param as DataGridRowItemViewModel, insertAbove: false), param => param is DataGridRowItemViewModel);

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
            // The command can execute if there are any layers loaded AND the project is dirty.
            return _cascadeLayers.Any() && IsDirty;
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

        private void OnHistoryModelChanged(EditOperation operation)
        {
            // When an undo/redo operation occurs, mark the specific layer as dirty.
            if (operation.LayerIndex >= 0 && operation.LayerIndex < _cascadeLayers.Count)
            {
                _cascadeLayers[operation.LayerIndex].IsDirty = true;
            }
            // Notify that the computed IsDirty property may have changed.
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(WindowTitle));

            _ = Task.Run(() => ValidateDocumentAsync());

            if (operation.RequiresFullRefresh)
            {
                // --- OPTIMIZATION ---
                // Only recalculate the authoritative origin maps after a structural change.
                RecalculateAuthoritativeOriginMaps();
                // --- END OPTIMIZATION ---

                string? pathToSelect = operation.NodePath; // Default path

                // NEW: Special logic to handle focus after deleting an array item
                if (operation is RemoveNodeOperation removeOp)
                {
                    var parent = removeOp.ParentNode;
                    if (parent is ArrayNode arrayParent)
                    {
                        int originalIndex = removeOp.OriginalIndexInArray;
                        
                        // After deletion, the arrayParent.Count is already one less than before.
                        if (originalIndex < arrayParent.Count)
                        {
                            // If we didn't delete the last item, select the item that took its place.
                            // Its new path will end with the index `originalIndex`.
                            pathToSelect = $"{parent.Path}/{originalIndex}";
                        }
                        else if (arrayParent.Count > 0)
                        {
                            // If we deleted the last item, select the new last item.
                            int newLastIndex = arrayParent.Count - 1;
                            pathToSelect = $"{parent.Path}/{newLastIndex}";
                        }
                        else
                        {
                            // If the array is now empty, select the parent array itself.
                            pathToSelect = parent.Path;
                        }
                    }
                    else
                    {
                        // Fallback to the old logic for non-array deletions (object properties)
                        var itemToSelect = FindNextItemToSelectForDeletion(SelectedGridItem);
                        pathToSelect = itemToSelect?.DomNode?.Path ?? itemToSelect?.SchemaNodePathKey;
                    }
                }

                // Pass the correctly calculated path directly into the refresh process.
                RefreshDisplay(pathToSelect);
            }
            else if (operation is ValueEditOperation valueOp && valueOp.NodePath != null)
            {
                if (_persistentVmMap.TryGetValue(valueOp.NodePath, out var vmToUpdate))
                {
                    var newValue = valueOp.Node.Value;
                    vmToUpdate.SyncValueFromModel(newValue);
                    SelectedGridItem = vmToUpdate;
                }
            }
        }

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

        // REPLACE the entire ExecuteLoadCascadeProject method
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
                    var projectLoadResult = await _projectLoader.LoadProjectAsync(openDialog.FileName);

                    // 1. Load schemas first, including defaults and project-specific ones.
                    var allSchemaPaths = new List<string> { Assembly.GetExecutingAssembly().Location };
                    allSchemaPaths.AddRange(projectLoadResult.SchemaAssemblyPaths);
                    await LoadSchemasAsync(allSchemaPaths.Distinct());

                    // 2. Clear and populate diagnostic panels from schema and project loading.
                    Issues.Clear();
                    LogMessages.Clear();
                    foreach (var log in _schemaLoader.LogMessages) { LogMessages.Add(log); }
                    foreach (var error in _schemaLoader.ErrorMessages.Concat(projectLoadResult.Errors))
                    {
                        var issue = new IntegrityIssue(ValidationSeverity.Error, error, "Project Load");
                        Issues.Add(new IssueViewModel(issue, this));
                    }

                    // 3. Process the layers from the project load result.
                    _cascadeLayers.Clear();
                    AllLayers.Clear();
                    int layerIndex = 0;
                    foreach (var layerData in projectLoadResult.LayerData)
                    {
                        foreach (var error in layerData.Errors)
                        {
                            var issue = new IntegrityIssue(ValidationSeverity.Error, error, layerData.Definition.Name);
                            Issues.Add(new IssueViewModel(issue, this));
                        }

                        var mergeResult = _intraLayerMerger.Merge(layerData);
                        var cascadeLayer = new CascadeLayer(
                            layerIndex++,
                            layerData.Definition.Name,
                            layerData.AbsoluteFolderPath,
                             layerData.SourceFiles,
                            mergeResult.LayerConfigRootNode,
                            mergeResult.IntraLayerValueOrigins
                        );
                        _cascadeLayers.Add(cascadeLayer);
                        AllLayers.Add(cascadeLayer);
                    }

                    var criticalIssues = _errorScanner.Scan(projectLoadResult.LayerData);
                    if (criticalIssues.Any())
                    {
                        NotifyUserOfCriticalErrors(criticalIssues);
                    }

                    var caseWarnings = _caseMismatchChecker.Scan(_cascadeLayers, _schemaLoader);
                    foreach (var warning in caseWarnings)
                    {
                        Issues.Add(new IssueViewModel(warning, this));
                    }

                    // 4. Finalize state and refresh UI
                    RecalculateAuthoritativeOriginMaps();
                    if (Issues.Any())
                    {
                        IsDiagnosticsPanelVisible = true;
                    }

                    CurrentFilePath = openDialog.FileName;
                    ActiveEditorLayer = _cascadeLayers.LastOrDefault();
                    OnPropertyChanged(nameof(IsCascadeModeActive)); 

                    RefreshDisplay();
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
                var fileContent = await File.ReadAllTextAsync(filePath);
                var domRoot = _jsonParser.ParseFromString(fileContent) as ObjectNode;

                if (domRoot == null)
                {
                    throw new InvalidOperationException("The root of the loaded JSON file must be an object.");
                }

                var fileName = Path.GetFileName(filePath);
                // Create a map to track that all nodes come from this one file.
                var intraLayerOrigins = new Dictionary<string, string>();
                TrackOriginsForSingleFile(domRoot, fileName, intraLayerOrigins);
                var sourceFile = new SourceFileInfo(filePath, fileName, domRoot, fileContent, 0);

                var singleLayer = new CascadeLayer(
                    layerIndex: 0,
                    name: "Default",
                    folderPath: Path.GetDirectoryName(filePath)!,
                    sourceFiles: new List<SourceFileInfo> { sourceFile },
                    layerConfigRootNode: domRoot,
                    // Assign the newly created origin map to the layer.
                    intraLayerValueOrigins: intraLayerOrigins
                );

                _cascadeLayers = new List<CascadeLayer> { singleLayer };
                AllLayers.Clear();
                AllLayers.Add(singleLayer);
                ActiveEditorLayer = _cascadeLayers.FirstOrDefault();
                OnPropertyChanged(nameof(IsCascadeModeActive));
                  
                _authoritativeValueOrigins.Clear();
                _authoritativeOverrideSources.Clear();
                PopulateOriginsForSingleFile(singleLayer.LayerConfigRootNode, 0, _authoritativeValueOrigins, _authoritativeOverrideSources);
                  
                CurrentFilePath = filePath;

                ResetSearchState();
                RefreshDisplay();
                await ValidateDocumentAsync();
                OnPropertyChanged(nameof(WindowTitle));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load file: {ex.Message}", ex);
            }
        }

        // Add this new helper method to the MainViewModel class
        private void PopulateOriginsForSingleFile(DomNode node, int layerIndex, 
            Dictionary<string, int> valueOrigins, Dictionary<string, List<int>> overrideSources)
        {
            valueOrigins[node.Path] = layerIndex;
            overrideSources[node.Path] = new List<int> { layerIndex };

            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    PopulateOriginsForSingleFile(child, layerIndex, valueOrigins, overrideSources);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    PopulateOriginsForSingleFile(item, layerIndex, valueOrigins, overrideSources);
                }
            }
        }

        // Add this new helper method to the MainViewModel class
        private void TrackOriginsForSingleFile(DomNode node, string fileName, Dictionary<string, string> origins)
        {
            origins[node.Path] = fileName;

            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    TrackOriginsForSingleFile(child, fileName, origins);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    TrackOriginsForSingleFile(item, fileName, origins);
                }
            }
        }

        public async Task SaveFileAsync(string? filePath = null)
        {
            var targetPath = filePath ?? CurrentFilePath;
            if (string.IsNullOrEmpty(targetPath))
                throw new InvalidOperationException("No file path specified for save operation");

            // Check if we are in "Project Mode" by inspecting the file extension.
            if (CurrentFilePath?.EndsWith(".cascade.jsonc", StringComparison.OrdinalIgnoreCase) == true)
            {
                // In project mode, 'Save As' is not a simple file copy. We'll disallow it for now
                // and save changes back to the original layer files.
                if (filePath != null && filePath != CurrentFilePath)
                {
                    MessageBox.Show("Saving a project to a new location ('Save As') is not supported. Changes will be saved to the original project structure.", "Save As Not Supported", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Iterate through only the dirty layers and save them using the ProjectSaver.
                foreach (var layer in _cascadeLayers.Where(l => l.IsDirty))
                {
                    await _projectSaver.SaveLayerAsync(layer);
                }
            }
            else // We are in "Single-File Mode".
            {
                var rootNodeToSave = _cascadeLayers.FirstOrDefault()?.LayerConfigRootNode;
                if (rootNodeToSave == null) return;

                await _jsonSerializer.SerializeToFileAsync(rootNodeToSave, targetPath);
                CurrentFilePath = targetPath; // Update path if 'Save As' was used.

                // Mark the single layer as no longer dirty.
                var singleLayer = _cascadeLayers.FirstOrDefault();
                if (singleLayer != null) singleLayer.IsDirty = false;
            }

            // After saving, notify the UI that the dirty state and window title need to be re-evaluated.
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(WindowTitle));
        }

        public void NewDocument()
        {
            InitializeEmptyDocument();
            CurrentFilePath = null;
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(WindowTitle));
        }

        public async Task LoadSchemasAsync(IEnumerable<string> assemblyPaths)
        {
            await _schemaLoader.LoadSchemasFromAssembliesAsync(assemblyPaths);
            RebuildDomToSchemaMapping();
            RefreshFlatList(GetCurrentViewSpecificOrigins());
            await ValidateDocumentAsync();
        }

        internal void OnExpansionChanged(DataGridRowItemViewModel item)
        {
            RefreshFlatList(GetCurrentViewSpecificOrigins());
        }

        internal void SetCurrentlyEditedItem(DataGridRowItemViewModel? item)
        {
            _currentlyEditedItem = item;
        }

        internal void OnNodeValueChanged(DataGridRowItemViewModel item)
        {
            // When a value is changed, mark the active layer as dirty.
            if (ActiveEditorLayer != null)
            {
                ActiveEditorLayer.IsDirty = true;
            }
            // Notify that the computed IsDirty property may have changed.
            OnPropertyChanged(nameof(IsDirty));
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

                // *** CHANGE: Instead of calling AddNodeWithHistory, call the central method ***
                FinalizeNodeCreation(parentNode, newNode);
                
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

        // Helper method to create a JsonElement from an object, based on a target type.
        private JsonElement CreateJsonElementFromObject(object? defaultValue, Type targetType)
        {
            if (defaultValue == null)
            {
                return JsonDocument.Parse("null").RootElement;
            }

            if (targetType == typeof(bool))
            {
                return (bool)defaultValue ? JsonDocument.Parse("true").RootElement : JsonDocument.Parse("false").RootElement;
            }
            if (targetType == typeof(int) || targetType == typeof(long))
            {
                return JsonDocument.Parse(defaultValue.ToString()!).RootElement;
            }
            if (targetType == typeof(double) || targetType == typeof(float) || targetType == typeof(decimal))
            {
                return JsonDocument.Parse(defaultValue.ToString()!).RootElement;
            }
            // Fallback to a JSON string if no other type matches.
            return JsonDocument.Parse($"\"{JsonEncodedText.Encode(defaultValue.ToString()!)}\"").RootElement;
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
            RefreshFlatList(GetCurrentViewSpecificOrigins());
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

        private void RefreshDisplay(string? pathToSelect = null)
        {
            Dictionary<string, int> viewSpecificValueOrigins;
              
            if (IsMergedViewActive && IsCascadeModeActive && ActiveEditorLayer != null)
            {
                var layersToMerge = _cascadeLayers
                    .Where(l => l.LayerIndex <= ActiveEditorLayer.LayerIndex)
                    .ToList();
                var schemaDefaultsRoot = new ObjectNode("$root", null);
                  
                var viewSpecificMergeResult = _displayMerger.MergeForDisplay(layersToMerge, schemaDefaultsRoot);
                _displayRoot = viewSpecificMergeResult.MergedRoot;
                viewSpecificValueOrigins = viewSpecificMergeResult.ValueOrigins;
            }
            else
            {
                _displayRoot = ActiveEditorLayer?.LayerConfigRootNode;
                viewSpecificValueOrigins = new Dictionary<string, int>();
                if (_displayRoot != null && ActiveEditorLayer != null)
                {
                    // The PopulateOriginsForSingleLayer method is added back here
                    PopulateOriginsForSingleLayer(_displayRoot, ActiveEditorLayer.LayerIndex, viewSpecificValueOrigins);
                }
            }

            RebuildDomToSchemaMapping();
            RefreshFlatList(viewSpecificValueOrigins, pathToSelect); // Pass the view-specific map
            _ = ValidateDocumentAsync();
            OnPropertyChanged(nameof(WindowTitle));
        }

        // Add this helper method back to MainViewModel
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

        // Helper method to get current view-specific origin data
        private Dictionary<string, int> GetCurrentViewSpecificOrigins()
        {
            if (IsMergedViewActive && IsCascadeModeActive && ActiveEditorLayer != null)
            {
                var layersToMerge = _cascadeLayers
                    .Where(l => l.LayerIndex <= ActiveEditorLayer.LayerIndex)
                    .ToList();
                var schemaDefaultsRoot = new ObjectNode("$root", null);
                var viewSpecificMergeResult = _displayMerger.MergeForDisplay(layersToMerge, schemaDefaultsRoot);
                return viewSpecificMergeResult.ValueOrigins;
            }
            else if (_displayRoot != null && ActiveEditorLayer != null)
            {
                var viewSpecificValueOrigins = new Dictionary<string, int>();
                PopulateOriginsForSingleLayer(_displayRoot, ActiveEditorLayer.LayerIndex, viewSpecificValueOrigins);
                return viewSpecificValueOrigins;
            }
            else
            {
                return new Dictionary<string, int>();
            }
        }

    private void RefreshFlatList(Dictionary<string, int> viewSpecificValueOrigins, string? pathToSelect = null)
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

            // Use the explicit path if provided; otherwise, fall back to the currently selected item.
            string? selectedPathIdentifier = pathToSelect ?? (SelectedGridItem?.DomNode?.Path ?? (SelectedGridItem?.IsSchemaOnlyNode == true ? SelectedGridItem.SchemaNodePathKey : null));
              
            List<DataGridRowItemViewModel> tempFlatList;
            if (!string.IsNullOrEmpty(FilterText))
            {
                var visiblePaths = _filterService.GetVisibleNodePaths(rootNode, FilterText);
                tempFlatList = _viewModelBuilder.BuildList(
                    rootNode, 
                    _persistentVmMap, 
                    _domToSchemaMap, 
                    viewSpecificValueOrigins, // Use the view-specific map
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
                    viewSpecificValueOrigins, // Use the view-specific map
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

            // 1. Create a NEW, LOCAL dictionary. This will be safely populated on the background thread.
            var localIssuesMap = new Dictionary<string, List<ValidationIssue>>();

            await Task.Run(() =>
            {
                // This code now operates ONLY on the local dictionary, not the shared _validationIssuesMap.
                var tempDomToSchemaMap = new Dictionary<DomNode, SchemaNode?>();
                BuildTemporaryDomToSchemaMap(rootNode, tempDomToSchemaMap);
                var issuesByNode = _validationService.ValidateTree(rootNode, tempDomToSchemaMap);

                foreach (var issueEntry in issuesByNode)
                {
                    localIssuesMap[issueEntry.Key.Path] = issueEntry.Value;
                }
            });

            // 2. We are now back on the UI thread. It is safe to modify the shared collection.
            _validationIssuesMap.Clear();
            foreach (var entry in localIssuesMap)
            {
                _validationIssuesMap.Add(entry.Key, entry.Value);
            }

            // 3. Now that the shared map is safely updated, update the validation state on all visible ViewModels.
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
                RefreshFlatList(GetCurrentViewSpecificOrigins());
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
                        RefreshFlatList(GetCurrentViewSpecificOrigins());
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

            // Case 1: Direct delete in the active layer.
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
            // Case 2: Deleting an item FROM an inherited array.
            else if (item.DomNode.Parent is ArrayNode inheritedParentArray)
            {
                Action<ArrayNode> deleteAction = (array) => {
                    var itemToRemove = array.Items.FirstOrDefault(i => i.Path.EndsWith(item.DomNode.Name));
                    if (itemToRemove != null) array.RemoveItem(itemToRemove);
                };
                CreateArrayOverride(inheritedParentArray, deleteAction, "remove an item from");
            }
            // Case 3: Deleting an ENTIRE inherited array.
            else if (item.DomNode is ArrayNode inheritedArrayToDelete)
            {
                var layerName = ActiveEditorLayer.Name;
                var arrayName = inheritedArrayToDelete.Name;
                var message = $"The '{arrayName}' array is inherited from a lower-level layer.\n\nTo hide it, a new, empty array will be created in the active '{layerName}' layer. This will override the inherited array.\n\nDo you want to proceed?";
                
                // Use YesNoCancel to allow closing the dialog with the Escape key.
                var result = MessageBox.Show(message, "Confirm Override", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                var parentNode = FindOrCreateParentInSourceLayer_SchemaAware(inheritedArrayToDelete.Path);
                if (parentNode is not ObjectNode parentObject) return;
                
                var newEmptyArray = new ArrayNode(inheritedArrayToDelete.Name, parentObject);
                AddNodeWithHistory(parentObject, newEmptyArray, newEmptyArray.Name);
            }
            // Case 4: Catch-all for trying to delete any other inherited node.
            else
            {
                var nodeName = item.NodeName;
                var message = $"The node '{nodeName}' is inherited and cannot be deleted from this layer.\n\nTo hide an inherited property, you must create an override for it.";
                MessageBox.Show(message, "Cannot Delete Inherited Node", MessageBoxButton.OK, MessageBoxImage.Information);
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
            RefreshFlatList(GetCurrentViewSpecificOrigins());
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
        /// Generates the list of ViewModels for the "Override Sources" context submenu  
        /// for a given node path.  
        /// </summary>  
        public List<LayerMenuItemViewModel> GetOverrideSourceLayersForNode(string nodePath)  
        {  
            var menuItems = new List<LayerMenuItemViewModel>();  
            if (!IsCascadeModeActive) return menuItems;

            _authoritativeOverrideSources.TryGetValue(nodePath, out var definingLayerIndices);  
            _authoritativeValueOrigins.TryGetValue(nodePath, out var effectiveLayerIndex);

            foreach (var layer in _cascadeLayers)  
            {  
                bool isDefined = definingLayerIndices?.Contains(layer.LayerIndex) ?? false;  
                bool isEffective = layer.LayerIndex == effectiveLayerIndex;

                menuItems.Add(new LayerMenuItemViewModel(  
                    layerName: layer.Name,  
                    layerIndex: layer.LayerIndex,  
                    isDefinedInThisLayer: isDefined,  
                    isEffectiveInThisLayer: isEffective,  
                    mainViewModel: this  
                ));  
            }  
            return menuItems;  
        }

        // Add this new public method to MainViewModel
        public List<LayerMenuItemViewModel> GetAuthoritativeOverrideLayersForNode(string nodePath)
        {
            var menuItems = new List<LayerMenuItemViewModel>();
            if (!IsCascadeModeActive) return menuItems;

            // Use the authoritative map for override sources
            _authoritativeOverrideSources.TryGetValue(nodePath, out var definingLayerIndices);
            // Use the authoritative map for the winning value
            _authoritativeValueOrigins.TryGetValue(nodePath, out var effectiveLayerIndex);

            foreach (var layer in _cascadeLayers)
            {
                bool isDefined = definingLayerIndices?.Contains(layer.LayerIndex) ?? false;
                bool isEffective = layer.LayerIndex == effectiveLayerIndex;

                menuItems.Add(new LayerMenuItemViewModel(
                    layerName: layer.Name,
                    layerIndex: layer.LayerIndex,
                    isDefinedInThisLayer: isDefined,
                    isEffectiveInThisLayer: isEffective,
                    mainViewModel: this
                ));
            }
            return menuItems;
        }

        private void ExecuteRunIntegrityCheck()
        {
            // Pass the loaded settings to the dialog's ViewModel
            var dialog = new IntegrityCheckDialog(this, UserSettings.IntegrityChecks.ChecksToRun);
            dialog.ShowDialog();
        }

        private void ExecuteDismissCriticalError()
        {
            ShowCriticalErrorNotification = false;
        }

        private void ExecuteCheckProjectStructure()
        {
            if (!IsCascadeModeActive)
            {
                MessageBox.Show("Project structure check is only available for cascade projects.", "Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Collect all proposed consolidations from all layers
            var allProposedConsolidations = new List<ConsolidationAction>();
            
            foreach (var layer in _cascadeLayers)
            {
                // Create a LayerDefinitionModel from the existing layer data
                var layerDefinition = new LayerDefinitionModel(layer.Name, layer.FolderPath);
                
                // Re-run the intra-layer merger to get consolidation proposals
                var layerData = new LayerLoadResult(
                    layerDefinition,
                    layer.FolderPath,
                    layer.SourceFiles,
                    new List<string>() // Empty errors list for this re-run
                );
                
                var mergeResult = _intraLayerMerger.Merge(layerData);
                if (mergeResult.ProposedConsolidations.Any())
                {
                    allProposedConsolidations.AddRange(mergeResult.ProposedConsolidations);
                }
            }

            if (allProposedConsolidations.Any())
            {
                var dialog = new ConsolidationDialog(allProposedConsolidations, selectedActions =>
                {
                    // This is the callback that applies the changes.
                    ApplyConsolidationActions(selectedActions);
                });
                dialog.ShowDialog();
            }
            else
            {
                MessageBox.Show("No project structure improvements found. Your project structure is already optimal.", "No Changes Needed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExecuteToggleDiagnosticsPanel()
        {
            IsDiagnosticsPanelVisible = !IsDiagnosticsPanelVisible;
        }

        private void ExecuteToggleMergedView()
        {
            IsMergedViewActive = !IsMergedViewActive;
        }

        private void ExecuteToggleSchemaNodes()
        {
            ShowSchemaNodes = !ShowSchemaNodes;
        }

        private void ExecuteToggleInvalidOnly()
        {
            ShowOnlyInvalidNodes = !ShowOnlyInvalidNodes;
        }

        private bool CanExecuteAddNewNode(DataGridRowItemViewModel? item)
        {
            // Can add a sibling to any existing node.
            return item?.IsDomNodePresent == true;
        }

        private bool CanExecuteAddNewChildNode(DataGridRowItemViewModel? item)
        {
            // Can only add a child to an existing ObjectNode.
            return item?.DomNode is ObjectNode;
        }

        private ObjectNode? FindOrCreateObjectNodeInSource(string path)
        {
            if (ActiveEditorLayer == null) return null;

            var segments = path.Split('/').Where(s => !string.IsNullOrEmpty(s) && s != "$root").ToList();
            ObjectNode currentParent = ActiveEditorLayer.LayerConfigRootNode;

            foreach (var segment in segments)
            {
                var childNode = currentParent.GetChild(segment);
                if (childNode == null)
                {
                    // Child doesn't exist, create it.
                    var newObject = new ObjectNode(segment, currentParent);
                    currentParent.AddChild(segment, newObject);
                    // Also track its origin to the most likely file
                    var deducedOrigin = DeduceOriginForNewNode(newObject);
                    if (deducedOrigin != null)
                    {
                        TrackOriginForNewNodeRecursive(newObject, deducedOrigin, ActiveEditorLayer.IntraLayerValueOrigins);
                    }
                    currentParent = newObject;
                }
                else if (childNode is ObjectNode obj)
                {
                    // Child exists and is an object, traverse into it.
                    currentParent = obj;
                }
                else
                {
                    // A node exists at this path but it's not an object (e.g., a value or array).
                    // This is an error condition. We cannot add a child to it.
                    return null;
                }
            }
            return currentParent;
        }

        private void ExecuteAddNewNode(DataGridRowItemViewModel? selectedItem, bool isChild)
        {
            if (selectedItem?.DomNode == null || ActiveEditorLayer == null) return;

            var displayParentNode = isChild ? selectedItem.DomNode : selectedItem.DomNode.Parent;
            if (displayParentNode == null) return;

            // Use the new helper to get the REAL parent from the source layer, creating it if necessary.
            var realParentObject = FindOrCreateObjectNodeInSource(displayParentNode.Path);
            if (realParentObject == null)
            {
                MessageBox.Show($"Cannot add a property to '{displayParentNode.Path}' because a non-object value already exists at this path in the '{ActiveEditorLayer.Name}' layer.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Action<string, string, NodeType?> onCommit = (propName, valueStr, selectedType) =>
            {
                var newNode = CreateNodeFromUserInput(propName, valueStr, selectedType, realParentObject);
                if (newNode == null) return;

                // FinalizeNodeCreation will now operate on the real parent object.
                FinalizeNodeCreation(realParentObject, newNode);
                ActiveEditorLayer.IsDirty = true;
            };

            Func<string, bool> isNameValid = (newName) => !realParentObject.HasProperty(newName);

            var vm = new AddNewNodeViewModel(onCommit, isNameValid);
            var dialog = new AddNewNodeDialog
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            dialog.ShowDialog();
        }

        private void ExecuteAddItem(DataGridRowItemViewModel? selectedItem, bool insertAbove)
        {
            if (selectedItem == null || ActiveEditorLayer == null) return;

            // Determine the target array based on the context
            ArrayNode? displayParentArray = null;
            if (selectedItem.DomNode is ArrayNode arrayNode)
            {
                displayParentArray = arrayNode; // Case: User clicked on an empty or populated array node itself.
            }
            else if (selectedItem.DomNode?.Parent is ArrayNode parent)
            {
                displayParentArray = parent; // Case: User clicked on an item within an array.
            }
            else if (selectedItem.IsAddItemPlaceholder)
            {
                // Case: User clicked the "(Add new item)" placeholder.
                displayParentArray = FindDomNodeByPath(selectedItem.SchemaNodePathKey.Substring(0, selectedItem.SchemaNodePathKey.Length - 5)) as ArrayNode;
            }
            else if (selectedItem.IsSchemaOnlyNode && selectedItem.NodeType == DataGridRowItemViewModel.ViewModelNodeType.Array)
            {
                // Case: User clicked a schema-only array. It must be materialized first.
                var materializedNode = _nodeFactory.MaterializeDomPathRecursive(selectedItem.SchemaNodePathKey, selectedItem.SchemaContextNode);
                displayParentArray = materializedNode as ArrayNode;
            }
              
            if (displayParentArray == null) return;

            var realParentArray = FindNodeInSourceLayer(displayParentArray.Path, ActiveEditorLayer.LayerIndex) as ArrayNode;

            Action<ArrayNode> insertAction = (targetArray) =>
            {
                int insertionIndex;
                if (selectedItem.DomNode == targetArray) // If we clicked the array itself
                {
                    insertionIndex = 0; // Add to the beginning
                }
                else if (selectedItem.IsAddItemPlaceholder)
                {
                    insertionIndex = targetArray.Count; // Add to the end
                }
                else
                {
                    var nodeInTarget = targetArray.Items.FirstOrDefault(i => i.Path.EndsWith(selectedItem.DomNode.Name));
                    int anIndex = (nodeInTarget != null) ? targetArray.IndexOf(nodeInTarget) : -1;
                    insertionIndex = insertAbove ? anIndex : anIndex + 1;
                }

                if (insertionIndex < 0) insertionIndex = 0;

                var arraySchema = _domToSchemaMap.GetValueOrDefault(targetArray.Path);
                var itemSchema = arraySchema?.ItemSchema;

                DomNode newNode;
                if (itemSchema != null)
                {
                    var defaultJson = CreateJsonElementFromObject(itemSchema.DefaultValue, itemSchema.ClrType);
                    newNode = new ValueNode(insertionIndex.ToString(), targetArray, defaultJson);
                }
                else
                {
                    newNode = new ValueNode(insertionIndex.ToString(), targetArray, JsonDocument.Parse("\"new item\"").RootElement);
                }

                var operation = new InsertArrayItemOperation(ActiveEditorLayer.LayerIndex, targetArray, newNode, insertionIndex);
                operation.Redo(this);
                HistoryService.Record(operation);
            };

            if (realParentArray != null)
            {
                insertAction(realParentArray);
            }
            else
            {
                CreateArrayOverride(displayParentArray, insertAction, "add an item to");
            }
        }

        private DomNode? CreateNodeFromUserInput(string name, string value, NodeType? selectedType, DomNode parent)
        {
            NodeType finalType = selectedType ?? DeduceTypeFromString(value);

            if (finalType == NodeType.String && value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
            {
                value = value.Substring(1, value.Length - 2);
            }

            return finalType switch
            {
                NodeType.Object => new ObjectNode(name, parent),
                NodeType.Array => new ArrayNode(name, parent),
                NodeType.Boolean => new ValueNode(name, parent, JsonDocument.Parse(bool.TryParse(value, out var b) && b ? "true" : "false").RootElement),
                NodeType.Null => new ValueNode(name, parent, JsonDocument.Parse("null").RootElement),
                NodeType.Number => double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num) ? new ValueNode(name, parent, JsonDocument.Parse(num.ToString(System.Globalization.CultureInfo.InvariantCulture)).RootElement) : null,
                _ => new ValueNode(name, parent, JsonDocument.Parse($"\"{JsonEncodedText.Encode(value)}\"").RootElement),
            };
        }

        private NodeType DeduceTypeFromString(string value)
        {
            if (value.StartsWith("\"") && value.EndsWith("\"")) return NodeType.String;
            if (value.Trim() == "{}") return NodeType.Object;
            if (value.Trim() == "[]") return NodeType.Array;
            if (bool.TryParse(value, out _)) return NodeType.Boolean;
            if (value.Equals("null", StringComparison.OrdinalIgnoreCase)) return NodeType.Null;
            if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _)) return NodeType.Number;
            return NodeType.String;
        }

        /// <summary>
        /// Runs the selected integrity checks.
        /// </summary>
        public void ExecuteIntegrityCheck(IntegrityCheckType checksToRun)
        {
            // *** NEW: Update the settings model before saving ***
            UserSettings.IntegrityChecks.ChecksToRun = checksToRun;
            _ = _userSettingsService.SaveSettingsAsync(UserSettings); // Save the updated settings

            if (!_cascadeLayers.Any()) return;

            Issues.Clear();

            var integrityChecker = new IntegrityChecker(); // Instantiate the service
            // Modify this line to pass the schema loader
            var issuesFound = integrityChecker.RunChecks(_cascadeLayers, _schemaLoader, checksToRun);

            if (issuesFound.Any())
            {
                foreach (var issue in issuesFound)
                {
                    Issues.Add(new IssueViewModel(issue, this));
                }
                IsDiagnosticsPanelVisible = true;
            }
            else
            {
                // If no issues are found, we can show a confirmation message.
                MessageBox.Show("No integrity issues found.", "Integrity Check Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                IsDiagnosticsPanelVisible = false;
            }
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
                // When a value is changed, mark the active layer as dirty.
                if (ActiveEditorLayer != null)
                {
                    ActiveEditorLayer.IsDirty = true;
                }
                // Notify that the computed IsDirty property may have changed.
                OnPropertyChanged(nameof(IsDirty));
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
                
                // *** CHANGE: Call the central method ***
                FinalizeNodeCreation(parentObject, newNode);

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
            
            // Use YesNoCancel to allow closing the dialog with the Escape key.
            var result = MessageBox.Show(message, "Confirm Array Override", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
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

            // *** CHANGE: Call the central method with the new array ***
            FinalizeNodeCreation(parentObject, newArrayInActiveLayer);

            // 7. Mark dirty and refresh the UI (this might be better outside the helper)
            ActiveEditorLayer.IsDirty = true;
            RefreshDisplay();
        }


        private DomNode? FindNodeByPathFromRoot(DomNode? rootNode, string path)
        {
            if (rootNode == null) return null;

            if (string.IsNullOrEmpty(path) || path.Equals(rootNode.Path, StringComparison.OrdinalIgnoreCase)) return rootNode;

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

        /// <summary>
        /// Gets the name of a layer by its index.
        /// </summary>
        /// <param name="layerIndex">The index of the layer.</param>
        /// <returns>The layer name, or null if the index is invalid.</returns>
        public string? GetLayerNameByIndex(int layerIndex)
        {
            if (layerIndex >= 0 && layerIndex < _cascadeLayers.Count)
            {
                return _cascadeLayers[layerIndex].Name;
            }
            return null;
        }

        private void TrackOriginForNewNodeRecursive(DomNode node, string relativeFilePath, Dictionary<string, string> origins)
        {
            origins[node.Path] = relativeFilePath;

            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    TrackOriginForNewNodeRecursive(child, relativeFilePath, origins);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    TrackOriginForNewNodeRecursive(item, relativeFilePath, origins);
                }
            }
        }

        // --- Stage 2 Implementation: Central File Assignment Workflow ---

        private bool IsNewFileRequiredForNode(DomNode node)
        {
            if (ActiveEditorLayer == null || node.Parent == null) return false;

            // A new file is needed if the node's immediate parent does not have a source file  
            // assigned to it within the currently active layer.
            return !ActiveEditorLayer.IntraLayerValueOrigins.ContainsKey(node.Parent.Path);
        }

        private List<string> GetSuggestedFilePaths(string domPath)
        {
            // A raw list to hold all potential suggestions with their priority.
            // Lower priority number = more important.
            var rawSuggestions = new List<(string Path, string Reason, int Priority)>();
            var segments = domPath.Split('/')
                .Where(s => !string.IsNullOrEmpty(s) && s != "$root")
                .ToList();

            if (segments.Count == 0)
            {
                // For a root-level property, the only option is root.json, with no reason.
                return new List<string> { "root.json" };
            }
    
            // 1. Add the primary path suggestion with a low priority (3). The reason is now empty.
            string primaryPath = $"{segments.First()}.json";
            rawSuggestions.Add((primaryPath, "", 3));

            // 2. Walk up the hierarchy from the direct parent to find precedents and other valid paths.
            for (int i = segments.Count - 1; i >= 1; i--)
            {
                var currentParentSegments = segments.Take(i).ToList();
                var currentParentPath = "$root/" + string.Join('/', currentParentSegments);

                var precedents = new Dictionary<string, List<string>>();
                int totalDefiningLayers = 0;
                foreach (var layer in _cascadeLayers.Where(l => l != ActiveEditorLayer))
                {
                    var filesForThisParent = layer.IntraLayerValueOrigins
                        .Where(kvp => kvp.Key.StartsWith(currentParentPath + "/", StringComparison.OrdinalIgnoreCase))
                        .Select(kvp => kvp.Value)
                        .Distinct().ToList();

                    if (filesForThisParent.Any())
                    {
                        totalDefiningLayers++;
                        foreach (var filePath in filesForThisParent)
                        {
                            if (!precedents.ContainsKey(filePath)) precedents[filePath] = new List<string>();
                            precedents[filePath].Add(layer.Name);
                        }
                    }
                }

                // Add found precedents to the raw list with a high priority (1 or 2).
                foreach (var precedent in precedents)
                {
                    bool isSameAsOthers = precedent.Value.Count == totalDefiningLayers && totalDefiningLayers > 1;
                    string reason = isSameAsOthers ? "same as other layers" : $"from '{string.Join("', '", precedent.Value)}'";
                    int priority = isSameAsOthers ? 1 : 2; // Priority 1 is the best.
                    rawSuggestions.Add((precedent.Key, reason, priority));
                }

                // Add the ancestor path as a fallback with the lowest priority (4). The reason is now empty.
                var currentParentName = currentParentSegments.Last();
                var directorySegments = currentParentSegments.Take(currentParentSegments.Count - 1);
                var directoryPath = string.Join('/', directorySegments);
                string ancestorPath = string.IsNullOrEmpty(directoryPath)
                    ? $"{currentParentName}.json"
                    : $"{directoryPath}/{currentParentName}.json";
        
                rawSuggestions.Add((ancestorPath, "", 4));
            }

            // 3. Process the raw list to get the final, sorted, and distinct list.
            var finalSuggestions = rawSuggestions
                .GroupBy(s => s.Path) // Group by the file path to handle duplicates.
                .Select(g => g.OrderBy(s => s.Priority).First()) // From each group, pick the one with the best (lowest) priority.
                .OrderBy(s => s.Priority) // Sort the final list by that priority.
                .ThenBy(s => s.Path)      // Then alphabetically for consistent ordering.
                .Select(s => string.IsNullOrWhiteSpace(s.Reason) ? s.Path : $"{s.Path}  ({s.Reason})") // Format for display.
                .ToList();

            return finalSuggestions;
        }

        private void FinalizeNodeCreation(DomNode parent, DomNode newNode)
        {
            if (ActiveEditorLayer == null) return;

            // Try to deduce the origin automatically first.
            var deducedOrigin = DeduceOriginForNewNode(newNode);

            if (deducedOrigin != null)
            {
                // SUCCESS: An origin was deduced. Assign it and add the node without prompting.
                TrackOriginForNewNodeRecursive(newNode, deducedOrigin, ActiveEditorLayer.IntraLayerValueOrigins);
                AddNodeWithHistory(parent, newNode, newNode.Name);
            }
            else
            {
                // FAILURE: No origin could be deduced. Prompt the user as a last resort.
                Action<string> onPathSelected = chosenPath =>
                {
                    // Assign the user's chosen path and add the node.
                    TrackOriginForNewNodeRecursive(newNode, chosenPath, ActiveEditorLayer.IntraLayerValueOrigins);
                    AddNodeWithHistory(parent, newNode, newNode.Name);
                };

                var suggestions = GetSuggestedFilePaths(newNode.Path);
                var activeLayerName = ActiveEditorLayer?.Name ?? "Unknown Layer";

                // Call the updated ViewModel constructor.
                var vm = new AssignSourceFileViewModel(suggestions, onPathSelected, newNode.Path, activeLayerName);
                var dialog = new AssignSourceFileDialog { DataContext = vm, Owner = Application.Current.MainWindow };

                dialog.ShowDialog();
            }
        }

        private void RecalculateAuthoritativeOriginMaps()
        {
            if (!IsCascadeModeActive || !_cascadeLayers.Any())
            {
                _authoritativeValueOrigins.Clear();
                _authoritativeOverrideSources.Clear();
                return;
            }

            var schemaDefaultsRoot = new ObjectNode("$root", null);
            var fullMergeResult = _displayMerger.MergeForDisplay(_cascadeLayers, schemaDefaultsRoot);
            _authoritativeValueOrigins = fullMergeResult.ValueOrigins;
            _authoritativeOverrideSources = fullMergeResult.OverrideSources;
        }

        // Add this new private method to MainViewModel
        private void ApplyConsolidationActions(List<ConsolidationAction> actionsToApply)
        {
            foreach (var action in actionsToApply)
            {
                // MODIFICATION: Find the layer directly by its name from the action.
                var layer = _cascadeLayers.FirstOrDefault(l => l.Name == action.LayerName);
                if (layer == null) continue;

                // Find all paths that need to be re-mapped.
                var pathsToRemap = layer.IntraLayerValueOrigins
                    .Where(kvp => kvp.Value.Equals(action.DescendantFile, StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Key)
                    .ToList();

                // Re-map them to the ancestor file.
                foreach (var path in pathsToRemap)
                {
                    layer.IntraLayerValueOrigins[path] = action.AncestorFile;
                }

                // Queue the descendant file for deletion.
                layer.FilesToDeleteOnSave.Add(action.DescendantFile);
                layer.IsDirty = true; // Mark layer as dirty to trigger save.
            }
            OnPropertyChanged(nameof(IsDirty)); // Notify UI that overall state may be dirty.
        }

        private void NotifyUserOfCriticalErrors(List<IntegrityIssue> issues)
        {
            Issues.Clear();
            foreach (var issue in issues)
            {
                // This line should be present and not commented out.
                // The IssueViewModel constructor is designed to take an IntegrityIssue.
                Issues.Add(new IssueViewModel(issue, this));
            }

            HasCriticalErrors = true;
            IsDiagnosticsPanelVisible = true; // Automatically open the panel
            SearchStatusText = $"Project loaded with {issues.Count} critical errors."; // Reuse status text property
        }

        /// <summary>
        /// Tries to deduce the destination file for a new node by checking its surroundings.
        /// </summary>
        /// <returns>The deduced file path, or null if no origin can be determined.</returns>
        private string? DeduceOriginForNewNode(DomNode node)
        {
            var parent = node.Parent;
            if (parent == null || ActiveEditorLayer == null)
            {
                return null;
            }

            // 1. First, check the immediate parent. This is the most direct relationship.
            if (ActiveEditorLayer.IntraLayerValueOrigins.TryGetValue(parent.Path, out var parentOrigin))
            {
                return parentOrigin;
            }

            // 2. If parent has no origin, check siblings and their descendants for a clue.
            Func<DomNode, string?>? findDescendantOrigin = null;
            findDescendantOrigin = (startNode) =>
            {
                if (startNode is ValueNode || startNode is RefNode)
                {
                    return ActiveEditorLayer.IntraLayerValueOrigins.GetValueOrDefault(startNode.Path);
                }
                if (startNode is ObjectNode obj)
                {
                    foreach (var child in obj.GetChildren())
                    {
                        var origin = findDescendantOrigin!(child);
                        if (origin != null) return origin;
                    }
                }
                else if (startNode is ArrayNode arr)
                {
                    foreach (var item in arr.GetItems())
                    {
                        var origin = findDescendantOrigin!(item);
                        if (origin != null) return origin;
                    }
                }
                return null;
            };
    
            // THE FIX: Get the list of siblings based on the parent's actual type.
            IEnumerable<DomNode> siblings;
            if (parent is ObjectNode objectParent)
            {
                siblings = objectParent.GetChildren();
            }
            else if (parent is ArrayNode arrayParent)
            {
                siblings = arrayParent.GetItems();
            }
            else
            {
                // The parent is a type that cannot have children, so there are no siblings.
                return null;
            }

            // Check all siblings of the new node.
            foreach (var sibling in siblings)
            {
                if (sibling == node) continue; // Don't check the new node itself
                var deducedOrigin = findDescendantOrigin(sibling);
                if (deducedOrigin != null)
                {
                    return deducedOrigin; // Found a home!
                }
            }

            // 3. No origin could be deduced from any surrounding properties.
            return null;
        }

        public async Task SaveCurrentUserSettings()
        {
            await _userSettingsService.SaveSettingsAsync(this.UserSettings);
        }
    }

}

using JsonConfigEditor.Core; // For EditorMode
using JsonConfigEditor.Core.Cascading; // For ICascadeProjectLoaderService, LayerDefinition, CascadeLayer, SourceFileInfo
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
        private readonly ICascadeProjectLoaderService _cascadeProjectLoader;

        // --- Private Fields: Core Data State ---
        private DomNode? _rootDomNode;
        private readonly Dictionary<DomNode, SchemaNode?> _domToSchemaMap = new();
        private readonly Dictionary<DomNode, List<ValidationIssue>> _validationIssuesMap = new();
        private readonly Dictionary<DomNode, DataGridRowItemViewModel> _persistentVmMap = new();
        private readonly Dictionary<string, bool> _schemaNodeExpansionState = new();

        // --- Cascade Project State ---
        public ObservableCollection<CascadeLayer> CascadeLayers { get; private set; } = new ObservableCollection<CascadeLayer>();
        private CascadeLayer? _selectedEditorLayer;
        private int _selectedEditorLayerIndex = -1;
        // private List<LayerDefinition> _layerDefinitions = new List<LayerDefinition>(); // No longer needed directly if CascadeLayers holds all info

        private EditorMode _editorMode = EditorMode.SingleFile;
        private string? _currentCascadeProjectFilePath;


        // --- Private Fields: UI State & Filters/Search ---
        private string? _currentFilePath;
        private bool _isDirty;
        private string _filterText = string.Empty;
        private bool _showOnlyInvalidNodes;
        private bool _showSchemaNodes = true;
        private string _searchText = string.Empty;
        private DataGridRowItemViewModel? _currentlyEditedItem;
        private DataGridRowItemViewModel? _selectedGridItem;
        private List<SearchResult> _searchResults = new();
        private int _currentSearchIndex = -1;

        // --- Private Fields: Undo/Redo ---
        private readonly Stack<EditOperation> _undoStack = new();
        private readonly Stack<EditOperation> _redoStack = new();

        // --- Private Fields: Search ---
        private System.Threading.Timer? _searchDebounceTimer;
        private const int SearchDebounceMilliseconds = 500;
        private readonly object _searchLock = new object();
        private HashSet<DomNode> _globallyMatchedDomNodes = new HashSet<DomNode>();
        private HashSet<string> _globallyMatchedSchemaNodePaths = new HashSet<string>();

        private static readonly JsonSerializerOptions _defaultSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = null,
        };

        // --- Commands ---
        public ICommand NewFileCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand OpenCascadeProjectCommand { get; }
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
        public ObservableCollection<DataGridRowItemViewModel> FlatItemsSource { get; } = new();
        private System.Threading.Timer? _filterDebounceTimer;

        public EditorMode CurrentEditorMode
        {
            get => _editorMode;
            private set => SetProperty(ref _editorMode, value);
        }

        public CascadeLayer? SelectedEditorLayer
        {
            get => _selectedEditorLayer;
            set
            {
                if (SetProperty(ref _selectedEditorLayer, value))
                {
                    SelectedEditorLayerIndex = _selectedEditorLayer != null ? CascadeLayers.IndexOf(_selectedEditorLayer) : -1;
                    // TODO: Phase II/III - Trigger RefreshFlatList based on new selection and IsMergedViewActive
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] SelectedEditorLayer changed to: {_selectedEditorLayer?.Name ?? "None"}");
                    OnPropertyChanged(nameof(WindowTitle)); // Title might depend on selected layer in some UI designs
                }
            }
        }

        public int SelectedEditorLayerIndex
        {
            get => _selectedEditorLayerIndex;
            private set => SetProperty(ref _selectedEditorLayerIndex, value); // Keep private if only driven by SelectedEditorLayer
        }

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
                var baseTitle = "JSON Configuration Editor";
                var fileName = string.IsNullOrEmpty(CurrentFilePath) ? "Untitled" : Path.GetFileName(CurrentFilePath);
                var dirtyIndicator = IsDirty ? "*" : "";
                var modeIndicator = CurrentEditorMode == EditorMode.CascadeProject ? "[Cascade Project]" : "";
                var selectedLayerIndicator = (CurrentEditorMode == EditorMode.CascadeProject && SelectedEditorLayer != null) ? $" - Layer: {SelectedEditorLayer.Name}" : "";
                return $"{baseTitle} - {fileName}{dirtyIndicator} {modeIndicator}{selectedLayerIndicator}".Trim();
            }
        }

        private abstract class EditOperation { /* ... existing ... */ }
        private sealed class ValueEditOperation : EditOperation { /* ... existing ... */ }
        private sealed class AddNodeOperation : EditOperation { /* ... existing ... */ }
        private sealed class RemoveNodeOperation : EditOperation { /* ... existing ... */ }
        private sealed class ReplaceRootOperation : EditOperation { /* ... existing ... */ }

        public MainViewModel()
        {
            _jsonParser = new JsonDomParser();
            _jsonSerializer = new DomNodeToJsonSerializer();
            _validationService = new ValidationService();
            _uiRegistry = new CustomUIRegistryService();
            _schemaLoader = new SchemaLoaderService(_uiRegistry);
            _cascadeProjectLoader = new CascadeProjectLoaderService();

            NewFileCommand = new RelayCommand(ExecuteNewFile);
            OpenFileCommand = new RelayCommand(ExecuteOpenFile);
            OpenCascadeProjectCommand = new RelayCommand(ExecuteOpenCascadeProject);
            SaveFileCommand = new RelayCommand(ExecuteSaveFile, CanExecuteSaveFile);
            SaveAsFileCommand = new RelayCommand(ExecuteSaveAsFile);
            ExitCommand = new RelayCommand(ExecuteExit);
            UndoCommand = new RelayCommand(ExecuteUndo, CanExecuteUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, CanExecuteRedo);
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

        public async Task InitializeWithStartupFileAsync(string filePath)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel.InitializeWithStartupFileAsync] Attempting to load: {filePath}");
            if (Path.GetFileName(filePath).Equals("cascade_project.jsonc", StringComparison.OrdinalIgnoreCase) ||
                filePath.EndsWith(".jsoncascade", StringComparison.OrdinalIgnoreCase))
            {
                await LoadCascadeProjectInternalAsync(filePath);
            }
            else if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                await LoadFileAsync(filePath);
            }
            else
            {
                MessageBox.Show($"Unsupported startup file type: {filePath}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                InitializeEmptyDocument();
            }
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
            if (!CheckUnsavedChanges()) return;
            var openDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", DefaultExt = "json", Title = "Open Single JSON File" };
            if (openDialog.ShowDialog() == true) { await LoadFileAsync(openDialog.FileName); }
        }

        private async void ExecuteOpenCascadeProject()
        {
            if (!CheckUnsavedChanges()) return;
            var openDialog = new OpenFileDialog { Filter = "Cascade Project files (*.jsoncascade;*.jsonc)|*.jsoncascade;*.jsonc|All files (*.*)|*.*", DefaultExt = "jsonc", Title = "Open Cascade Project" };
            if (openDialog.ShowDialog() == true) { await LoadCascadeProjectInternalAsync(openDialog.FileName); }
        }

        private async Task LoadCascadeProjectInternalAsync(string projectFilePath)
        {
            try
            {
                CurrentEditorMode = EditorMode.CascadeProject;
                _currentCascadeProjectFilePath = projectFilePath;
                CurrentFilePath = projectFilePath;
                CascadeLayers.Clear(); // Clear previous layers
                SelectedEditorLayer = null;

                var layerDefs = await _cascadeProjectLoader.LoadCascadeProjectAsync(projectFilePath);
                if (layerDefs == null || !layerDefs.Any())
                {
                    MessageBox.Show($"Cascade project '{projectFilePath}' loaded no layers or parsing failed. Check logs.", "Cascade Load Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    InitializeEmptyDocument();
                    CurrentEditorMode = EditorMode.SingleFile;
                    return;
                }

                foreach (var def in layerDefs)
                {
                    var newLayer = new CascadeLayer(def);
                    await LoadSourceFilesInLayer(newLayer); // Populate SourceFiles and placeholder LayerConfigRootNode
                    CascadeLayers.Add(newLayer);
                }

                System.Diagnostics.Debug.WriteLine($"[MainViewModel] Cascade project '{Path.GetFileName(projectFilePath)}' processed with {CascadeLayers.Count} layers.");

                if (CascadeLayers.Any())
                {
                    SelectedEditorLayer = CascadeLayers.FirstOrDefault(l => !l.IsReadOnly) ?? CascadeLayers.First();
                }

                // For Phase I, _rootDomNode will be a placeholder or based on SelectedEditorLayer's (unmerged) content.
                // Actual merging and display logic is for Phase II/III.
                if (SelectedEditorLayer != null)
                {
                     // Temporarily point _rootDomNode to the selected layer's (currently basic) LayerConfigRootNode
                    _rootDomNode = SelectedEditorLayer.LayerConfigRootNode;
                }
                else
                {
                    _rootDomNode = new ObjectNode("$empty_cascade_root", null); // Fallback if no selectable layers
                }

                IsDirty = false;
                ResetSearchState();
                RebuildDomToSchemaMapping();
                RefreshFlatList();
                await ValidateDocumentAsync();
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(CurrentEditorMode));
                OnPropertyChanged(nameof(CascadeLayers)); // Notify UI if bound
                OnPropertyChanged(nameof(SelectedEditorLayer));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load cascade project: {ex.Message}", "Cascade Project Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                InitializeEmptyDocument();
                CurrentEditorMode = EditorMode.SingleFile;
                OnPropertyChanged(nameof(CurrentEditorMode));
            }
        }

        private async Task LoadSourceFilesInLayer(CascadeLayer layer)
        {
            layer.SourceFiles.Clear(); // Ensure it's empty before loading
            if (!Directory.Exists(layer.ResolvedFolderPath))
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel.LoadSourceFilesInLayer] Folder not found for layer '{layer.Name}': {layer.ResolvedFolderPath}");
                layer.LayerConfigRootNode = new ObjectNode($"empty_{layer.Name}", null); // Ensure it has a root
                return;
            }

            var jsonFiles = Directory.GetFiles(layer.ResolvedFolderPath, "*.json", SearchOption.AllDirectories)
                                .OrderBy(f => f); // Simple ordering, Phase II will refine for intra-layer merge

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var fileContent = await File.ReadAllTextAsync(filePath);
                    var fileDomRoot = _jsonParser.ParseFromString(fileContent);
                    // Name of fileDomRoot might need adjustment based on file name if not an object to be merged.
                    // For now, assume parser handles root name adequately or it's merged based on structure.

                    var relativePath = Path.GetRelativePath(layer.ResolvedFolderPath, filePath).Replace("\\", "/");
                    layer.SourceFiles.Add(new SourceFileInfo(filePath, relativePath, fileDomRoot));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel.LoadSourceFilesInLayer] Failed to load or parse file '{filePath}' for layer '{layer.Name}': {ex.Message}");
                    // Optionally add a placeholder or error node to SourceFiles
                }
            }
            System.Diagnostics.Debug.WriteLine($"[MainViewModel.LoadSourceFilesInLayer] Layer '{layer.Name}' loaded {layer.SourceFiles.Count} source files.");

            // Placeholder for intra-layer merge (Phase II) - for now, LayerConfigRootNode is basic
            // In Phase II, this is where layer.LayerConfigRootNode would be properly built from merging layer.SourceFiles
            // and layer.IntraLayerValueOrigins would be populated.
            if (layer.SourceFiles.Any())
            {
                // Simple approach for Phase I: use the first file's content, or a named root if multiple.
                // This is NOT the final merge logic.
                layer.LayerConfigRootNode = new ObjectNode(layer.Name, null); // Placeholder root
                // A more representative placeholder might try to merge top-level keys of first few files
                // but full merge is Phase II.
            }
            else
            {
                layer.LayerConfigRootNode = new ObjectNode($"empty_{layer.Name}", null);
            }
        }


        private async void ExecuteSaveFile()
        {
            if (CurrentEditorMode == EditorMode.SingleFile)
            {
                try
                {
                    if (string.IsNullOrEmpty(CurrentFilePath) || _currentCascadeProjectFilePath != null)
                    {
                        ExecuteSaveAsFile();
                    }
                    else
                    {
                        await SaveFileAsync();
                    }
                }
                catch (Exception ex) { MessageBox.Show($"Failed to save file: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
            else
            {
                MessageBox.Show("Saving cascade projects (Phase IV feature) is not yet implemented.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private bool CanExecuteSaveFile()
        {
            return _rootDomNode != null && (CurrentEditorMode == EditorMode.SingleFile || IsDirty );
        }

        private async void ExecuteSaveAsFile()
        {
            var saveDialog = new SaveFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", DefaultExt = "json" };
            if (saveDialog.ShowDialog() == true)
            {
                if (CurrentEditorMode == EditorMode.SingleFile) { await SaveFileAsync(saveDialog.FileName); }
                else
                {
                    if (_rootDomNode != null)
                    {
                        try
                        {
                            await _jsonSerializer.SerializeToFileAsync(_rootDomNode, saveDialog.FileName);
                            MessageBox.Show($"Merged view saved to {saveDialog.FileName}", "Save Merged View As", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex) { MessageBox.Show($"Failed to save merged view: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error); }
                    }
                }
            }
        }

        private void ExecuteExit() { Application.Current.Shutdown(); }
        private void ExecuteUndo() { /* ... existing ... */ }
        private bool CanExecuteUndo() => _undoStack.Count > 0;
        private void ExecuteRedo() { /* ... existing ... */ }
        private bool CanExecuteRedo() => _redoStack.Count > 0;
        private void ExecuteFocusSearch() { }
        private void ExecuteFindNext() { /* ... existing ... */ }
        private void ExecuteFindPrevious() { /* ... existing ... */ }
        private bool CanExecuteFind() { return !string.IsNullOrEmpty(SearchText) && _searchResults.Any(); }
        private bool CanExecuteOpenModalEditor(DataGridRowItemViewModel? item) { return item?.ModalEditorInstance != null; }
        private void ExecuteOpenModalEditor(DataGridRowItemViewModel? parameter) { /* ... existing ... */ }
        private async void ExecuteLoadSchema() { /* ... existing ... */ }
        private void ExecuteClearFilter() { FilterText = string.Empty; }
        private void ExecuteClearSearch() { SearchText = string.Empty; }

        private bool CheckUnsavedChanges()
        {
            bool isCurrentlyDirty = IsDirty;
            if (!isCurrentlyDirty) return true;
            var result = MessageBox.Show("You have unsaved changes. Do you want to save before continuing?", "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            switch (result)
            {
                case MessageBoxResult.Yes: ExecuteSaveFile(); return !IsDirty;
                case MessageBoxResult.No: return true;
                case MessageBoxResult.Cancel: default: return false;
            }
        }

        public async Task LoadFileAsync(string filePath)
        {
            try
            {
                CurrentEditorMode = EditorMode.SingleFile;
                _currentCascadeProjectFilePath = null;
                CascadeLayers.Clear();
                SelectedEditorLayer = null;
                // OnPropertyChanged(nameof(CascadeLayerViewModels));

                _rootDomNode = await _jsonParser.ParseFromFileAsync(filePath);
                CurrentFilePath = filePath;
                IsDirty = false;
                ResetSearchState();
                RebuildDomToSchemaMapping();
                RefreshFlatList();
                await ValidateDocumentAsync();
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(CurrentEditorMode));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load file: {ex.Message}", "Open Error", MessageBoxButton.OK, MessageBoxImage.Error);
                InitializeEmptyDocument();
            }
        }

        public async Task SaveFileAsync(string? filePath = null)
        {
            if (CurrentEditorMode != EditorMode.SingleFile || _rootDomNode == null)
            {
                MessageBox.Show("Save operation is not applicable in the current context or with no data.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var targetPath = filePath ?? CurrentFilePath;
            if (string.IsNullOrEmpty(targetPath)) throw new InvalidOperationException("No file path specified for save operation");
            try
            {
                await _jsonSerializer.SerializeToFileAsync(_rootDomNode, targetPath);
                CurrentFilePath = targetPath;
                IsDirty = false;
                OnPropertyChanged(nameof(WindowTitle));
            }
            catch (Exception ex) { throw new InvalidOperationException($"Failed to save file: {ex.Message}", ex); }
        }

        public void NewDocument()
        {
            CurrentEditorMode = EditorMode.SingleFile;
            _currentCascadeProjectFilePath = null;
            CascadeLayers.Clear();
            SelectedEditorLayer = null;

            InitializeEmptyDocument();
            CurrentFilePath = null;
            IsDirty = false;
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(CurrentEditorMode));
        }

        public async Task LoadSchemasAsync(IEnumerable<string> assemblyPaths) { /* ... existing ... */ }
        internal void OnExpansionChanged(DataGridRowItemViewModel item) { RefreshFlatList(); }
        internal void SetCurrentlyEditedItem(DataGridRowItemViewModel? item) { _currentlyEditedItem = item; }
        internal void OnNodeValueChanged(DataGridRowItemViewModel item) { IsDirty = true; OnPropertyChanged(nameof(WindowTitle)); _ = ValidateDocumentAsync(); }
        internal bool IsRefPathResolvable(string referencePath) { /* ... existing ... */ return false;}
        internal bool AddArrayItem(ArrayNode parentArray, string editValue, SchemaNode? itemSchema) { /* ... existing ... */ return false; }
        internal bool MaterializeSchemaNodeAndBeginEdit(DataGridRowItemViewModel schemaOnlyVm, string initialEditValue) { /* ... existing ... */ return false; }
        private DomNode? MaterializeDomPathRecursive(string targetPathKey, SchemaNode? targetSchemaNodeContext) { /* ... existing ... */ return null; }
        private string NormalizeSchemaName(string schemaName) { return schemaName; }
        private DomNode? FindDomNodeByPath(string pathKey) { /* ... existing ... */ return null; }
        private DomNode? CreateNodeFromSchema(SchemaNode schema, string name, DomNode? parent) { /* ... existing ... */ return null; }
        private void RecordAddNodeOperation(DomNode? parent, DomNode newNode, string nameInParent) { /* ... existing ... */ }
        private JsonElement ConvertObjectToJsonElement(object? value) { /* ... existing ... */ return default; }

        private void InitializeEmptyDocument()
        {
            _rootDomNode = new ObjectNode("$root", null);
            _domToSchemaMap.Clear();
            _validationIssuesMap.Clear();
            _persistentVmMap.Clear();
            CascadeLayers.Clear();
            SelectedEditorLayer = null;
            CurrentFilePath = null;
            IsDirty = false;
            CurrentEditorMode = EditorMode.SingleFile;
            OnPropertyChanged(nameof(CurrentEditorMode));
            OnPropertyChanged(nameof(WindowTitle));
            RefreshFlatList();
        }

        private void RebuildDomToSchemaMapping()
        {
            _domToSchemaMap.Clear();
            if (_rootDomNode != null) { MapDomNodeToSchemaRecursive(_rootDomNode); }
        }

        private void MapDomNodeToSchemaRecursive(DomNode node)
        {
            var schema = _schemaLoader.FindSchemaForPath(node.Path);
            _domToSchemaMap[node] = schema;
            switch (node)
            {
                case ObjectNode on: foreach (var child in on.GetChildren()) MapDomNodeToSchemaRecursive(child); break;
                case ArrayNode an: foreach (var item in an.GetItems()) MapDomNodeToSchemaRecursive(item); break;
            }
        }

        private void RefreshFlatList()
        {
            if (_rootDomNode == null && CurrentEditorMode == EditorMode.SingleFile)
            { FlatItemsSource.Clear(); _persistentVmMap.Clear(); SelectedGridItem = null; return; }
            if (_rootDomNode == null && CurrentEditorMode == EditorMode.CascadeProject && !CascadeLayers.Any())
            { FlatItemsSource.Clear(); _persistentVmMap.Clear(); SelectedGridItem = null; return; }

            object? selectedIdentifier = null;
            bool wasSchemaOnlySelected = false;
            if (SelectedGridItem != null)
            {
                if (SelectedGridItem.IsDomNodePresent && SelectedGridItem.DomNode != null) { selectedIdentifier = SelectedGridItem.DomNode.Path; }
                else if (SelectedGridItem.IsSchemaOnlyNode && !string.IsNullOrEmpty(SelectedGridItem.SchemaNodePathKey)) { selectedIdentifier = SelectedGridItem.SchemaNodePathKey; wasSchemaOnlySelected = true; }
                else { selectedIdentifier = SelectedGridItem.NodeName; }
            }
            var tempFlatList = new List<DataGridRowItemViewModel>();
            var effectiveRootNode = _rootDomNode ?? new ObjectNode("$temp_empty_root", null); // Ensure not null for BuildFlatListRecursive

            if (!string.IsNullOrEmpty(FilterText))
            {
                var nodesToShow = GetFilteredNodeSet(FilterText); BuildFilteredFlatListRecursive(effectiveRootNode, tempFlatList, nodesToShow);
            }
            else
            {
                BuildFlatListRecursive(effectiveRootNode, tempFlatList);
                if (_showSchemaNodes && _schemaLoader?.RootSchemas != null)
                {
                    var primaryRootSchema = _schemaLoader.GetRootSchema();
                    foreach (var schemaEntry in _schemaLoader.RootSchemas)
                    {
                        string mountPath = schemaEntry.Key; SchemaNode schemaRoot = schemaEntry.Value;
                        if (schemaRoot == primaryRootSchema && string.IsNullOrEmpty(mountPath)) continue;
                        DomNode? existingDomForMountPath = FindDomNodeByPath(mountPath);
                        if (existingDomForMountPath != null) continue;
                        if (tempFlatList.Any(vm => vm.IsSchemaOnlyNode && vm.SchemaNodePathKey == mountPath && vm.Indentation.Left == 20)) continue;
                        int depth = 1; string propertyName = schemaRoot.Name;
                        var rootSchemaVm = new DataGridRowItemViewModel(schemaRoot, propertyName, this, depth, mountPath);
                        tempFlatList.Add(rootSchemaVm);
                        if (rootSchemaVm.IsExpanded && rootSchemaVm.IsExpandable) { AddSchemaOnlyChildrenRecursive(rootSchemaVm, tempFlatList, depth); }
                    }
                }
            }
            var processedList = ApplyFiltering(tempFlatList);
            FlatItemsSource.Clear();
            var newPersistentMap = new Dictionary<DomNode, DataGridRowItemViewModel>();
            DataGridRowItemViewModel? itemToReselect = null;
            foreach (var itemVm in processedList)
            {
                FlatItemsSource.Add(itemVm);
                if (itemVm.DomNode != null)
                {
                    newPersistentMap[itemVm.DomNode] = itemVm;
                    if (!wasSchemaOnlySelected && selectedIdentifier is string selectedDomPath && itemVm.DomNode.Path == selectedDomPath) { itemToReselect = itemVm; }
                }
                else if (itemVm.IsSchemaOnlyNode && !string.IsNullOrEmpty(itemVm.SchemaNodePathKey))
                {
                    if (wasSchemaOnlySelected && selectedIdentifier is string selectedSchemaPath && itemVm.SchemaNodePathKey == selectedSchemaPath) { itemToReselect = itemVm; }
                }
                else if (selectedIdentifier is string selectedName && itemVm.NodeName == selectedName) { itemToReselect = itemVm; }
            }
            _persistentVmMap.Clear();
            foreach (var entry in newPersistentMap) _persistentVmMap.Add(entry.Key, entry.Value);
            HighlightSearchResults();
            SelectedGridItem = itemToReselect;
        }

        private HashSet<DomNode> GetFilteredNodeSet(string filterText) { /* ... existing ... */ return new HashSet<DomNode>();}
        private void FindMatchingNodesRecursive(DomNode node, string lowerCaseFilter, List<DomNode> matchingNodes) { /* ... existing ... */ }
        private void BuildFilteredFlatListRecursive(DomNode node, List<DataGridRowItemViewModel> flatItems, HashSet<DomNode> nodesToShow) { /* ... existing ... */ }
        private void BuildFlatListRecursive(DomNode node, List<DataGridRowItemViewModel> flatItems) { /* ... existing ... */ }
        private List<DataGridRowItemViewModel> ApplyFiltering(List<DataGridRowItemViewModel> items) { /* ... existing ... */ return items; }
        private void DebouncedSearchAction(object? state) { /* ... existing ... */ }
        private void ResetSearchState() { /* ... existing ... */ }
        private void ExecuteSearchLogicAndRefreshUI() { /* ... existing ... */ }
        private void BuildAndApplyGlobalSearchMatches(string searchText) { /* ... existing ... */ }
        private void SearchDomNodeRecursive(DomNode node, string lowerSearchText) { /* ... existing ... */ }
        private void SearchSchemaNodeRecursive(SchemaNode schemaNode, string lowerSearchText, string currentPathKey) { /* ... existing ... */ }
        private void UpdateNavigableSearchResults() { /* ... existing ... */ }
        public bool IsDomNodeGloballyMatched(DomNode node) => _globallyMatchedDomNodes.Contains(node);
        public bool IsSchemaPathGloballyMatched(string schemaPathKey) => !string.IsNullOrEmpty(schemaPathKey) && _globallyMatchedSchemaNodePaths.Contains(schemaPathKey);
        private string _searchStatusText = string.Empty;
        public string SearchStatusText { get => _searchStatusText; private set => SetProperty(ref _searchStatusText, value); }
        private void HighlightSearchResults() { /* ... existing ... */ }
        private async Task ValidateDocumentAsync() { /* ... existing ... */ }
        private DomNode CreateNodeFromValue(string value, string name, DomNode parent, SchemaNode? schema) { /* ... existing ... */ return null!; }
        private class SearchResult { /* ... existing ... */ }
        private void RecordEditOperation(EditOperation op) { /* ... existing ... */ }
        private void SetNodeValue(DomNode node, System.Text.Json.JsonElement value) { /* ... existing ... */ }
        private void AddNode(DomNode parent, DomNode newNode, string name) { /* ... existing ... */ }
        private void RemoveNode(DomNode nodeToRemove) { /* ... existing ... */ }
        internal void RecordValueEdit(ValueNode node, JsonElement oldValue, JsonElement newValue) { /* ... existing ... */ }
        private void AddSchemaOnlyChildrenRecursive(DataGridRowItemViewModel parentVm, List<DataGridRowItemViewModel> flatItems, int parentDepth) { /* ... existing ... */ }
        private void EnsurePathIsExpandedInFlatItemsSource(string path) { /* ... existing ... */ }
        private void ClearMappingsRecursive(DomNode node) { /* ... existing ... */ }
        private void ExecuteDeleteSelectedNodes(DataGridRowItemViewModel? item) { /* ... existing ... */ }
        private bool CanExecuteDeleteSelectedNodes(DataGridRowItemViewModel? item) { /* ... existing ... */ return false; }
        private void ExecuteExpandSelectedRecursive(DataGridRowItemViewModel? item) { /* ... existing ... */ }
        private void ExecuteCollapseSelectedRecursive(DataGridRowItemViewModel? item) { /* ... existing ... */ }
        private bool CanExecuteExpandCollapseSelectedRecursive(DataGridRowItemViewModel? item) { /* ... existing ... */ return false; }
        private void SetExpansionRecursive(DataGridRowItemViewModel vm, bool expand) { /* ... existing ... */ }
        public bool? GetSchemaNodeExpansionState(string pathKey) { /* ... existing ... */ return null; }
        public void SetSchemaNodeExpansionState(string pathKey, bool isExpanded) { /* ... existing ... */ }
    }
}

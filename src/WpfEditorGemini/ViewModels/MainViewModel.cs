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

        // --- Private Fields: Core Data State ---
        private DomNode? _rootDomNode;
        private readonly Dictionary<DomNode, SchemaNode?> _domToSchemaMap = new();
        private readonly Dictionary<DomNode, List<ValidationIssue>> _validationIssuesMap = new();
        private readonly Dictionary<DomNode, DataGridRowItemViewModel> _persistentVmMap = new();
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
        private List<SearchResult> _searchResults = new(); // For F3 navigation (visible/filtered items)
        private int _currentSearchIndex = -1;

        // --- Private Fields: Undo/Redo ---
        private readonly Stack<EditOperation> _undoStack = new();
        private readonly Stack<EditOperation> _redoStack = new();

        // --- Private Fields: Search ---
        private System.Threading.Timer? _searchDebounceTimer;
        private const int SearchDebounceMilliseconds = 500;
        private readonly object _searchLock = new object(); // For thread safety with timer
        private HashSet<DomNode> _globallyMatchedDomNodes = new HashSet<DomNode>();
        private HashSet<string> _globallyMatchedSchemaNodePaths = new HashSet<string>();

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
        public ICommand DeleteSelectedNodesCommand { get; }
        public ICommand ExpandSelectedRecursiveCommand { get; }
        public ICommand CollapseSelectedRecursiveCommand { get; }

        // --- Public Properties ---

        public CustomUIRegistryService UiRegistry => _uiRegistry;

        public ObservableCollection<DataGridRowItemViewModel> FlatItemsSource { get; } = new();

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    RefreshFlatList();
                    OnPropertyChanged(nameof(CanClearFilter));
                }
            }
        }

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

        private abstract class EditOperation
        {
            public abstract DomNode? TargetNode { get; }
            public abstract void Undo(MainViewModel vm);
            public abstract void Redo(MainViewModel vm);
        }

        private sealed class ValueEditOperation : EditOperation
        {
            private readonly DomNode _node;
            private readonly System.Text.Json.JsonElement _oldValue;
            private readonly System.Text.Json.JsonElement _newValue;

            public override DomNode? TargetNode => _node;

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

            public override DomNode? TargetNode => _newNode;

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
            private readonly string _nameOrIndexAtTimeOfRemoval;
            private readonly int _originalIndexInArray;

            public override DomNode? TargetNode => _originalIndexInArray != -1 ? _removedNode : _parent;

            public RemoveNodeOperation(DomNode parent, DomNode removedNode, string nameOrIndexAtTimeOfRemoval, int originalIndexInArray)
            {
                _parent = parent;
                _removedNode = removedNode;
                _nameOrIndexAtTimeOfRemoval = nameOrIndexAtTimeOfRemoval;
                _originalIndexInArray = originalIndexInArray;
            }

            public override void Undo(MainViewModel vm)
            {
                if (_parent is ArrayNode arrayParent)
                {
                    arrayParent.InsertItem(_originalIndexInArray, _removedNode);
                }
                else if (_parent is ObjectNode objectParent)
                {
                    objectParent.AddChild(_removedNode.Name, _removedNode);
                }
                vm.MapDomNodeToSchemaRecursive(_removedNode);
                vm.IsDirty = true;
                vm.RefreshFlatList();
            }

            public override void Redo(MainViewModel vm)
            {
                vm.RemoveNode(_removedNode);
            }
        }

        private sealed class ReplaceRootOperation : EditOperation
        {
            private readonly DomNode? _oldRoot;
            private DomNode _newRoot;

            public override DomNode? TargetNode => _newRoot;

            public ReplaceRootOperation(DomNode? oldRoot, DomNode newRoot)
            {
                _oldRoot = oldRoot;
                _newRoot = newRoot;
            }

            public override void Undo(MainViewModel vm)
            {
                vm._rootDomNode = _oldRoot;
                vm.IsDirty = true;
                vm.RebuildDomToSchemaMapping();
                vm.RefreshFlatList();
                if (vm._rootDomNode != null && vm._persistentVmMap.TryGetValue(vm._rootDomNode, out var oldRootVm))
                {
                    vm.SelectedGridItem = oldRootVm;
                }
                else
                {
                    vm.SelectedGridItem = vm.FlatItemsSource.FirstOrDefault();
                }
            }

            public override void Redo(MainViewModel vm)
            {
                vm._rootDomNode = _newRoot;
                vm.IsDirty = true;
                vm.RebuildDomToSchemaMapping();
                vm.RefreshFlatList();
                if (vm._persistentVmMap.TryGetValue(vm._rootDomNode, out var newRootVm))
                {
                    vm.SelectedGridItem = newRootVm;
                }
                else
                {
                    vm.SelectedGridItem = vm.FlatItemsSource.FirstOrDefault();
                }
            }
        }

        public MainViewModel()
        {
            _jsonParser = new JsonDomParser();
            _jsonSerializer = new DomNodeToJsonSerializer();
            _validationService = new ValidationService();
            _uiRegistry = new CustomUIRegistryService();
            _schemaLoader = new SchemaLoaderService(_uiRegistry);

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
            OpenModalEditorCommand = new RelayCommand(param => ExecuteOpenModalEditor(param as DataGridRowItemViewModel), param => CanExecuteOpenModalEditor(param as DataGridRowItemViewModel));
            ClearFilterCommand = new RelayCommand(ExecuteClearFilter, () => CanClearFilter);
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
            DomNode? targetNodeForSelection = op.TargetNode;
            op.Undo(this);
            _redoStack.Push(op);
            RefreshFlatList();
            if (targetNodeForSelection != null && _persistentVmMap.TryGetValue(targetNodeForSelection, out var vmToSelect))
            {
                SelectedGridItem = vmToSelect;
            }
            OnPropertyChanged(nameof(WindowTitle));
        }

        private bool CanExecuteUndo() => _undoStack.Count > 0;

        private void ExecuteRedo()
        {
            if (_redoStack.Count == 0) return;
            var op = _redoStack.Pop();
            DomNode? targetNodeForSelection = op.TargetNode;
            op.Redo(this);
            _undoStack.Push(op);
            RefreshFlatList();
            if (targetNodeForSelection != null && _persistentVmMap.TryGetValue(targetNodeForSelection, out var vmToSelect))
            {
                SelectedGridItem = vmToSelect;
            }
            OnPropertyChanged(nameof(WindowTitle));
        }

        private bool CanExecuteRedo() => _redoStack.Count > 0;

        private void ExecuteFocusSearch() { }

        private void ExecuteFindNext()
        {
            if (string.IsNullOrEmpty(SearchText)) return;
            if (!_searchResults.Any() && !_globallyMatchedSchemaNodePaths.Any() && !_globallyMatchedDomNodes.Any())
            {
                return;
            }

            string? pathOfItemToNavigateTo = null;
            if (_searchResults.Any())
            {
                _currentSearchIndex = (_currentSearchIndex + 1) % _searchResults.Count;
                pathOfItemToNavigateTo = _searchResults[_currentSearchIndex].Path;
            }
            else
            {
                var allGlobalPaths = _globallyMatchedDomNodes.Select(n => n.Path)
                                       .Concat(_globallyMatchedSchemaNodePaths)
                                       .Distinct()
                                       .OrderBy(p => p)
                                       .ToList();
                if (allGlobalPaths.Any())
                {
                    pathOfItemToNavigateTo = allGlobalPaths.First();
                    _currentSearchIndex = -1;
                }
                else
                {
                    return;
                }
            }

            if (pathOfItemToNavigateTo != null)
            {
                EnsurePathIsExpandedInFlatItemsSource(pathOfItemToNavigateTo);
            }

            UpdateNavigableSearchResults();

            if (!_searchResults.Any())
            {
                SelectedGridItem = null;
                return;
            }

            int newFoundIndex = -1;
            for (int i = 0; i < _searchResults.Count; i++)
            {
                if (_searchResults[i].Path == pathOfItemToNavigateTo)
                {
                    newFoundIndex = i;
                    break;
                }
            }

            if (newFoundIndex != -1)
            {
                _currentSearchIndex = newFoundIndex;
                SelectedGridItem = _searchResults[_currentSearchIndex].Item;
            }
            else
            {
                _currentSearchIndex = 0;
                SelectedGridItem = _searchResults[_currentSearchIndex].Item;
            }
            if (SelectedGridItem != null && SelectedGridItem.IsExpandable && !SelectedGridItem.IsExpanded)
            {
                SelectedGridItem.IsExpanded = true;
            }
        }

        private void ExecuteFindPrevious()
        {
            if (string.IsNullOrEmpty(SearchText)) return;
            if (!_searchResults.Any() && !_globallyMatchedSchemaNodePaths.Any() && !_globallyMatchedDomNodes.Any())
            {
                return;
            }

            string? pathOfItemToNavigateTo = null;
            if (_searchResults.Any())
            {
                _currentSearchIndex = (_currentSearchIndex - 1 + _searchResults.Count) % _searchResults.Count;
                pathOfItemToNavigateTo = _searchResults[_currentSearchIndex].Path;
            }
            else
            {
                var allGlobalPaths = _globallyMatchedDomNodes.Select(n => n.Path)
                                       .Concat(_globallyMatchedSchemaNodePaths)
                                       .Distinct()
                                       .OrderByDescending(p => p)
                                       .ToList();
                if (allGlobalPaths.Any())
                {
                    pathOfItemToNavigateTo = allGlobalPaths.First();
                    _currentSearchIndex = -1;
                }
                else
                {
                    return;
                }
            }

            if (pathOfItemToNavigateTo != null)
            {
                EnsurePathIsExpandedInFlatItemsSource(pathOfItemToNavigateTo);
            }

            UpdateNavigableSearchResults();

            if (!_searchResults.Any())
            {
                SelectedGridItem = null;
                return;
            }

            int newFoundIndex = -1;
            for (int i = 0; i < _searchResults.Count; i++)
            {
                if (_searchResults[i].Path == pathOfItemToNavigateTo)
                {
                    newFoundIndex = i;
                    break;
                }
            }

            if (newFoundIndex != -1)
            {
                _currentSearchIndex = newFoundIndex;
                SelectedGridItem = _searchResults[_currentSearchIndex].Item;
            }
            else
            {
                _currentSearchIndex = _searchResults.Count - 1;
                SelectedGridItem = _searchResults[_currentSearchIndex].Item;
            }
            if (SelectedGridItem != null && SelectedGridItem.IsExpandable && !SelectedGridItem.IsExpanded)
            {
                SelectedGridItem.IsExpanded = true;
            }
        }

        private bool CanExecuteFind()
        {
            return !string.IsNullOrEmpty(SearchText) && _searchResults.Any();
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
            try
            {
                _rootDomNode = await _jsonParser.ParseFromFileAsync(filePath);
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
                var newNode = CreateNodeFromValue(editValue, parentArray.Items.Count.ToString(), parentArray, itemSchema);
                RecordEditOperation(new AddNodeOperation(parentArray, newNode, newNode.Name));
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

            DomNode? materializedDomNode = MaterializeDomPathRecursive(targetPathKey, targetSchemaNode);

            if (materializedDomNode == null)
            {
                return false;
            }

            if (materializedDomNode is ValueNode valueNode)
            {
                try
                {
                    JsonElement newJsonValue;
                    SchemaNode? vnSchema = _domToSchemaMap.TryGetValue(valueNode, out var s) ? s : null;
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
                        var valueEditOp = new ValueEditOperation(valueNode, oldValue, newJsonValue.Clone());
                        RecordEditOperation(valueEditOp);
                        valueEditOp.Redo(this);
                    }
                }
                catch (JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to parse initialEditValue '{initialEditValue}' for materialized node '{valueNode.Path}': {ex.Message}");
                }
            }

            RefreshFlatList();

            if (_persistentVmMap.TryGetValue(materializedDomNode, out var newVm))
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

        private DomNode? MaterializeDomPathRecursive(string targetPathKey, SchemaNode? targetSchemaNodeContext)
        {
            if (string.IsNullOrEmpty(targetPathKey) || targetPathKey == "$root")
            {
                if (_rootDomNode != null) return _rootDomNode;

                if (targetSchemaNodeContext == null)
                {
                    return null;
                }

                var oldRoot = _rootDomNode;
                var newRoot = CreateNodeFromSchema(targetSchemaNodeContext, "$root", null);
                if (newRoot != null)
                {
                    var op = new ReplaceRootOperation(oldRoot, newRoot);
                    RecordEditOperation(op);
                    op.Redo(this);
                    return _rootDomNode;
                }
                return null;
            }

            DomNode? existingNode = FindDomNodeByPath(targetPathKey);
            if (existingNode != null)
            {
                return existingNode;
            }

            string parentPathKey;
            string currentNodeName;

            string normalizedTargetPathKey = targetPathKey.StartsWith("$root/") ? targetPathKey.Substring("$root/".Length) : targetPathKey;

            int lastSlash = normalizedTargetPathKey.LastIndexOf('/');
            if (lastSlash == -1)
            {
                parentPathKey = "";
                currentNodeName = normalizedTargetPathKey;
            }
            else
            {
                parentPathKey = normalizedTargetPathKey.Substring(0, lastSlash);
                currentNodeName = normalizedTargetPathKey.Substring(lastSlash + 1);
            }

            SchemaNode? parentSchema = _schemaLoader.FindSchemaForPath(parentPathKey);

            DomNode? parentDomNode = MaterializeDomPathRecursive(parentPathKey, parentSchema);

            if (parentDomNode == null)
            {
                return null;
            }

            if (!(parentDomNode is ObjectNode parentAsObject))
            {
                return null;
            }

            SchemaNode? currentNodeSchema = null;
            if (parentSchema?.Properties != null && parentSchema.Properties.TryGetValue(currentNodeName, out SchemaNode? foundSchemaFromParent))
            {
                currentNodeSchema = foundSchemaFromParent;
            }
            if (targetSchemaNodeContext != null && NormalizeSchemaName(targetSchemaNodeContext.Name) == NormalizeSchemaName(currentNodeName))
            {
                currentNodeSchema = targetSchemaNodeContext;
            }
            if (currentNodeSchema == null)
            {
                currentNodeSchema = _schemaLoader.FindSchemaForPath(targetPathKey);
            }

            if (currentNodeSchema == null)
            {
                return null;
            }

            var newNode = CreateNodeFromSchema(currentNodeSchema, currentNodeName, parentDomNode);
            if (newNode == null)
            {
                return null;
            }

            var addOperation = new AddNodeOperation(parentAsObject, newNode, currentNodeName);
            RecordEditOperation(addOperation);
            addOperation.Redo(this);

            return newNode;
        }

        private string NormalizeSchemaName(string schemaName)
        {
            return schemaName;
        }

        private DomNode? FindDomNodeByPath(string pathKey)
        {
            if (_rootDomNode == null) return null;

            if (string.IsNullOrEmpty(pathKey) || pathKey == "$root") return _rootDomNode;

            string normalizedPathKey = pathKey.StartsWith("$root/") ? pathKey.Substring("$root/".Length) : pathKey;
            if (string.IsNullOrEmpty(normalizedPathKey)) return _rootDomNode;


            string[] segments = normalizedPathKey.Split('/');
            DomNode? current = _rootDomNode;

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

        private DomNode? CreateNodeFromSchema(SchemaNode schema, string name, DomNode? parent)
        {
            if (schema == null) return null;

            JsonElement defaultValue = ConvertObjectToJsonElement(schema.DefaultValue);

            switch (schema.NodeType)
            {
                case SchemaNodeType.Object:
                    var objNode = new ObjectNode(name, parent);
                    return objNode;
                case SchemaNodeType.Array:
                    var arrNode = new ArrayNode(name, parent);
                    return arrNode;
                case SchemaNodeType.Value:
                    return new ValueNode(name, parent, defaultValue);
                default:
                    return null;
            }
        }

        private void RecordAddNodeOperation(DomNode? parent, DomNode newNode, string nameInParent)
        {
            RecordEditOperation(new AddNodeOperation(parent ?? _rootDomNode!, newNode, nameInParent));
        }

        private JsonElement ConvertObjectToJsonElement(object? value)
        {
            if (value == null)
            {
                return JsonDocument.Parse("null").RootElement;
            }
            if (value is JsonElement element)
            {
                return element.Clone();
            }
            try
            {
                return JsonSerializer.SerializeToElement(value, _defaultSerializerOptions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to serialize object of type {value.GetType()} to JsonElement: {ex.Message}");
                return JsonDocument.Parse("null").RootElement;
            }
        }

        private void InitializeEmptyDocument()
        {
            _rootDomNode = new ObjectNode("$root", null);
            _domToSchemaMap.Clear();
            _validationIssuesMap.Clear();
            _persistentVmMap.Clear();
            RefreshFlatList();
        }

        private void RebuildDomToSchemaMapping()
        {
            _domToSchemaMap.Clear();
            if (_rootDomNode != null)
            {
                MapDomNodeToSchemaRecursive(_rootDomNode);
            }
        }

        private void MapDomNodeToSchemaRecursive(DomNode node)
        {
            var schema = _schemaLoader.FindSchemaForPath(node.Path);
            _domToSchemaMap[node] = schema;

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
            if (_rootDomNode == null)
            {
                FlatItemsSource.Clear();
                _persistentVmMap.Clear();
                SelectedGridItem = null;
                return;
            }

            object? selectedIdentifier = null;
            bool wasSchemaOnlySelected = false;
            if (SelectedGridItem != null)
            {
                if (SelectedGridItem.IsDomNodePresent && SelectedGridItem.DomNode != null)
                {
                    selectedIdentifier = SelectedGridItem.DomNode.Path;
                }
                else if (SelectedGridItem.IsSchemaOnlyNode && !string.IsNullOrEmpty(SelectedGridItem.SchemaNodePathKey))
                {
                    selectedIdentifier = SelectedGridItem.SchemaNodePathKey;
                    wasSchemaOnlySelected = true;
                }
                else
                {
                    selectedIdentifier = SelectedGridItem.NodeName;
                }
            }

            var tempFlatList = new List<DataGridRowItemViewModel>();

            if (!string.IsNullOrEmpty(FilterText))
            {
                var nodesToShow = GetFilteredNodeSet(FilterText);
                BuildFilteredFlatListRecursive(_rootDomNode, tempFlatList, nodesToShow);
            }
            else
            {
                BuildFlatListRecursive(_rootDomNode, tempFlatList);
                if (_showSchemaNodes && _schemaLoader?.RootSchemas != null)
                {
                    var primaryRootSchema = _schemaLoader.GetRootSchema();

                    foreach (var schemaEntry in _schemaLoader.RootSchemas)
                    {
                        string mountPath = schemaEntry.Key;
                        SchemaNode schemaRoot = schemaEntry.Value;

                        if (schemaRoot == primaryRootSchema && string.IsNullOrEmpty(mountPath))
                        {
                            continue;
                        }

                        DomNode? existingDomForMountPath = FindDomNodeByPath(mountPath);
                        if (existingDomForMountPath != null)
                        {
                            continue;
                        }

                        if (tempFlatList.Any(vm => vm.IsSchemaOnlyNode && vm.SchemaNodePathKey == mountPath && vm.Indentation.Left == 20))
                        {
                            continue;
                        }

                        int depth = 1;
                        string propertyName = schemaRoot.Name;

                        var rootSchemaVm = new DataGridRowItemViewModel(schemaRoot, propertyName, this, depth, mountPath);
                        tempFlatList.Add(rootSchemaVm);
                        if (rootSchemaVm.IsExpanded && rootSchemaVm.IsExpandable)
                        {
                            AddSchemaOnlyChildrenRecursive(rootSchemaVm, tempFlatList, depth);
                        }
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
                    if (!wasSchemaOnlySelected && selectedIdentifier is string selectedDomPath && itemVm.DomNode.Path == selectedDomPath)
                    {
                        itemToReselect = itemVm;
                    }
                }
                else if (itemVm.IsSchemaOnlyNode && !string.IsNullOrEmpty(itemVm.SchemaNodePathKey))
                {
                    if (wasSchemaOnlySelected && selectedIdentifier is string selectedSchemaPath && itemVm.SchemaNodePathKey == selectedSchemaPath)
                    {
                        itemToReselect = itemVm;
                    }
                }
                else if (selectedIdentifier is string selectedName && itemVm.NodeName == selectedName)
                {
                    itemToReselect = itemVm;
                }
            }

            _persistentVmMap.Clear();
            foreach (var entry in newPersistentMap) _persistentVmMap.Add(entry.Key, entry.Value);

            HighlightSearchResults();

            SelectedGridItem = itemToReselect;
        }

        private HashSet<DomNode> GetFilteredNodeSet(string filterText)
        {
            var directlyMatchingNodes = new List<DomNode>();
            if (_rootDomNode != null)
            {
                FindMatchingNodesRecursive(_rootDomNode, filterText.ToLowerInvariant(), directlyMatchingNodes);
            }

            var nodesToShow = new HashSet<DomNode>();
            foreach (var node in directlyMatchingNodes)
            {
                var current = node;
                while (current != null)
                {
                    nodesToShow.Add(current);
                    current = current.Parent;
                }
            }
            return nodesToShow;
        }

        private void FindMatchingNodesRecursive(DomNode node, string lowerCaseFilter, List<DomNode> matchingNodes)
        {
            bool isMatch = false;
            if (node.Name.ToLowerInvariant().Contains(lowerCaseFilter))
            {
                isMatch = true;
            }
            else if (node is ValueNode valueNode && valueNode.Value.ToString().ToLowerInvariant().Contains(lowerCaseFilter))
            {
                isMatch = true;
            }

            if (isMatch)
            {
                matchingNodes.Add(node);
            }

            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    FindMatchingNodesRecursive(child, lowerCaseFilter, matchingNodes);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    FindMatchingNodesRecursive(item, lowerCaseFilter, matchingNodes);
                }
            }
        }

        private void BuildFilteredFlatListRecursive(DomNode node, List<DataGridRowItemViewModel> flatItems, HashSet<DomNode> nodesToShow)
        {
            if (!nodesToShow.Contains(node))
            {
                return;
            }

            if (!_persistentVmMap.TryGetValue(node, out var viewModel))
            {
                _domToSchemaMap.TryGetValue(node, out var schema);
                viewModel = new DataGridRowItemViewModel(node, schema, this);
            }

            viewModel.SetExpansionStateInternal(true);

            flatItems.Add(viewModel);

            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren().OrderBy(c => c.Name))
                {
                    BuildFilteredFlatListRecursive(child, flatItems, nodesToShow);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    BuildFilteredFlatListRecursive(item, flatItems, nodesToShow);
                }
            }
        }

        private void BuildFlatListRecursive(DomNode node, List<DataGridRowItemViewModel> flatItems)
        {
            if (!_persistentVmMap.TryGetValue(node, out var viewModel))
            {
                _domToSchemaMap.TryGetValue(node, out var schema);
                viewModel = new DataGridRowItemViewModel(node, schema, this);
            }

            flatItems.Add(viewModel);

            if (viewModel.IsExpanded)
            {
                if (viewModel.IsDomNodePresent && node != null)
                {
                    switch (node)
                    {
                        case ObjectNode objectNode:
                            foreach (var childDomNode in objectNode.GetChildren())
                            {
                                BuildFlatListRecursive(childDomNode, flatItems);
                            }
                            if (_showSchemaNodes && _domToSchemaMap.TryGetValue(objectNode, out var objectSchema) && objectSchema?.Properties != null)
                            {
                                foreach (var schemaProp in objectSchema.Properties)
                                {
                                    if (!objectNode.HasProperty(schemaProp.Key))
                                    {
                                        var childDepth = objectNode.Depth + 1;
                                        var schemaPathKey = (string.IsNullOrEmpty(objectNode.Path) ? schemaProp.Key : $"{objectNode.Path}/{schemaProp.Key}").TrimStart('$');
                                        var schemaOnlyChildVm = new DataGridRowItemViewModel(
                                            schemaPropertyNode: schemaProp.Value,
                                            propertyName: schemaProp.Key,
                                            parentViewModel: this,
                                            depth: childDepth,
                                            pathKey: schemaPathKey
                                        );
                                        flatItems.Add(schemaOnlyChildVm);

                                        if (schemaOnlyChildVm.IsExpanded && schemaOnlyChildVm.IsExpandable)
                                        {
                                            AddSchemaOnlyChildrenRecursive(schemaOnlyChildVm, flatItems, childDepth);
                                        }
                                    }
                                }
                            }
                            break;

                        case ArrayNode arrayNode:
                            foreach (var itemDomNode in arrayNode.GetItems())
                            {
                                BuildFlatListRecursive(itemDomNode, flatItems);
                            }
                            if (_domToSchemaMap.TryGetValue(arrayNode, out var arraySchema))
                            {
                                var placeholderVm = new DataGridRowItemViewModel(arrayNode, arraySchema?.ItemSchema, this, arrayNode.Depth + 1);
                                flatItems.Add(placeholderVm);
                            }
                            break;
                    }
                }
                else if (viewModel.IsSchemaOnlyNode && viewModel.SchemaContextNode != null)
                {
                    SchemaNode schemaContext = viewModel.SchemaContextNode;
                    if (schemaContext.Properties != null && schemaContext.NodeType == SchemaNodeType.Object)
                    {
                        foreach (var propEntry in schemaContext.Properties)
                        {
                            var childDepth = (int)(viewModel.Indentation.Left / 20) + 1;
                            var childSchemaPathKey = $"{viewModel.SchemaNodePathKey}/{propEntry.Key}";
                            var childSchemaVm = new DataGridRowItemViewModel(propEntry.Value, propEntry.Key, this, childDepth, childSchemaPathKey);
                            flatItems.Add(childSchemaVm);
                            if (childSchemaVm.IsExpanded && childSchemaVm.IsExpandable)
                            {
                                AddSchemaOnlyChildrenRecursive(childSchemaVm, flatItems, childDepth);
                            }
                        }
                    }
                }
            }

            if (node == _rootDomNode && _showSchemaNodes)
            {
                var rootSchema = _schemaLoader.GetRootSchema();
                if (rootSchema?.Properties != null && node is ObjectNode rootObjectNode)
                {
                    foreach (var schemaProp in rootSchema.Properties)
                    {
                        if (!rootObjectNode.HasProperty(schemaProp.Key))
                        {
                            var schemaPathKey = schemaProp.Key.TrimStart('$');
                            var schemaOnlyVm = new DataGridRowItemViewModel(schemaProp.Value, schemaProp.Key, this, 1, schemaPathKey);

                            if (!flatItems.Any(vm => vm.NodeName == schemaProp.Key && vm.SchemaContextNode == schemaProp.Value && vm.Indentation.Left == (1 * 20) && vm.IsSchemaOnlyNode))
                            {
                                flatItems.Add(schemaOnlyVm);
                                if (schemaOnlyVm.IsExpanded && schemaOnlyVm.IsExpandable)
                                {
                                    AddSchemaOnlyChildrenRecursive(schemaOnlyVm, flatItems, 1);
                                }
                            }
                        }
                    }
                }
            }
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
                    if (string.IsNullOrEmpty(SearchText))
                    {
                        ResetSearchState();
                    }
                    else
                    {
                        ExecuteSearchLogicAndRefreshUI();
                    }
                });
            }
        }

        private void ResetSearchState()
        {
            _globallyMatchedDomNodes.Clear();
            _globallyMatchedSchemaNodePaths.Clear();
            _searchResults.Clear();
            _currentSearchIndex = -1;

            foreach (var vm in _persistentVmMap.Values) { vm.ClearHighlight(); }
            foreach (var vm in FlatItemsSource) { vm.ClearHighlight(); }

            RefreshFlatList();
        }

        private void ExecuteSearchLogicAndRefreshUI()
        {
            var vmsToNotify = FlatItemsSource.ToList();

            BuildAndApplyGlobalSearchMatches(SearchText);

            foreach (var vm in vmsToNotify)
            {
                vm.ReEvaluateHighlightStatus();
            }

            var allGlobalPaths = _globallyMatchedDomNodes.Select(n => n.Path)
                                   .Concat(_globallyMatchedSchemaNodePaths)
                                   .Distinct()
                                   .OrderBy(p => p)
                                   .ToList();

            if (allGlobalPaths.Any())
            {
                string firstPotentialPath = allGlobalPaths.First();
                EnsurePathIsExpandedInFlatItemsSource(firstPotentialPath);
            }

            UpdateNavigableSearchResults();
        }

        private void BuildAndApplyGlobalSearchMatches(string searchText)
        {
            _globallyMatchedDomNodes.Clear();
            _globallyMatchedSchemaNodePaths.Clear();

            if (string.IsNullOrEmpty(searchText)) return;

            if (_rootDomNode != null)
            {
                SearchDomNodeRecursive(_rootDomNode, searchText.ToLowerInvariant());
            }

            if (ShowSchemaNodes)
            {
                var rootSchema = _schemaLoader.GetRootSchema();
                if (rootSchema != null)
                {
                    SearchSchemaNodeRecursive(rootSchema, searchText.ToLowerInvariant(), "");
                }
            }
        }

        private void SearchDomNodeRecursive(DomNode node, string lowerSearchText)
        {
            if (node.Name.ToLowerInvariant().Contains(lowerSearchText))
            {
                _globallyMatchedDomNodes.Add(node);
            }
            if (node is ValueNode valueNode)
            {
                if (valueNode.Value.ToString().ToLowerInvariant().Contains(lowerSearchText))
                {
                    _globallyMatchedDomNodes.Add(node);
                }
            }

            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    SearchDomNodeRecursive(child, lowerSearchText);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    SearchDomNodeRecursive(item, lowerSearchText);
                }
            }
        }

        private void SearchSchemaNodeRecursive(SchemaNode schemaNode, string lowerSearchText, string currentPathKey)
        {
            if (schemaNode.Name.ToLowerInvariant().Contains(lowerSearchText))
            {
                _globallyMatchedSchemaNodePaths.Add(currentPathKey);
            }

            if (schemaNode.NodeType == SchemaNodeType.Object && schemaNode.Properties != null)
            {
                foreach (var prop in schemaNode.Properties)
                {
                    var childPathKey = string.IsNullOrEmpty(currentPathKey) ? prop.Key : $"{currentPathKey}/{prop.Key}";
                    SearchSchemaNodeRecursive(prop.Value, lowerSearchText, childPathKey);
                }
            }
        }

        private void UpdateNavigableSearchResults()
        {
            _searchResults.Clear();

            foreach (var vm in FlatItemsSource)
            {
                bool isMatch = false;
                if (vm.DomNode != null && _globallyMatchedDomNodes.Contains(vm.DomNode))
                {
                    isMatch = true;
                }
                else if (vm.IsSchemaOnlyNode && !string.IsNullOrEmpty(vm.SchemaNodePathKey) && _globallyMatchedSchemaNodePaths.Contains(vm.SchemaNodePathKey))
                {
                    isMatch = true;
                }

                if (isMatch)
                {
                    string path = vm.DomNode?.Path ?? vm.SchemaNodePathKey ?? vm.NodeName;
                    _searchResults.Add(new SearchResult(vm, path, vm.IsSchemaOnlyNode));
                }
            }

            if (!string.IsNullOrEmpty(SearchText))
            {
                if (_searchResults.Any())
                {
                    SearchStatusText = $"{_searchResults.Count} matches found";
                    if ((_currentSearchIndex == -1 || _currentSearchIndex >= _searchResults.Count) && _searchResults.Any())
                    {
                        _currentSearchIndex = 0;
                    }
                }
                else
                {
                    SearchStatusText = "No matches found";
                }
            }
            else
            {
                SearchStatusText = string.Empty;
            }
        }

        public bool IsDomNodeGloballyMatched(DomNode node) => _globallyMatchedDomNodes.Contains(node);
        public bool IsSchemaPathGloballyMatched(string schemaPathKey) => !string.IsNullOrEmpty(schemaPathKey) && _globallyMatchedSchemaNodePaths.Contains(schemaPathKey);

        private string _searchStatusText = string.Empty;
        public string SearchStatusText
        {
            get => _searchStatusText;
            private set => SetProperty(ref _searchStatusText, value);
        }

        private void HighlightSearchResults()
        {
        }

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

        private DomNode CreateNodeFromValue(string value, string name, DomNode parent, SchemaNode? schema)
        {
            try
            {
                var jsonDoc = System.Text.Json.JsonDocument.Parse(value);
                return _jsonParser.ParseFromJsonElement(jsonDoc.RootElement, name, parent);
            }
            catch
            {
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

        private void RecordEditOperation(EditOperation op)
        {
            _undoStack.Push(op);
            _redoStack.Clear();
            IsDirty = true;
            OnPropertyChanged(nameof(WindowTitle));
        }

        private void SetNodeValue(DomNode node, System.Text.Json.JsonElement value)
        {
            if (node is ValueNode valueNode)
            {
                valueNode.Value = value;
                if (_persistentVmMap.TryGetValue(node, out var vm))
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
        }

        private void AddNode(DomNode parent, DomNode newNode, string name)
        {
            if (parent is ObjectNode objectParent)
            {
                objectParent.AddChild(name, newNode);
            }
            else if (parent is ArrayNode arrayParent)
            {
                arrayParent.InsertItem(arrayParent.Items.Count, newNode);
            }
            else
            {
                return;
            }
        }

        private void RemoveNode(DomNode nodeToRemove)
        {
            if (nodeToRemove == null) return;

            var parent = nodeToRemove.Parent;

            ClearMappingsRecursive(nodeToRemove);

            if (parent is ObjectNode objectParent)
            {
                objectParent.RemoveChild(nodeToRemove.Name);
            }
            else if (parent is ArrayNode arrayParent)
            {
                arrayParent.RemoveItem(nodeToRemove);
            }
            else if (nodeToRemove == _rootDomNode)
            {
                if (_rootDomNode == nodeToRemove) _rootDomNode = null;
            }

            IsDirty = true;
            RefreshFlatList();
        }

        internal void RecordValueEdit(ValueNode node, JsonElement oldValue, JsonElement newValue)
        {
            RecordEditOperation(new ValueEditOperation(node, oldValue, newValue));

            IsDirty = true;
            OnPropertyChanged(nameof(WindowTitle));
        }

        private void AddSchemaOnlyChildrenRecursive(DataGridRowItemViewModel parentVm, List<DataGridRowItemViewModel> flatItems, int parentDepth)
        {
            if (parentVm.SchemaContextNode?.Properties != null)
            {
                foreach (var propEntry in parentVm.SchemaContextNode.Properties)
                {
                    var childDepth = parentDepth + 1;
                    var childSchemaPathKey = $"{parentVm.SchemaNodePathKey}/{propEntry.Key}";
                    var childSchemaVm = new DataGridRowItemViewModel(propEntry.Value, propEntry.Key, this, childDepth, childSchemaPathKey);
                    flatItems.Add(childSchemaVm);
                    if (childSchemaVm.IsExpanded && childSchemaVm.IsExpandable)
                    {
                        AddSchemaOnlyChildrenRecursive(childSchemaVm, flatItems, childDepth);
                    }
                }
            }
        }

        private void EnsurePathIsExpandedInFlatItemsSource(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            var segments = path.Split('/');
            DomNode? currentNode = _rootDomNode;
            DataGridRowItemViewModel? currentVm = null;

            if (_rootDomNode != null && _persistentVmMap.TryGetValue(_rootDomNode, out var rootVm))
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

                if (currentNode != null && _persistentVmMap.TryGetValue(currentNode, out var childVm))
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
            _domToSchemaMap.Remove(node);
            _validationIssuesMap.Remove(node);
            _persistentVmMap.Remove(node);

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
                    RecordEditOperation(new RemoveNodeOperation(parent, nodeToRemove, nodeToRemove.Name, originalIndex));
                    RemoveNode(nodeToRemove);
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
                    if (_persistentVmMap.TryGetValue(childNode, out var childVm))
                    {
                        SetExpansionRecursive(childVm, expand);
                    }
                }
            }
            else if (vm.DomNode is ArrayNode arrayNode)
            {
                foreach (var childNode in arrayNode.GetItems())
                {
                    if (_persistentVmMap.TryGetValue(childNode, out var childVm))
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
    }
}

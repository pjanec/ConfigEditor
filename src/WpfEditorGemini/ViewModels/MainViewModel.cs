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
        public ICommand DeleteSelectedNodesCommand { get; }
        public ICommand ExpandSelectedRecursiveCommand { get; }
        public ICommand CollapseSelectedRecursiveCommand { get; }

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
                    // Debounce the search
                    _searchDebounceTimer?.Dispose(); // Dispose previous timer
                    _searchDebounceTimer = new System.Threading.Timer(DebouncedSearchAction, null, SearchDebounceMilliseconds, System.Threading.Timeout.Infinite);
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
        /// Gets or sets the currently selected item in the DataGrid.
        /// Used for TwoWay binding to preserve selection across list refreshes.
        /// </summary>
        public DataGridRowItemViewModel? SelectedGridItem
        {
            get => _selectedGridItem;
            set => SetProperty(ref _selectedGridItem, value);
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
            public abstract DomNode? TargetNode { get; } // Node primarily affected by the operation
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

            public override DomNode? TargetNode => _newNode; // The node that was added

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
                _removedNode = removedNode; // This node already has its Name and Parent (which is _parent) set from its construction
                _nameOrIndexAtTimeOfRemoval = nameOrIndexAtTimeOfRemoval; // This is _removedNode.Name
                _originalIndexInArray = originalIndexInArray;
            }

            public override void Undo(MainViewModel vm)
            {
                // _removedNode was constructed with _parent as its parent, and _nameOrIndexAtTimeOfRemoval as its Name.
                // These are immutable on _removedNode.
                // We are just re-attaching it to the _parent's collection.
                if (_parent is ArrayNode arrayParent)
                {
                    // Assumes _removedNode.Name is its stringified original index, suitable for re-insertion if needed
                    // but InsertItem takes the node itself.
                    // The critical part is that _removedNode already has its Parent link to arrayParent.
                    arrayParent.InsertItem(_originalIndexInArray, _removedNode); 
                }
                else if (_parent is ObjectNode objectParent)
                {
                    // _removedNode.Name is the propertyName.
                    // The critical part is that _removedNode already has its Parent link to objectParent.
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

        // New operation for root replacement
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

        // --- Constructor ---

        public MainViewModel()
        {
            // Initialize services
            _jsonParser = new JsonDomParser();
            _jsonSerializer = new DomNodeToJsonSerializer();
            _validationService = new ValidationService();
            _uiRegistry = new CustomUIRegistryService();
            _schemaLoader = new SchemaLoaderService(_uiRegistry);
            // _uiRegistry.DiscoverAndRegister(Assembly.GetExecutingAssembly()); // Linter error: Method not found. Commenting out.

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
            OpenModalEditorCommand = new RelayCommand(param => ExecuteOpenModalEditor(param as DataGridRowItemViewModel), param => CanExecuteOpenModalEditor(param as DataGridRowItemViewModel));
            DeleteSelectedNodesCommand = new RelayCommand(param => ExecuteDeleteSelectedNodes(param as DataGridRowItemViewModel), param => CanExecuteDeleteSelectedNodes(param as DataGridRowItemViewModel));
            ExpandSelectedRecursiveCommand = new RelayCommand(param => ExecuteExpandSelectedRecursive(param as DataGridRowItemViewModel), param => CanExecuteExpandCollapseSelectedRecursive(param as DataGridRowItemViewModel)); 
            CollapseSelectedRecursiveCommand = new RelayCommand(param => ExecuteCollapseSelectedRecursive(param as DataGridRowItemViewModel), param => CanExecuteExpandCollapseSelectedRecursive(param as DataGridRowItemViewModel));

            // Initialize with empty document
            InitializeEmptyDocument();
        }

        // --- Command Implementations ---

        private void ExecuteNewFile()
        {
            if (CheckUnsavedChanges())
            {
                NewDocument(); // This will call ResetSearchState
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
                    await LoadFileAsync(openDialog.FileName); // This will call ResetSearchState
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
            RefreshFlatList(); // This will try to preserve current SelectedGridItem or use its logic.
            // Explicitly try to reselect the item related to the operation if possible
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
            // Explicitly try to reselect the item related to the operation if possible
            if (targetNodeForSelection != null && _persistentVmMap.TryGetValue(targetNodeForSelection, out var vmToSelect))
            {
                 SelectedGridItem = vmToSelect;
            }
            OnPropertyChanged(nameof(WindowTitle));
        }

        private bool CanExecuteRedo() => _redoStack.Count > 0;

        private void ExecuteFocusSearch()
        {
            // TODO: Focus the search textbox
        }

        private void ExecuteFindNext()
        {
            if (!_searchResults.Any()) return; // Use _searchResults (navigable items)
            _currentSearchIndex = (_currentSearchIndex + 1) % _searchResults.Count;
            
            var targetVm = _searchResults[_currentSearchIndex].Item;
            SelectedGridItem = targetVm; 
            ExpandAncestors(targetVm);   
            // TODO: Notify View to scroll SelectedGridItem into view.
        }

        private void ExecuteFindPrevious()
        {
            if (!_searchResults.Any()) return;  // Use _searchResults (navigable items)
            _currentSearchIndex = (_currentSearchIndex - 1 + _searchResults.Count) % _searchResults.Count;
            
            var targetVm = _searchResults[_currentSearchIndex].Item;
            SelectedGridItem = targetVm; 
            ExpandAncestors(targetVm);  
            // TODO: Notify View to scroll SelectedGridItem into view.
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
            object viewModelForEditor = vm; // The DataGridRowItemViewModel itself is passed as context

            // --- Placeholder for Modal Dialog Logic ---
            // 1. Create the editor control: FrameworkElement? editorControl = editor.CreateEditControl(viewModelForEditor);
            //    If editorControl is null, perhaps show an error or do nothing.
            // 2. Create a new Window (or a custom modal dialog UserControl).
            // 3. Set the editorControl as the content of this new Window/UserControl.
            // 4. Configure the Window (Title, SizeToContent, ShowInTaskbar=false, Owner, etc.).
            // 5. Show the Window as a dialog: window.ShowDialog();
            // 6. Handle the result of ShowDialog(). If true (e.g., user clicked OK):
            //    - The custom editor control itself should have updated vm.EditValue.
            //    - Call vm.CommitEdit() to attempt to save the value to the DOM.
            //    - vm.IsInEditMode = false; // Exit edit mode for the row
            // If false (e.g., user clicked Cancel or closed the dialog):
            //    - vm.CancelEdit(); // Or simply revert vm.EditValue if the editor didn't change it until commit.
            //    - vm.IsInEditMode = false;

            MessageBox.Show($"Attempting to open modal editor for: {vm.NodeName}\nEditor Type: {editor.GetType().Name}\nThis is a placeholder. Actual modal dialog logic needs to be implemented here.", 
                            "Open Modal Editor", MessageBoxButton.OK, MessageBoxImage.Information);
            
            // Example of how the editor might be used (actual dialog logic is more complex)
            // var editorControl = editor.CreateEditControl(viewModelForEditor);
            // if (editorControl != null) {
            //     // Code to host editorControl in a new modal window...
            //     // bool? dialogResult = new ModalWindow(editorControl).ShowDialog();
            //     // if (dialogResult == true) {
            //     //     vm.CommitEdit();
            //     // }
            //     // vm.IsInEditMode = false;
            // }
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
                
                ResetSearchState(); // Clear search state
                RebuildDomToSchemaMapping();
                RefreshFlatList();
                await ValidateDocumentAsync();
                OnPropertyChanged(nameof(WindowTitle)); // Ensure title updates if it depends on CurrentFilePath
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
            InitializeEmptyDocument(); // Calls ResetSearchState
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
            // If it's a schema-only node, its IsExpanded setter would have already updated _schemaNodeExpansionState.
            // For DOM nodes, their expansion is implicitly persisted by being in _persistentVmMap if RefreshFlatList reuses them.
            // The primary job here is to rebuild the list display based on current expansion states.
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
                System.Diagnostics.Debug.WriteLine($"MaterializeAndBeginEdit: MaterializeDomPathRecursive failed for '{targetPathKey}'.");
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
                        valueEditOp.Redo(this); // This calls SetNodeValue which updates valueNode.Value
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
            // 0. Handle empty path for root (_rootDomNode is the target)
            if (string.IsNullOrEmpty(targetPathKey) || targetPathKey == "$root") // Normalized root path check
            {
                if (_rootDomNode != null) return _rootDomNode; // Root already exists

                if (targetSchemaNodeContext == null) {
                    System.Diagnostics.Debug.WriteLine($"MaterializeDomPathRecursive: Cannot materialize root, targetSchemaNodeContext is null.");
                    return null; 
                }
                
                var oldRoot = _rootDomNode; // Will be null here if we are materializing for the first time
                var newRoot = CreateNodeFromSchema(targetSchemaNodeContext, "$root", null);
                if (newRoot != null)
                {
                    var op = new ReplaceRootOperation(oldRoot, newRoot); // Handles setting _rootDomNode via Redo
                    RecordEditOperation(op); 
                    op.Redo(this); 
                    return _rootDomNode; // Return the newly set root from vm instance member
                }
                System.Diagnostics.Debug.WriteLine($"MaterializeDomPathRecursive: Failed to create root node from schema.");
                return null;
            }

            // 1. Try to find if the node for targetPathKey already exists in DOM.
            DomNode? existingNode = FindDomNodeByPath(targetPathKey);
            if (existingNode != null)
            {
                return existingNode; 
            }

            // 2. If doesn't exist, ensure its parent exists, then create this node.
            string parentPathKey;
            string currentNodeName;

            // Normalize targetPathKey before splitting (though FindDomNodeByPath also normalizes)
            string normalizedTargetPathKey = targetPathKey.StartsWith("$root/") ? targetPathKey.Substring("$root/".Length) : targetPathKey;

            int lastSlash = normalizedTargetPathKey.LastIndexOf('/');
            if (lastSlash == -1) // Direct child of root
            {
                parentPathKey = ""; // Root path for MaterializeDomPathRecursive (normalized)
                currentNodeName = normalizedTargetPathKey;
            }
            else
            {
                parentPathKey = normalizedTargetPathKey.Substring(0, lastSlash);
                currentNodeName = normalizedTargetPathKey.Substring(lastSlash + 1);
            }

            // 3. Ensure parent DOM node exists.
            SchemaNode? parentSchema = _schemaLoader.FindSchemaForPath(parentPathKey); 

            DomNode? parentDomNode = MaterializeDomPathRecursive(parentPathKey, parentSchema);

            if (parentDomNode == null)
            {
                System.Diagnostics.Debug.WriteLine($"MaterializeDomPathRecursive: Failed to materialize parent DOM node for path '{parentPathKey}' while trying for '{targetPathKey}'.");
                return null;
            }

            if (!(parentDomNode is ObjectNode parentAsObject))
            {
                System.Diagnostics.Debug.WriteLine($"MaterializeDomPathRecursive: Parent DOM node '{parentDomNode.Path}' is not an ObjectNode. Cannot add property '{currentNodeName}'.");
                return null;
            }
            
            SchemaNode? currentNodeSchema = null;
            // Try to get specific schema from parent if targetSchemaNodeContext was for a deeper path initially
            if (parentSchema?.Properties != null && parentSchema.Properties.TryGetValue(currentNodeName, out SchemaNode? foundSchemaFromParent))
            {
                currentNodeSchema = foundSchemaFromParent;
            }
            // If not found via parent, or if targetSchemaNodeContext is more specific (e.g. passed directly for the current segment)
            // and its name matches, prefer it. This helps if targetSchemaNodeContext was indeed for *this* segment.
            if (targetSchemaNodeContext != null && NormalizeSchemaName(targetSchemaNodeContext.Name) == NormalizeSchemaName(currentNodeName)) {
                 currentNodeSchema = targetSchemaNodeContext;
            }
            // Fallback to direct path lookup if still null
            if (currentNodeSchema == null) {
                currentNodeSchema = _schemaLoader.FindSchemaForPath(targetPathKey); // targetPathKey is the full path to current node
            }
            
            if (currentNodeSchema == null)
            {   
                System.Diagnostics.Debug.WriteLine($"MaterializeDomPathRecursive: Could not find schema for current node '{currentNodeName}' within path '{targetPathKey}'.");
                return null; 
            }

            // 5. Create the current node.
            var newNode = CreateNodeFromSchema(currentNodeSchema, currentNodeName, parentDomNode);
            if (newNode == null)
            {
                System.Diagnostics.Debug.WriteLine($"MaterializeDomPathRecursive: Failed to create new DOM node for '{currentNodeName}' from schema.");
                return null;
            }

            var addOperation = new AddNodeOperation(parentAsObject, newNode, currentNodeName);
            RecordEditOperation(addOperation);
            addOperation.Redo(this); 

            return newNode;
        }

        private string NormalizeSchemaName(string schemaName) {
            // Schemas might have names like "*" for array items, but property names are specific.
            // This helper is more for conceptual matching; property names from path splitting are usually exact.
            return schemaName; // Placeholder for now, might need more sophisticated normalization if schema names have prefixes/suffixes.
        }

        private DomNode? FindDomNodeByPath(string pathKey)
        {
            if (_rootDomNode == null) return null;
            
            // Root path can be "" or "$root" conceptually. DomNode.Path for root is "".
            // If schema gives "$root" but DOM path is "", adjust.
            if (string.IsNullOrEmpty(pathKey) || pathKey == "$root") return _rootDomNode;

            // Normalize: DomNode.Path doesn't start with "$root/"
            string normalizedPathKey = pathKey.StartsWith("$root/") ? pathKey.Substring("$root/".Length) : pathKey;
            if (string.IsNullOrEmpty(normalizedPathKey)) return _rootDomNode;


            string[] segments = normalizedPathKey.Split('/');
            DomNode? current = _rootDomNode;

            foreach (string segment in segments)
            {
                if (current == null) return null;

                if (current is ObjectNode objNode)
                {
                    current = objNode.GetChild(segment); // Using GetChild
                    if (current == null) return null; // Not found
                }
                else if (current is ArrayNode arrNode)
                {
                    if (int.TryParse(segment, out int index))
                    {
                        current = arrNode.GetItem(index); // Using GetItem
                        if (current == null) return null; // Not found or index out of bounds by GetItem logic
                    }
                    else return null; // Invalid segment for array
                }
                else return null; // Not a container
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
                    // TODO: Populate with default children if schema defines them (recursive call to CreateNodeFromSchema/Materialize)
                    return objNode;
                case SchemaNodeType.Array:
                    var arrNode = new ArrayNode(name, parent);
                    // TODO: Populate with default items if schema defines them
                    return arrNode;
                case SchemaNodeType.Value:
                    // For ValueNode, the JsonElement default value is part of its state.
                    return new ValueNode(name, parent, defaultValue);
                // case SchemaNodeType.Ref: // Assuming RefNode is created differently or not directly from here.
                // return new RefNode(name, parent, referencePath); 
                default:
                    return null; // Unknown schema node type
            }
        }

        // Helper to standardize AddNodeOperation recording, especially when node is created fresh.
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
            // Handle cases where 'value' might already be a JsonElement (e.g. from ValueNode.Value)
            if (value is JsonElement element)
            {
                return element.Clone(); // Clone to ensure it's not tied to another document
            }
            try
            {
                return JsonSerializer.SerializeToElement(value, _defaultSerializerOptions);
            }
            catch (Exception ex)
            {
                // Fallback or error handling if direct serialization fails
                System.Diagnostics.Debug.WriteLine($"Failed to serialize object of type {value.GetType()} to JsonElement: {ex.Message}");
                // Fallback to a JSON null or throw, depending on desired behavior
                return JsonDocument.Parse("null").RootElement;
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
            if (_rootDomNode == null)
            {
                FlatItemsSource.Clear();
                _persistentVmMap.Clear();
                SelectedGridItem = null; // Clear selection
                return;
            }

            // Preserve selected item's underlying DomNode or unique path key
            object? selectedIdentifier = null;
            bool wasSchemaOnlySelected = false;
            if(SelectedGridItem != null)
            {
                if (SelectedGridItem.IsDomNodePresent && SelectedGridItem.DomNode != null)
                {
                    selectedIdentifier = SelectedGridItem.DomNode.Path; // Use DOM path for DOM nodes
                }
                else if (SelectedGridItem.IsSchemaOnlyNode && !string.IsNullOrEmpty(SelectedGridItem.SchemaNodePathKey))
                {
                    selectedIdentifier = SelectedGridItem.SchemaNodePathKey; // Use SchemaNodePathKey for schema-only
                    wasSchemaOnlySelected = true;
                }
                else // Fallback for add item placeholders or other edge cases
                {
                    selectedIdentifier = SelectedGridItem.NodeName;
                }
                System.Diagnostics.Debug.WriteLine($"RefreshFlatList: Preserving selection. Identifier: '{selectedIdentifier}', WasSchemaOnly: {wasSchemaOnlySelected}");
            }

            var tempFlatList = new List<DataGridRowItemViewModel>();
            BuildFlatListRecursive(_rootDomNode, tempFlatList);

            // Augment with VMs for other schema roots if not represented by DOM or already added by BuildFlatListRecursive for the primary root
            if (_showSchemaNodes && _schemaLoader?.RootSchemas != null)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshFlatList: Checking for unrepresented schema roots. Total roots: {_schemaLoader.RootSchemas.Count}");
                var primaryRootSchema = _schemaLoader.GetRootSchema();

                foreach (var schemaEntry in _schemaLoader.RootSchemas)
                {
                    string mountPath = schemaEntry.Key;
                    SchemaNode schemaRoot = schemaEntry.Value;

                    // Skip the primary root schema if its items are handled by BuildFlatListRecursive via _rootDomNode
                    if (schemaRoot == primaryRootSchema && string.IsNullOrEmpty(mountPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"RefreshFlatList: Skipping primary schema root '{schemaRoot.Name}' at empty MountPath as it's handled by DOM recursion.");
                        continue;
                    }

                    // Check if a DOM node already exists at this mount path.
                    DomNode? existingDomForMountPath = FindDomNodeByPath(mountPath);
                    if (existingDomForMountPath != null)
                    {
                         System.Diagnostics.Debug.WriteLine($"RefreshFlatList: Skipping schema root '{schemaRoot.Name}' at MountPath='{mountPath}' because a DOM node exists there.");
                        continue;
                    }
                    
                    // Check if a VM for this specific schema root (identified by its mountPath) already exists at the top level (depth 1).
                    // Depth 1 corresponds to Indentation.Left == 20 (assuming IndentSize is 20).
                    if (tempFlatList.Any(vm => vm.IsSchemaOnlyNode && vm.SchemaNodePathKey == mountPath && vm.Indentation.Left == 20))
                    {
                        System.Diagnostics.Debug.WriteLine($"RefreshFlatList: Skipping schema root '{schemaRoot.Name}' at MountPath='{mountPath}' because a VM already exists in tempFlatList at depth 1.");
                        continue;
                    }

                    System.Diagnostics.Debug.WriteLine($"RefreshFlatList: Adding VM for unrepresented schema root '{schemaRoot.Name}' at MountPath='{mountPath}'.");
                    int depth = 1; // Display these other schema roots at depth 1
                    string propertyName = schemaRoot.Name; // Display name for this schema root entry
                    
                    var rootSchemaVm = new DataGridRowItemViewModel(schemaRoot, propertyName, this, depth, mountPath);
                    tempFlatList.Add(rootSchemaVm);
                    if (rootSchemaVm.IsExpanded && rootSchemaVm.IsExpandable)
                    {
                        System.Diagnostics.Debug.WriteLine($"RefreshFlatList: Expanded VM for schema root '{schemaRoot.Name}'. Adding its children.");
                        AddSchemaOnlyChildrenRecursive(rootSchemaVm, tempFlatList, depth);
                    }
                }
            }

            var processedList = ApplyFiltering(tempFlatList);

            FlatItemsSource.Clear();
            // _persistentVmMap.Clear(); // Clearing this aggressively can lose VM state like expansion if not careful
            // Let's try to update existing VMs in persistentMap or add new ones, and remove stale ones.
            var newPersistentMap = new Dictionary<DomNode, DataGridRowItemViewModel>();

            DataGridRowItemViewModel? itemToReselect = null;

            foreach (var itemVm in processedList) // itemVm is the new or updated VM from BuildFlatListRecursive
            {
                FlatItemsSource.Add(itemVm);
                if (itemVm.DomNode != null) // If it's a DOM node VM
                {
                    newPersistentMap[itemVm.DomNode] = itemVm; // Update/add to new persistent map
                    if (!wasSchemaOnlySelected && selectedIdentifier is string selectedDomPath && itemVm.DomNode.Path == selectedDomPath)
                    {
                        itemToReselect = itemVm;
                    }
                }
                else if (itemVm.IsSchemaOnlyNode && !string.IsNullOrEmpty(itemVm.SchemaNodePathKey)) // If it's a schema-only VM
                {
                    if (wasSchemaOnlySelected && selectedIdentifier is string selectedSchemaPath && itemVm.SchemaNodePathKey == selectedSchemaPath)
                    {
                        itemToReselect = itemVm;
                    }
                    // Schema-only VMs are not typically put in _persistentVmMap by DomNode key.
                    // Their state (like expansion) is managed by _schemaNodeExpansionState.
                }
                else if (selectedIdentifier is string selectedName && itemVm.NodeName == selectedName) // Fallback by name (e.g. for AddItem placeholder)
                {
                     itemToReselect = itemVm;
                }
            }

            // Update the main persistent map with the new one (removes stale, keeps/updates existing, adds new)
            _persistentVmMap.Clear(); // Clear old one first
            foreach(var entry in newPersistentMap) _persistentVmMap.Add(entry.Key, entry.Value);

            HighlightSearchResults();

            SelectedGridItem = itemToReselect; 
            if (itemToReselect != null)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshFlatList: Reselected item '{itemToReselect.NodeName}' (Path: {itemToReselect.DomNode?.Path ?? itemToReselect.SchemaNodePathKey}).");
            }
            else if (selectedIdentifier != null)
            {
                 System.Diagnostics.Debug.WriteLine($"RefreshFlatList: Could not reselect item with identifier '{selectedIdentifier}'. SelectedGridItem is null.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"RefreshFlatList: No item was previously selected or to reselect. SelectedGridItem is null.");
            }
            
            // Ensure the re-selected item is visible if focus is important
            if (SelectedGridItem != null)
            {
                // ParentViewModel.EnsureVisible(SelectedGridItem); // Conceptual - needs DataGrid access
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
                if (schema != null)
                {
                    // viewModel.ModalEditorInstance = _uiRegistry.GetEditor(schema); // Assuming _uiRegistry.GetEditor(SchemaNode) exists
                    // For now, let's assume DataGridRowItemViewModel's constructor handles this if schema is present,
                    // or it's set up by a more specific mechanism if custom editors are complex.
                    // This line is commented out as DataGridRowItemViewModel's constructor was updated to handle this.
                }
                System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive (DOM Node): CREATED NEW VM for node '{viewModel.NodeName}' (Path: {node.Path}, Hash: {viewModel.GetHashCode()}), IsSchemaOnly: False, Initial IsExpanded: {viewModel.IsExpanded}");
            }
            else
            {
                // If re-using a VM, ensure its schema context and editor are up-to-date,
                // though schema is unlikely to change for an existing DOM node without re-mapping.
                // viewModel.UpdateSchemaInfo(); // This updates display props, not ModalEditorInstance directly.
                // If ModalEditorInstance could change, it would need an update here too.
                System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive (DOM Node): REUSED VM for node '{viewModel.NodeName}' (Path: {node.Path}, Hash: {viewModel.GetHashCode()}), IsSchemaOnly: False, Current IsExpanded: {viewModel.IsExpanded}");
            }

            flatItems.Add(viewModel);

            System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive: Checking expansion for VM '{viewModel.NodeName}' (Hash: {viewModel.GetHashCode()}), IsDomNodePresent: {viewModel.IsDomNodePresent}, IsSchemaOnlyNode: {viewModel.IsSchemaOnlyNode}, IsExpanded property value: {viewModel.IsExpanded}");
            
            if (viewModel.IsExpanded)
            {
                System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive: VM '{viewModel.NodeName}' (Hash: {viewModel.GetHashCode()}) IS expanded. Processing children/properties.");
                if (viewModel.IsDomNodePresent && node != null) // Children of an existing DOM node
                {
                    switch (node)
                    {
                        case ObjectNode objectNode:
                            // First, add children that exist in the DOM
                            foreach (var childDomNode in objectNode.GetChildren())
                            {
                                BuildFlatListRecursive(childDomNode, flatItems);
                            }
                            // Second, add schema-only properties not present in DOM for this objectNode
                            if (_showSchemaNodes && _domToSchemaMap.TryGetValue(objectNode, out var objectSchema) && objectSchema?.Properties != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive (DOM Node '{objectNode.Path}'): Checking schema-only properties. Schema CLR Type: '{objectSchema.ClrType.FullName}', Name: '{objectSchema.Name}'.");
                                foreach (var schemaProp in objectSchema.Properties)
                                {
                                    if (!objectNode.HasProperty(schemaProp.Key))
                                    {
                                        var childDepth = objectNode.Depth + 1;
                                        var schemaPathKey = (string.IsNullOrEmpty(objectNode.Path) ? schemaProp.Key : $"{objectNode.Path}/{schemaProp.Key}").TrimStart('$');
                                        System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive (DOM Node '{objectNode.Path}'): Adding schema-only child VM for '{schemaProp.Key}' (PathKey: {schemaPathKey}, Type: {schemaProp.Value.ClrType.Name}). Depth: {childDepth}");
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
                                            System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive (DOM Node '{objectNode.Path}'): Schema-only child '{schemaOnlyChildVm.NodeName}' is expanded. Adding its children.");
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
                            if (_domToSchemaMap.TryGetValue(arrayNode, out var arraySchema)) // Add "Add item" placeholder
                            {
                                var placeholderVm = new DataGridRowItemViewModel(arrayNode, arraySchema?.ItemSchema, this, arrayNode.Depth + 1);
                                flatItems.Add(placeholderVm);
                            }
                            break;
                    }
                }
                else if (viewModel.IsSchemaOnlyNode && viewModel.SchemaContextNode != null) // Children of an expanded schema-only node
                {
                    SchemaNode schemaContext = viewModel.SchemaContextNode;
                    System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive (Schema-Only VM '{viewModel.NodeName}'): Processing expanded schema node. Schema CLR Type: '{schemaContext.ClrType.FullName}', Name: '{schemaContext.Name}'.");
                    if (schemaContext.Properties != null && schemaContext.NodeType == SchemaNodeType.Object)
                    {
                        foreach (var propEntry in schemaContext.Properties)
                        {
                            System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive (Schema-Only VM '{viewModel.NodeName}'): Adding child schema-only VM for '{propEntry.Key}' (Type: {propEntry.Value.ClrType.Name}). Depth: {viewModel.Indentation.Left / 20 + 1}");
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
                    else if (schemaContext.ItemSchema != null && schemaContext.NodeType == SchemaNodeType.Array)
                    {
                        // For schema-only arrays, we might show a placeholder like "(Define array items)" or nothing,
                        // as there's no "Add Item" equivalent without a parent DOM ArrayNode.
                        // For now, do nothing for children of expanded schema-only arrays.
                        System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive (Schema-Only VM '{viewModel.NodeName}'): Is an expanded schema-only array. Not adding item placeholders currently.");
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive: VM '{viewModel.NodeName}' (Hash: {viewModel.GetHashCode()}) is NOT expanded.");
            }

            // Special handling for root node to show schema-only top-level items if root is empty or they are not present
            // This should run *after* the root DomNode (if any) and its direct children are processed.
            // We only want to add top-level schema items if the root DomNode itself doesn't provide them.
            if (node == _rootDomNode && _showSchemaNodes)
            {
                var rootSchema = _schemaLoader.GetRootSchema(); 
                if (rootSchema?.Properties != null && node is ObjectNode rootObjectNode)
                {
                    System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive: Checking schema-only properties for ROOT node '{rootObjectNode.Path}'. Root Schema CLR Type: {rootSchema.ClrType.FullName}, Name: {rootSchema.Name}");
                    foreach (var schemaProp in rootSchema.Properties)
                    {
                        if (!rootObjectNode.HasProperty(schemaProp.Key))
                        {
                             System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive: Adding schema-only VM for '{schemaProp.Key}' (Type: {schemaProp.Value.ClrType.Name}) under ROOT.");
                            var schemaPathKey = schemaProp.Key.TrimStart('$');
                            var schemaOnlyVm = new DataGridRowItemViewModel(schemaProp.Value, schemaProp.Key, this, 1, schemaPathKey); 
                            
                            if (!flatItems.Any(vm => vm.NodeName == schemaProp.Key && vm.SchemaContextNode == schemaProp.Value && vm.Indentation.Left == (1 * 20) && vm.IsSchemaOnlyNode))
                            {
                                flatItems.Add(schemaOnlyVm);
                                // If the added top-level schema-only VM is expanded and expandable, add its children.
                                if (schemaOnlyVm.IsExpanded && schemaOnlyVm.IsExpandable)
                                {
                                    System.Diagnostics.Debug.WriteLine($"BuildFlatListRecursive (Root): Top-level schema-only VM '{schemaOnlyVm.NodeName}' is expanded. Adding its children.");
                                    AddSchemaOnlyChildrenRecursive(schemaOnlyVm, flatItems, 1); // Depth of children of root-level items is 1 + 1 = 2, but AddSchemaOnlyChildrenRecursive takes parentDepth.
                                }
                            }
                        }
                    }
                }
                else if (rootSchema == null)
                {
                    System.Diagnostics.Debug.WriteLine("BuildFlatListRecursive: Root schema is null, cannot add schema-only items for root.");
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

        // --- Search Logic ---

        private void DebouncedSearchAction(object? state)
        {
            lock (_searchLock) // Ensure only one search operation runs at a time
            {
                // Must dispatch to UI thread if updating UI-bound collections or properties
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (string.IsNullOrEmpty(SearchText))
                    {
                        ResetSearchState(); // This will clear highlights and refresh list
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
            // Don't set SearchText here to avoid re-triggering setter logic if called from SearchText setter itself.
            // If called from New/Load, SearchText should be reset separately if desired or managed by UI.
            // For now, focus on clearing internal state. If SearchText is "" in setter, this will be called.

            _globallyMatchedDomNodes.Clear();
            _globallyMatchedSchemaNodePaths.Clear();
            _searchResults.Clear();
            _currentSearchIndex = -1;

            // Clear highlights on existing VMs
            foreach (var vm in _persistentVmMap.Values) { vm.ClearHighlight(); }
            foreach (var vm in FlatItemsSource) { vm.ClearHighlight(); }
            
            System.Diagnostics.Debug.WriteLine("Search state reset.");
            RefreshFlatList(); // Refresh to update UI (e.g. remove highlights, update selection)
        }

        private void ExecuteSearchLogicAndRefreshUI()
        {
            System.Diagnostics.Debug.WriteLine($"Executing search for: '{SearchText}'");
            BuildAndApplyGlobalSearchMatches(SearchText);
            UpdateNavigableSearchResults(); // Rebuild _searchResults based on FlatItemsSource and global matches
            RefreshFlatList(); // This will cause VMs to re-evaluate IsHighlightedInSearch
        }

        private void BuildAndApplyGlobalSearchMatches(string searchText)
        {
            _globallyMatchedDomNodes.Clear();
            _globallyMatchedSchemaNodePaths.Clear();

            if (string.IsNullOrEmpty(searchText)) return;

            // 1. Search DOM Nodes
            if (_rootDomNode != null)
            {
                SearchDomNodeRecursive(_rootDomNode, searchText.ToLowerInvariant());
            }

            // 2. Search Schema Nodes (if shown)
            if (ShowSchemaNodes)
            {
                var rootSchema = _schemaLoader.GetRootSchema();
                if (rootSchema != null)
                {
                    SearchSchemaNodeRecursive(rootSchema, searchText.ToLowerInvariant(), "");
                }
            }
            System.Diagnostics.Debug.WriteLine($"Global search found {_globallyMatchedDomNodes.Count} DOM matches and {_globallyMatchedSchemaNodePaths.Count} schema path matches.");
        }

        private void SearchDomNodeRecursive(DomNode node, string lowerSearchText)
        {
            // Check node name (Path.GetFileName might be useful for last segment if Name is full path for some)
            if (node.Name.ToLowerInvariant().Contains(lowerSearchText))
            {
                _globallyMatchedDomNodes.Add(node);
            }
            // Check node value if it's a ValueNode
            if (node is ValueNode valueNode)
            {
                // Simple string check on the value. More sophisticated checks could be added (e.g. for numbers).
                if (valueNode.Value.ToString().ToLowerInvariant().Contains(lowerSearchText))
                {
                    _globallyMatchedDomNodes.Add(node);
                }
            }
            // TODO: Add search for RefNode reference paths if desired

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
            // Check schema node name (property name)
            if (schemaNode.Name.ToLowerInvariant().Contains(lowerSearchText))
            {
                 // currentPathKey should be the full path to this schemaNode
                _globallyMatchedSchemaNodePaths.Add(currentPathKey);
            }
            // Optionally, search in schema descriptions, titles, etc.
            // if (schemaNode.Description?.ToLowerInvariant().Contains(lowerSearchText) == true) { ... }

            if (schemaNode.NodeType == SchemaNodeType.Object && schemaNode.Properties != null)
            {
                foreach (var prop in schemaNode.Properties)
                {
                    var childPathKey = string.IsNullOrEmpty(currentPathKey) ? prop.Key : $"{currentPathKey}/{prop.Key}";
                    SearchSchemaNodeRecursive(prop.Value, lowerSearchText, childPathKey);
                }
            }
            else if (schemaNode.NodeType == SchemaNodeType.Array && schemaNode.ItemSchema != null)
            {
                // Schema arrays themselves don't have named children in the same way objects do.
                // The ItemSchema's properties would be searched if it's an object.
                // For now, we only match the array node itself by its name.
                // If ItemSchema is an object, and we want to search "prototype" properties:
                // SearchSchemaNodeRecursive(schemaNode.ItemSchema, lowerSearchText, $"{currentPathKey}/*"); // Or some other placeholder for item schema context
            }
        }
        
        private void UpdateNavigableSearchResults()
        {
            _searchResults.Clear();
            _currentSearchIndex = -1;

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
                    // Path for SearchResult could be vm.DomNode.Path or vm.SchemaNodePathKey
                    string path = vm.DomNode?.Path ?? vm.SchemaNodePathKey ?? vm.NodeName;
                    _searchResults.Add(new SearchResult(vm, path, vm.IsSchemaOnlyNode));
                }
            }
            System.Diagnostics.Debug.WriteLine($"Updated navigable search results: {_searchResults.Count} items.");

            if (_searchResults.Any())
            {
                _currentSearchIndex = 0;
                SelectedGridItem = _searchResults[_currentSearchIndex].Item;
                ExpandAncestors(SelectedGridItem);
            }
            else
            {
                // If no navigable results, what should selection be? Current behavior: stays as is or becomes null.
                // SelectedGridItem = null; // Optionally clear selection if no search results in current view
            }
        }

        // Public accessors for DataGridRowItemViewModel to check highlight status
        public bool IsDomNodeGloballyMatched(DomNode node) => _globallyMatchedDomNodes.Contains(node);
        public bool IsSchemaPathGloballyMatched(string schemaPathKey) => !string.IsNullOrEmpty(schemaPathKey) && _globallyMatchedSchemaNodePaths.Contains(schemaPathKey);

        // --- Old Search Methods (to be removed or ensured they are no longer called) ---
        private void BuildSearchIndex(string searchText, List<SearchResult> results)
        {
            // This method's direct responsibility for populating _searchResults for navigation
            // is now handled by UpdateNavigableSearchResults.
            // Its traversal logic is now in BuildAndApplyGlobalSearchMatches (via SearchDomNodeRecursive and SearchSchemaNodeRecursive).
            // This can be removed if not called elsewhere. For safety, leaving a stub.
             System.Diagnostics.Debug.WriteLine("BuildSearchIndex (old) called - should be deprecated.");
        }
        
        private void HighlightSearchResults() 
        {
            // This is now entirely superseded by IsHighlightedInSearch on DataGridRowItemViewModel
            // and the global match sets.
            System.Diagnostics.Debug.WriteLine("HighlightSearchResults (old) called - should be deprecated.");
        }

        private void ScrollToCurrentSearchResult()
        {
            // This is primarily a View concern. ViewModel can select the item.
            // If this method had any VM logic, it needs to be reviewed.
            // For now, it seems to just call ExpandAncestors, which is good.
            if (_currentSearchIndex >= 0 && _currentSearchIndex < _searchResults.Count)
            {
                var vm = _searchResults[_currentSearchIndex].Item;
                ExpandAncestors(vm); 
                // Actual scrolling must be done in View.
                 System.Diagnostics.Debug.WriteLine("ScrollToCurrentSearchResult (old) called. Expanded ancestors. Scrolling is View's job.");
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
                // The 'value' parameter is the one to apply (either old or new value from the operation)
                valueNode.Value = value; 
                // RecordEditOperation(new ValueEditOperation(node, oldValue, value)); // This was the bug
                
                // Notify that the node changed, which handles IsDirty, validation, and VM refresh via persistent map.
                if (_persistentVmMap.TryGetValue(node, out var vm))
                {
                    OnNodeValueChanged(vm); 
                }
                else
                {
                    // If VM not found (should not happen for existing nodes), still mark dirty and re-validate globally.
                    IsDirty = true;
                    OnPropertyChanged(nameof(WindowTitle));
                    _ = ValidateDocumentAsync();
                    // RefreshFlatList might be needed if a VM wasn't found, though this is an edge case.
                }
            }
        }

        private void AddNode(DomNode parent, DomNode newNode, string name)
        {
            // This method is called by AddNodeOperation.Redo.
            // newNode comes from AddNodeOperation, which means it was the node originally added.
            // At the time of its original creation (before AddNodeOperation was made), 
            // it MUST have been constructed with 'parent' as its Parent and 'name' as its Name.
            // So, newNode.Parent should already be 'parent' and newNode.Name should already be 'name'.

            if (parent is ObjectNode objectParent)
            {
                // newNode.Name should be 'name' (the property name)
                AddNodeToObject(objectParent, newNode);
            }
            else if (parent is ArrayNode arrayParent)
            {
                // newNode.Name should be the stringified index it was originally created for.
                // 'name' here is that original stringified index.
                // We need to know the *target index* for insertion.
                // If AddNodeOperation is for adding to end, index is parentArray.Items.Count.
                // If AddNodeOperation needs to support specific index, it needs to store it.
                // For now, assume AddNode by default adds to end of array for simplicity of this legacy AddNode method.
                // A more robust AddNodeOperation would store the index for arrays.
                // For now, assume AddNode by default adds to end of array for simplicity of this legacy AddNode method.
                // A more robust AddNodeOperation would store the index for arrays.
                AddNodeToArrayAtIndex(arrayParent, newNode, arrayParent.Items.Count);
            }
            else
            {
                return;
            }
        }

        // Specific method for adding to ObjectNode - REPLACED BY THE TWO-ARGUMENT VERSION
        // private void AddNodeToObject(ObjectNode parent, DomNode newNode, string name)
        // {
        //     if (parent.Children.ContainsKey(name))
        //     {
        //         return;
        //     }
        //     // newNode.Parent = parent; // WRONG - DomNode.Parent is readonly
        //     // newNode.Name = name; // WRONG - DomNode.Name is readonly
        //     // parent.Children[name] = newNode; // WRONG - IReadOnlyDictionary

        //     // Correct approach: newNode must be created with 'name' and 'parent'
        //     // Then, parent.AddChild(name, newNode)
        //     parent.AddChild(name, newNode); // Assumes newNode.Name is already 'name'

        //     MapDomNodeToSchemaRecursive(newNode); 
        //     IsDirty = true;
        //     RefreshFlatList(); 
        // }

        // Specific method for adding to ArrayNode at a specific index - REPLACED BY THE THREE-ARGUMENT VERSION
        // private void AddNodeToArrayAtIndex(ArrayNode parent, DomNode newNode, int index)
        // {
        //     // if (index < 0 || index > parent.Items.Count)
        //     // {
        //     //     return; 
        //     // }
        //     // newNode.Parent = parent; // WRONG
            
        //     // parent.Items.Insert(index, newNode); // WRONG - IReadOnlyList

        //     // Correct approach: newNode must be created with index.ToString() as name and 'parent' as parent
        //     // Then, parent.InsertItem(index, newNode)
        //     parent.InsertItem(index, newNode);


        //     MapDomNodeToSchemaRecursive(newNode); 
        //     IsDirty = true;
        //     RefreshFlatList(); 
        // }

        private void RemoveNode(DomNode nodeToRemove) // Renamed parameter for clarity
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

        // Method to be called by DataGridRowItemViewModel to record a value change for undo/redo
        internal void RecordValueEdit(ValueNode node, JsonElement oldValue, JsonElement newValue)
        {
            // Ensure distinct JsonElement instances for oldValue and newValue if they might point to the same underlying data
            // ValueNode.Value setter should handle cloning, and TryUpdateFromString creates new JsonElements.
            // So, direct use should be fine here.
            RecordEditOperation(new ValueEditOperation(node, oldValue, newValue));
            
            // Trigger general post-edit logic
            IsDirty = true;
            OnPropertyChanged(nameof(WindowTitle));
            _ = ValidateDocumentAsync(); // Re-validate after change

            // Refresh the specific item in the list if direct DOM manipulation doesn't auto-update UI sufficient
            if (_persistentVmMap.TryGetValue(node, out var vmToUpdate))
            {
                vmToUpdate.RefreshDisplayProperties(); // To update ValueDisplay etc.
            }
        }

        // --- Schema Node Expansion State Persistence ---
        internal bool? GetSchemaNodeExpansionState(string pathKey)
        {
            if (_schemaNodeExpansionState.TryGetValue(pathKey, out bool isExpanded))
            {
                return isExpanded;
            }
            return null; // Not found, use default
        }

        internal void SetSchemaNodeExpansionState(string pathKey, bool isExpanded)
        {    
            System.Diagnostics.Debug.WriteLine($"MainViewModel: Setting SchemaNodeExpansionState for '{pathKey}' to {isExpanded}");
            _schemaNodeExpansionState[pathKey] = isExpanded;
            // No RefreshFlatList here, as this is called from IsExpanded setter which then calls OnExpansionChanged.
        }

        // Helper method to recursively add children of an expanded schema-only VM
        private void AddSchemaOnlyChildrenRecursive(DataGridRowItemViewModel parentSchemaVm, List<DataGridRowItemViewModel> flatItems, int parentDepth)
        {
            if (!parentSchemaVm.IsSchemaOnlyNode || parentSchemaVm.SchemaContextNode == null || !parentSchemaVm.IsExpanded)
            {
                return;
            }

            SchemaNode schemaContext = parentSchemaVm.SchemaContextNode;
            System.Diagnostics.Debug.WriteLine($"AddSchemaOnlyChildrenRecursive: Processing for '{parentSchemaVm.NodeName}', Schema Type: {schemaContext.ClrType.FullName}");

            if (schemaContext.Properties != null && schemaContext.NodeType == SchemaNodeType.Object)
            {
                foreach (var propEntry in schemaContext.Properties)
                {
                    int childDepth = parentDepth + 1;
                    var childSchemaPathKey = $"{parentSchemaVm.SchemaNodePathKey}/{propEntry.Key}";
                    System.Diagnostics.Debug.WriteLine($"AddSchemaOnlyChildrenRecursive ('{parentSchemaVm.NodeName}'): Adding child schema-only VM for '{propEntry.Key}' (PathKey: {childSchemaPathKey}, Type: {propEntry.Value.ClrType.Name}). Depth: {childDepth}");
                    var childSchemaVm = new DataGridRowItemViewModel(propEntry.Value, propEntry.Key, this, childDepth, childSchemaPathKey);
                    flatItems.Add(childSchemaVm);
                    
                    if (childSchemaVm.IsExpanded && childSchemaVm.IsExpandable)
                    {
                        AddSchemaOnlyChildrenRecursive(childSchemaVm, flatItems, childDepth);
                    }
                }
            }
            // No "Add item" placeholder for pure schema-only arrays for now.
        }

        private bool CanExecuteDeleteSelectedNodes(DataGridRowItemViewModel? item = null) 
        {
            var selectedVm = item ?? SelectedGridItem; 
            System.Diagnostics.Debug.WriteLine($"CanExecuteDeleteSelectedNodes called. Item param: {item?.NodeName}, SelectedGridItem: {SelectedGridItem?.NodeName}, Effective VM for check: {selectedVm?.NodeName}");

            if (selectedVm == null)
            {
                System.Diagnostics.Debug.WriteLine("CanExecuteDeleteSelectedNodes: Returning False because selectedVm is null.");
                return false;
            }
            if (selectedVm.DomNode == null)
            {
                System.Diagnostics.Debug.WriteLine($"CanExecuteDeleteSelectedNodes: Returning False because selectedVm '{selectedVm.NodeName}' has null DomNode.");
                return false;
            }
            if (selectedVm.IsAddItemPlaceholder)
            {
                System.Diagnostics.Debug.WriteLine($"CanExecuteDeleteSelectedNodes: Returning False because selectedVm '{selectedVm.NodeName}' is IsAddItemPlaceholder.");
                return false;
            }
            
            if (_domToSchemaMap.TryGetValue(selectedVm.DomNode, out var schemaNode) && schemaNode != null)
            {
                bool canDelete = !schemaNode.IsReadOnly;
                System.Diagnostics.Debug.WriteLine($"CanExecuteDeleteSelectedNodes: Node '{selectedVm.NodeName}' has schema '{schemaNode.Name}'. IsReadOnly: {schemaNode.IsReadOnly}. CanDelete: {canDelete}");
                return canDelete;
            }
            
            System.Diagnostics.Debug.WriteLine($"CanExecuteDeleteSelectedNodes: Node '{selectedVm.NodeName}' has no schema or schema doesn't specify IsReadOnly. Returning True (allow delete).");
            return true; 
        }

        private void ExecuteDeleteSelectedNodes(DataGridRowItemViewModel? item = null) 
        {
            System.Diagnostics.Debug.WriteLine($"ExecuteDeleteSelectedNodes called. Item param: {item?.NodeName}, SelectedGridItem: {SelectedGridItem?.NodeName}");
            var selectedVm = item ?? SelectedGridItem;
            if (selectedVm == null || selectedVm.DomNode == null || !CanExecuteDeleteSelectedNodes(selectedVm))
            {
                return;
            }

            var nodeToDelete = selectedVm.DomNode;
            var parentNode = nodeToDelete.Parent;

            if (parentNode == null && _rootDomNode == nodeToDelete) 
            {
                 var confirmRootDelete = MessageBox.Show(
                    "Are you sure you want to delete the root node? This will clear the entire document.",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (confirmRootDelete == MessageBoxResult.Yes)
                {
                    var oldRoot = _rootDomNode;
                    var newRoot = new ObjectNode("$root", null); 
                    var op = new ReplaceRootOperation(oldRoot, newRoot); 
                    RecordEditOperation(op);
                    op.Redo(this); 
                }
                return;
            }

            if (parentNode == null) return;

            var confirmResult = MessageBox.Show(
                $"Are you sure you want to delete the node '{selectedVm.NodeName}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
            {
                return;
            }

            string nodeNameOrIndex = nodeToDelete.Name;
            int originalIndexInArray = -1;
            if (parentNode is ArrayNode arrayParent)
            {
                var itemsList = arrayParent.Items as IList<DomNode> ?? arrayParent.Items.ToList(); 
                originalIndexInArray = itemsList.IndexOf(nodeToDelete);
            }
            
            var removeOp = new RemoveNodeOperation(parentNode, nodeToDelete, nodeNameOrIndex, originalIndexInArray);
            RecordEditOperation(removeOp);
            removeOp.Redo(this); 

            DataGridRowItemViewModel? vmToSelect = null;

            if (parentNode is ArrayNode castedArrayParent) // Parent was an array, use a new name for the cast variable
            {
                if (originalIndexInArray < castedArrayParent.Items.Count && originalIndexInArray >= 0) // Item shifted into place
                {
                    _persistentVmMap.TryGetValue(castedArrayParent.Items[originalIndexInArray], out vmToSelect);
                    System.Diagnostics.Debug.WriteLine($"DeleteFocus: Array item deleted. Attempting to select item at same index {originalIndexInArray}. VM found: {vmToSelect != null}");
                }
                else if (castedArrayParent.Items.Count > 0) // Deleted last or out of bounds, select new last
                {
                    _persistentVmMap.TryGetValue(castedArrayParent.Items[castedArrayParent.Items.Count - 1], out vmToSelect);
                    System.Diagnostics.Debug.WriteLine($"DeleteFocus: Array item deleted. Attempting to select new last item. VM found: {vmToSelect != null}");
                }
                // If array becomes empty, vmToSelect remains null, will try to select parentArray itself (castedArrayParent) below via Fallback 1.
            }
            else if (parentNode is ObjectNode objectParent) // Parent was an object
            {
                var remainingChildren = objectParent.GetChildren().ToList();
                if (remainingChildren.Any()) // Try to select any remaining sibling
                {
                    foreach(var child in remainingChildren) {
                        if (_persistentVmMap.TryGetValue(child, out vmToSelect)) {
                            System.Diagnostics.Debug.WriteLine($"DeleteFocus: Object property deleted. Attempting to select sibling '{child.Name}'. VM found: {vmToSelect != null}");
                            break;
                        }
                    }
                    if (vmToSelect == null) System.Diagnostics.Debug.WriteLine($"DeleteFocus: Object property deleted. Siblings exist but no VM found for them.");
                }
                // If no siblings remain, vmToSelect remains null, will try to select objectParent below.
            }

            // Fallback 1: Try to select the parent node itself if no specific child/sibling was selected
            if (vmToSelect == null && parentNode != null)
            {
                if (_persistentVmMap.TryGetValue(parentNode, out vmToSelect))
                {
                    System.Diagnostics.Debug.WriteLine($"DeleteFocus: Fallback 1 - Selected parent node '{parentNode.Name}'. VM found: {vmToSelect != null}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"DeleteFocus: Fallback 1 - Parent node '{parentNode.Name}' exists but no VM found for it.");
                }
            }

            // Fallback 2: If still no selection, and the root DOM node itself exists and has a VM
            // (This is more relevant if parentNode was null or its VM wasn't found, and it wasn't the root that got replaced)
            if (vmToSelect == null && _rootDomNode != null && _persistentVmMap.TryGetValue(_rootDomNode, out var rootVm))
            {
                // Avoid re-selecting parent if parent *was* the root and already considered by Fallback 1
                if (parentNode != _rootDomNode) 
                {
                    vmToSelect = rootVm;
                    System.Diagnostics.Debug.WriteLine($"DeleteFocus: Fallback 2 - Selected root node '{_rootDomNode.Name}'.");
                }
            }

            // Fallback 3: Final fallback - select the first item in the entire list if list is not empty
            if (vmToSelect == null && FlatItemsSource.Any())
            {
                vmToSelect = FlatItemsSource.First();
                System.Diagnostics.Debug.WriteLine($"DeleteFocus: Fallback 3 - Selected first item in FlatItemsSource: {vmToSelect?.NodeName}");
            }
            
            if (vmToSelect == null) System.Diagnostics.Debug.WriteLine("DeleteFocus: All fallbacks failed, no item selected.");

            SelectedGridItem = vmToSelect; 
        }

        private void AddNodeToObject(ObjectNode parentObject, DomNode childNode)
        {
            // ASSUMPTION: childNode was ALREADY created with:
            // 1. Name = the correct property name.
            // 2. Parent = parentObject.
            // ObjectNode.AddChild just adds it to the internal dictionary.
            // It uses childNode.Name as the key internally if it needs to, or it's passed.
            // The AddChild in ObjectNode.cs takes (propertyName, child), so we use childNode.Name
            parentObject.AddChild(childNode.Name, childNode); 
            
            MapDomNodeToSchemaRecursive(childNode); 
            IsDirty = true;
            RefreshFlatList(); 
        }

        private void AddNodeToArrayAtIndex(ArrayNode parentArray, DomNode itemNode, int index)
        {
            // ASSUMPTION: itemNode was ALREADY created with:
            // 1. Name = index.ToString() (or its correct intended name if not just index).
            // 2. Parent = parentArray.
            // ArrayNode.InsertItem just adds it to the internal list.
            // It does not set Name or Parent.
            parentArray.InsertItem(index, itemNode); 
            // The ArrayNode.UpdateItemNames stub means subsequent item Names (and thus Paths) might be stale.
            // This is a known issue from the ArrayNode.cs analysis.
            
            MapDomNodeToSchemaRecursive(itemNode); 
            IsDirty = true;
            RefreshFlatList(); 
        }

        private void ClearMappingsRecursive(DomNode node)
        {
            if (node == null) return;

            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    ClearMappingsRecursive(child);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    ClearMappingsRecursive(item);
                }
            }

            _domToSchemaMap.Remove(node);
            _persistentVmMap.Remove(node);
        }

        // --- Expansion/Collapse All --- 
        private bool CanExecuteExpandCollapseSelectedRecursive(DataGridRowItemViewModel? item = null)
        {
            var targetItem = item ?? SelectedGridItem;
            return targetItem != null && targetItem.IsExpandable;
        }
        private void ExecuteExpandSelectedRecursive(DataGridRowItemViewModel? item = null)
        {
            var targetItem = item ?? SelectedGridItem;
            if (targetItem == null || !targetItem.IsExpandable) return;

            System.Diagnostics.Debug.WriteLine($"ExecuteExpandSelectedRecursive called for '{targetItem.NodeName}'");

            if (targetItem.DomNode != null)
            {
                SetExpansionStateRecursive(targetItem.DomNode, true);
            }
            else if (targetItem.IsSchemaOnlyNode && targetItem.SchemaContextNode != null)
            {
                // Expand this schema node and all its descendants in the schema expansion state map
                UpdateSchemaExpansionStates(true, targetItem.SchemaContextNode, targetItem.SchemaNodePathKey);
            }
            RefreshFlatList();
        }

        private void ExecuteCollapseSelectedRecursive(DataGridRowItemViewModel? item = null)
        {
            var targetItem = item ?? SelectedGridItem;
            if (targetItem == null || !targetItem.IsExpandable) return;

            System.Diagnostics.Debug.WriteLine($"ExecuteCollapseSelectedRecursive called for '{targetItem.NodeName}'");

            if (targetItem.DomNode != null)
            {
                SetExpansionStateRecursive(targetItem.DomNode, false);
            }
            else if (targetItem.IsSchemaOnlyNode && targetItem.SchemaContextNode != null)
            {
                // Collapse this schema node and all its descendants in the schema expansion state map
                UpdateSchemaExpansionStates(false, targetItem.SchemaContextNode, targetItem.SchemaNodePathKey);
            }
            RefreshFlatList();
        }

        private void SetExpansionStateRecursive(DomNode? node, bool expand)
        {
            if (node == null) return;

            if (_persistentVmMap.TryGetValue(node, out var vm))
            {
                if (vm.IsExpandable) // Check if the VM itself thinks it can be expanded/collapsed
                {
                    vm.SetExpansionStateInternal(expand); // Use internal setter to avoid loop if it calls RefreshFlatList
                }
            }
            // else if node is not in _persistentVmMap, its VM will be created during RefreshFlatList
            // and its initial expansion will be determined by default logic or schema state if applicable.

            if (node is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    SetExpansionStateRecursive(child, expand);
                }
            }
            else if (node is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    SetExpansionStateRecursive(item, expand);
                }
            }
        }

        private void UpdateSchemaExpansionStates(bool expand, SchemaNode? schemaNode, string currentPathKey)
        {
            if (schemaNode == null) return;

            // Update current schema node's expansion state if it's an object or array
            if (schemaNode.NodeType == SchemaNodeType.Object || schemaNode.NodeType == SchemaNodeType.Array)
            {
                _schemaNodeExpansionState[currentPathKey] = expand;
            }

            // Recursively update children if this node is now expanded (or if collapsing all, path is still relevant)
            if (schemaNode.Properties != null)
            {
                foreach (var prop in schemaNode.Properties)
                {
                    var childPathKey = string.IsNullOrEmpty(currentPathKey) ? prop.Key : $"{currentPathKey}/{prop.Key}";
                    UpdateSchemaExpansionStates(expand, prop.Value, childPathKey);
                }
            }
            // No explicit handling for ArrayItemSchema here, as _schemaNodeExpansionState is keyed by property paths.
            // Array item expansion is part of the parent ArrayNode's VM.
        }

        private void ExpandAncestors(DataGridRowItemViewModel? vm)
        {
            if (vm == null) return;

            // Attempt to get the parent from the DomNode first
            var parentNode = vm.DomNode?.Parent;
            while (parentNode != null)
            {
                if (_persistentVmMap.TryGetValue(parentNode, out var parentVm))
                {
                    parentVm.IsExpanded = true;
                }
                parentNode = parentNode.Parent;
            }

            // If it was a schema-only node, or to ensure UI consistency for complex cases,
            // we can also try to walk up the FlatItemsSource if the parent VM isn't found via DomNode path.
            // This part is a bit trickier as FlatItemsSource is flat. We'd rely on Depth.
            // For now, the DomNode based approach is primary. We might need a more robust parent lookup for schema-only if an issue.
            // One way for schema-only nodes: reconstruct parent path key and find VM if DomNode path is null.
            if (vm.IsSchemaOnlyNode && !string.IsNullOrEmpty(vm.SchemaNodePathKey))
            {
                string currentPathKey = vm.SchemaNodePathKey;
                while (currentPathKey.Contains("/"))
                {
                    int lastSlash = currentPathKey.LastIndexOf('/');
                    string parentPathKey = currentPathKey.Substring(0, lastSlash);
                    // Find the VM for this parent schema path key.
                    // This requires iterating _persistentVmMap or FlatItemsSource to find the schema VM by its path key.
                    var parentSchemaVm = _persistentVmMap.Values.FirstOrDefault(pvm => pvm.IsSchemaOnlyNode && pvm.SchemaNodePathKey == parentPathKey) ?? 
                                         FlatItemsSource.FirstOrDefault(fvm => fvm.IsSchemaOnlyNode && fvm.SchemaNodePathKey == parentPathKey);
                    if (parentSchemaVm != null)
                    {
                        parentSchemaVm.IsExpanded = true;
                    }
                    else
                    {
                        // Parent schema VM not found in current lists, might not be visible or doesn't exist as a separate entry.
                        // This can happen if schema structure is deep but UI flattens it differently.
                    }
                    currentPathKey = parentPathKey;
                    if (string.IsNullOrEmpty(currentPathKey)) break; // Reached root or invalid path segment
                }
            }
        }

        private void ValidatePendingEdits()
        {
            // Implement any additional validation logic you want to execute when edits are pending
            // This method can be called when edits are saved, or when the user navigates away from the editor
            // You can add any custom validation logic you want to execute here
        }
    }

    // Ensure SearchResult class is public if it needs to be accessed by DataGridRowItemViewModel indirectly
    // or if its properties are bound in a way that requires public accessibility.
    // For now, assuming it's mainly used internally by MainViewModel.
    // If linter errors about accessibility appear, make this public.
    public class SearchResult // Made public to avoid potential accessibility issues if list is exposed
    {
        public DataGridRowItemViewModel Item { get; set; }
        public string Path { get; set; } // Path or identifier of the found item
        public bool IsSchemaOnly { get; set; }
        public SearchResult(DataGridRowItemViewModel item, string path, bool isSchemaOnly)
        {
            Item = item;
            Path = path;
            IsSchemaOnly = isSchemaOnly;
        }
    }
} 
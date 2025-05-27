# Implementation Guidelines

# View Models

here are the skeletons for `MainViewModel` and `DataGridRowItemViewModel`, including private fields. These skeletons aim to be comprehensive in terms of declared members, with comments guiding their purpose and linking back to the specification concepts.

---

**Project: `JsonConfigEditor.Wpf` (WPF Application)**

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/ViewModelBase.cs \---  
using System.ComponentModel;  
using System.Runtime.CompilerServices;

namespace JsonConfigEditor.Wpf.ViewModels  
{  
    /// \<summary\>  
    /// Base class for ViewModels implementing INotifyPropertyChanged.  
    /// \</summary\>  
    public abstract class ViewModelBase : INotifyPropertyChanged  
    {  
        public event PropertyChangedEventHandler? PropertyChanged;  
        protected virtual void OnPropertyChanged(\[CallerMemberName\] string? propertyName \= null)  
        {  
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));  
        }  
    }  
}

**`DataGridRowItemViewModel.cs` (WPF Project \- ViewModels)**

C\#  
using JsonConfigEditor.Core.Dom;  
using JsonConfigEditor.Core.Schema;  
using JsonConfigEditor.Core.Validation; // For ValidationIssue (if needed directly)  
using System.ComponentModel; // For INotifyPropertyChanged from ViewModelBase  
using System.Text.Json;      // For JsonElement  
using System.Windows;        // For Thickness

namespace JsonConfigEditor.Wpf.ViewModels  
{  
    /// \<summary\>  
    /// ViewModel for a single row in the DataGrid. It wraps either an actual DomNode  
    /// or represents a schema-only placeholder for an addable node.  
    /// (From specification document, Section 2.3, 2.12, and various editing sections)  
    /// \</summary\>  
    public class DataGridRowItemViewModel : ViewModelBase  
    {  
        // \--- Private Fields \---  
        private DomNode? \_domNode; // Null if this is a schema-only placeholder  
        private readonly SchemaNode \_schemaContextNode; // Must always be present; for DOM nodes it's their mapped schema, for placeholders it's their definition  
        private readonly MainViewModel \_parentViewModel; // To interact with main logic (undo, materialization, etc.)

        private bool \_isExpandedInternal; // Actual expansion state  
        private string \_editValue \= string.Empty;  
        private bool \_isInEditMode;

        private bool \_isValid \= true; // Reflects validation status against schema  
        private string \_validationErrorMessage \= string.Empty;

        private bool \_isHighlightedInSearch;

        // Fields specific to placeholders  
        private readonly bool \_isAddItemPlaceholder; // True if this is an "Add item" placeholder for an array  
        private readonly ArrayNode? \_parentArrayNodeForPlaceholder; // Parent array if \_isAddItemPlaceholder  
        private readonly SchemaNode? \_itemSchemaForPlaceholder;    // Item schema if \_isAddItemPlaceholder

        private readonly string? \_nameOverrideForSchemaOnly; // Name for schema-only property placeholders  
        private readonly int \_depthForSchemaOnly; // Depth for schema-only/placeholder nodes

        // \--- Constructor for actual DomNodes \---  
        public DataGridRowItemViewModel(DomNode domNode, SchemaNode schemaContextNode, MainViewModel parentViewModel)  
        {  
            \_domNode \= domNode ?? throw new ArgumentNullException(nameof(domNode));  
            \_schemaContextNode \= schemaContextNode ?? throw new ArgumentNullException(nameof(schemaContextNode)); // Should always have a schema context if from DOM mapping  
            \_parentViewModel \= parentViewModel;  
            \_isAddItemPlaceholder \= false;  
            \_isExpandedInternal \= (\_domNode is ObjectNode || \_domNode is ArrayNode) && (\_domNode.Depth \< 2); // Default expand first few levels  
            // Initialize IsValid based on current validation status if available from MainViewModel  
        }

        // \--- Constructor for Schema-Only Property Placeholders (not "Add Item" array placeholder) \---  
        public DataGridRowItemViewModel(SchemaNode schemaPropertyNode, string propertyName, SchemaNode parentObjectSchema, MainViewModel parentViewModel, int depth)  
        {  
            \_domNode \= null; // Marks as schema-only  
            \_schemaContextNode \= schemaPropertyNode;  
            \_parentViewModel \= parentViewModel;  
            \_nameOverrideForSchemaOnly \= propertyName;  
            \_depthForSchemaOnly \= depth;  
            \_isAddItemPlaceholder \= false;  
            \_isExpandedInternal \= (schemaPropertyNode.NodeType \== SchemaNodeType.Object || schemaPropertyNode.NodeType \== SchemaNodeType.Array) && (depth \< 2);  
        }

        // \--- Constructor for "Add Item" Array Placeholders \---  
        public DataGridRowItemViewModel(ArrayNode parentArrayNode, SchemaNode? itemSchema, MainViewModel parentViewModel, int depth)  
        {  
            \_domNode \= null; // Marks as placeholder  
            \_schemaContextNode \= itemSchema ?? new SchemaNode("\[DynamicItem\]", typeof(object), false, false, null, null, null, null, null, false, null, null, true, null); // Fallback schema for placeholder display  
            \_parentViewModel \= parentViewModel;  
            \_parentArrayNodeForPlaceholder \= parentArrayNode;  
            \_itemSchemaForPlaceholder \= itemSchema;  
            \_nameOverrideForSchemaOnly \= "(Add new item)"; // Display text for placeholder  
            \_depthForSchemaOnly \= depth;  
            \_isAddItemPlaceholder \= true;  
            \_isExpandedInternal \= false; // Placeholders are not expandable  
        }

        // \--- Public Properties for DataBinding and Logic \---

        /// \<summary\>  
        /// Gets the underlying DomNode this ViewModel wraps.  
        /// Null if this represents a schema-only placeholder or an "Add Item" placeholder.  
        /// \</summary\>  
        public DomNode? DomNode \=\> \_domNode;

        /// \<summary\>  
        /// Gets the SchemaNode providing context for this item.  
        /// For DOM-present nodes, it's their mapped schema. For schema-only/placeholders, it's their definition.  
        /// \</summary\>  
        public SchemaNode SchemaContextNode \=\> \_schemaContextNode;

        /// \<summary\>  
        /// Reference to the MainViewModel for callbacks and accessing shared services.  
        /// \</summary\>  
        public MainViewModel ParentViewModel \=\> \_parentViewModel;

        /// \<summary\>  
        /// True if this ViewModel wraps an actual DomNode present in the JSON document.  
        /// False for schema-only property placeholders and "Add Item" array placeholders.  
        /// \</summary\>  
        public bool IsDomNodePresent \=\> \_domNode \!= null;

        /// \<summary\>  
        /// True if this ViewModel represents a property defined in the schema but not yet in the DOM.  
        /// Excludes the "Add Item" array placeholder.  
        /// (From specification document, Section 2.12)  
        /// \</summary\>  
        public bool IsSchemaOnlyNode \=\> \_domNode \== null && \!\_isAddItemPlaceholder;

        /// \<summary\>  
        /// True if this is the special "Add Item" placeholder for an array.  
        /// (From specification document, Section 2.4.3)  
        /// \</summary\>  
        public bool IsAddItemPlaceholder \=\> \_isAddItemPlaceholder;

        /// \<summary\>  
        /// Gets the display name of the node. Handles placeholders, root, and array indices.  
        /// (From specification document, Section 2.3.1)  
        /// \</summary\>  
        public string NodeName  
        {  
            get  
            {  
                string name \= \_isAddItemPlaceholder ? \_nameOverrideForSchemaOnly\! :  
                              IsSchemaOnlyNode ? \_nameOverrideForSchemaOnly\! :  
                              \_domNode\!.Name;

                // Determine if parent context is an array  
                bool isParentArray \= (\_domNode?.Parent is ArrayNode) ||  
                                     (\_parentArrayNodeForPlaceholder \!= null && \_isAddItemPlaceholder) ||  
                                     (IsSchemaOnlyNode && ParentViewModel.GetParentSchemaForSchemaOnlyNode(this)?.NodeType \== SchemaNodeType.Array); // Conceptual parent schema lookup

                if (isParentArray && int.TryParse(name, out \_)) // If name is numeric index  
                {  
                    return $"\[{name}\]";  
                }  
                return name \== "$root" ? "(Root)" : name;  
            }  
        }

        /// \<summary\>  
        /// Gets a string representation of the node's value for display.  
        /// For schema-only nodes, shows the default value. For placeholders, shows placeholder text.  
        /// (From specification document, Section 2.3.2, 2.12)  
        /// \</summary\>  
        public string ValueDisplay { /\* Complex getter based on IsSchemaOnlyNode, IsAddItemPlaceholder, DomNode type, SchemaContextNode.DefaultValue. Needs to be implemented. \*/ }

        /// \<summary\>  
        /// Gets the indentation margin based on the node's depth.  
        /// (From specification document, Section 2.3.1)  
        /// \</summary\>  
        public Thickness Indentation { get { return new Thickness((\_domNode?.Depth ?? \_depthForSchemaOnly) \* 15, 0, 0, 0); } } // 15 is placeholder for indent size

        /// \<summary\>  
        /// Gets a value indicating whether this node can be expanded (ObjectNode, ArrayNode, or schema-only object/array).  
        /// \</summary\>  
        public bool IsExpandable \=\> \!\_isAddItemPlaceholder &&  
                                    ((\_domNode is ObjectNode || \_domNode is ArrayNode) ||  
                                     (IsSchemaOnlyNode && (\_schemaContextNode.NodeType \== SchemaNodeType.Object || \_schemaContextNode.NodeType \== SchemaNodeType.Array)));

        /// \<summary\>  
        /// Gets or sets whether this node is currently expanded in the UI.  
        /// Setter notifies MainViewModel to refresh the flat list.  
        /// (From specification document, Section 2.3.3)  
        /// \</summary\>  
        public bool IsExpanded  
        {  
            get \=\> \_isExpandedInternal;  
            set  
            {  
                if (\_isExpandedInternal \!= value && IsExpandable)  
                {  
                    \_isExpandedInternal \= value;  
                    OnPropertyChanged();  
                    ParentViewModel.OnExpansionChanged(this); // Triggers list refresh  
                }  
            }  
        }

        /// \<summary\>  
        /// Allows MainViewModel to set expansion state internally (e.g., due to filtering) without triggering the full OnExpansionChanged logic.  
        /// \</summary\>  
        internal void SetExpansionStateInternal(bool expanded)  
        {  
            if (\_isExpandedInternal \!= expanded && IsExpandable)  
            {  
                \_isExpandedInternal \= expanded;  
                OnPropertyChanged(nameof(IsExpanded));  
            }  
        }

        /// \<summary\>  
        /// Gets a value indicating whether the node's value can be edited.  
        /// Considers IsReadOnly from schema, node type, and if it's a placeholder.  
        /// (From specification document, Section 2.4, 2.10)  
        /// \</summary\>  
        public bool IsEditable { /\* Complex getter logic. True for placeholders to trigger add/materialize. \*/ }

        /// \<summary\>  
        /// Gets or sets the string value currently being edited in an editor control.  
        /// (From specification document, Section 2.4)  
        /// \</summary\>  
        public string EditValue { get \=\> \_editValue; set \=\> SetProperty(ref \_editValue, value); }

        /// \<summary\>  
        /// Gets or sets a value indicating whether this row is in edit mode.  
        /// (From specification document, Section 2.4)  
        /// \</summary\>  
        public bool IsInEditMode  
        {  
            get \=\> \_isInEditMode;  
            set  
            {  
                if (IsEditable || (IsAddItemPlaceholder && value)) // Allow entering edit mode for add item placeholder  
                {  
                    if (SetProperty(ref \_isInEditMode, value) && \_isInEditMode)  
                    {  
                        // Initialize EditValue when entering edit mode  
                        // (From ValueDisplay or specific logic for types)  
                        InitializeEditValue();  
                        ParentViewModel.SetCurrentlyEditedItem(this);  
                    } else if (\!\_isInEditMode) {  
                        ParentViewModel.SetCurrentlyEditedItem(null);  
                    }  
                }  
                else if (\_isInEditMode && \!value) // Allow exiting edit mode even if not editable  
                {  
                     SetProperty(ref \_isInEditMode, false);  
                     ParentViewModel.SetCurrentlyEditedItem(null);  
                }  
            }  
        }

        /// \<summary\>  
        /// Gets a value indicating whether the current node's data is valid.  
        /// (From specification document, Section 2.4.1)  
        /// \</summary\>  
        public bool IsValid { get \=\> \_isValid; private set \=\> SetProperty(ref \_isValid, value); }

        /// \<summary\>  
        /// Gets the validation error message if IsValid is false.  
        /// (From specification document, Section 2.4.1)  
        /// \</summary\>  
        public string ValidationErrorMessage { get \=\> \_validationErrorMessage; private set \=\> SetProperty(ref \_validationErrorMessage, value); }

        /// \<summary\>  
        /// Gets or sets a value indicating if this item should be highlighted as a search result.  
        /// (From specification document, Section 2.6)  
        /// \</summary\>  
        public bool IsHighlightedInSearch { get \=\> \_isHighlightedInSearch; set \=\> SetProperty(ref \_isHighlightedInSearch, value); }

        // \--- Schema Derived Read-Only Properties for UI \---  
        /// \<summary\>True if the schema marks this node as read-only and it's a DOM-present node.\</summary\>  
        public bool IsNodeReadOnly \=\> IsDomNodePresent && (\_schemaContextNode?.IsReadOnly ?? false);  
        /// \<summary\>True if this is a DOM-present node but has no associated schema.\</summary\>  
        public bool IsUnschematized \=\> IsDomNodePresent && \_schemaContextNode \== null && \!(\_domNode is RefNode); // RefNodes have special handling  
        /// \<summary\>True if this is a RefNode pointing to an external or unresolvable path.\</summary\>  
        public bool IsRefLinkToExternalOrMissing { /\* Needs logic to check RefNode path resolution status from MainViewModel/service \*/ }

        // \--- Public Methods \---

        private void InitializeEditValue() { /\* Populate \_editValue from DomNode or DefaultValue \*/ }

        /// \<summary\>  
        /// Attempts to commit the edited value (\_editValue) back to the DomNode.  
        /// If this VM represents a schema-only node, it first triggers its materialization.  
        /// Performs validation and updates IsValid, ValidationErrorMessage.  
        /// (From specification document, Section 2.4, 2.12)  
        /// \</summary\>  
        public bool CommitEdit() { /\* Complex logic as outlined in previous responses. Returns true if successful/partially successful. \*/ return false; }

        /// \<summary\>  
        /// Cancels the current edit operation and reverts EditValue if necessary.  
        /// (From specification document, Section 2.4)  
        /// \</summary\>  
        public void CancelEdit() { /\* Reset IsInEditMode, potentially EditValue \*/ }

        /// \<summary\>  
        /// Sets the validation state and message for this item.  
        /// Called by MainViewModel after full document validation or by CommitEdit.  
        /// \</summary\>  
        public void SetValidationState(bool isValid, string message)  
        {  
            IsValid \= isValid;  
            ValidationErrorMessage \= message;  
        }

        /// \<summary\>  
        /// Updates the ViewModel with its associated SchemaNode (e.g., after a global remap).  
        /// \</summary\>  
        public void UpdateSchemaInfo(SchemaNode? schemaNode)  
        {  
            // Note: \_schemaContextNode is readonly after construction.  
            // This method is tricky if it's supposed to change \_schemaContextNode.  
            // More likely, this VM would be REPLACED in the FlatItemsSource if its fundamental schema context changes.  
            // If it's just for refreshing derived properties, that's fine.  
            // For now, let's assume it's about refreshing properties that depend on it.  
            OnPropertyChanged(nameof(IsNodeReadOnly));  
            OnPropertyChanged(nameof(NodeName)); // Name might come from schema for schema-only  
            OnPropertyChanged(nameof(ValueDisplay));  
            // ... other schema-dependent properties  
        }

        /// \<summary\>  
        /// Called by MainViewModel after an Undo/Redo operation modifies the underlying DomNode.  
        /// Refreshes display properties that depend directly on DomNode's state.  
        /// \</summary\>  
        public void RefreshDisplayProperties()  
        {  
            OnPropertyChanged(nameof(NodeName));  
            OnPropertyChanged(nameof(ValueDisplay));  
            OnPropertyChanged(nameof(IsDomNodePresent)); // If node was added/removed by undo/redo  
            // ... any other property that directly reads from DomNode ...  
        }  
    }  
}

---

**`MainViewModel.cs` (WPF Project \- ViewModels)**

C\#  
using JsonConfigEditor.Core.Dom;  
using JsonConfigEditor.Core.Parsing;  
using JsonConfigEditor.Core.Schema;  
using JsonConfigEditor.Core.SchemaLoading;  
using JsonConfigEditor.Core.Serialization;  
using JsonConfigEditor.Core.UndoRedo;  
using JsonConfigEditor.Core.Validation;  
using JsonConfigEditor.Wpf.Services; // For IDialogService, etc.  
using System.Collections.ObjectModel;  
using System.Windows.Input; // For ICommand

namespace JsonConfigEditor.Wpf.ViewModels  
{  
    public enum ChangeType { Modified, StructureChanged, SelectionChanged }

    /// \<summary\>  
    /// The main ViewModel for the application. Orchestrates interactions, data flow,  
    /// manages DOM, schema, validation, undo/redo, filtering, search, and UI state.  
    /// (From specification document, various sections)  
    /// \</summary\>  
    public class MainViewModel : ViewModelBase  
    {  
        // \--- Private Fields: Services \---  
        private readonly IJsonDomParser \_jsonParser;  
        private readonly IDomNodeToJsonSerializer \_jsonSerializer;  
        private readonly ISchemaLoaderService \_schemaLoader;  
        private readonly UndoRedoService \_undoRedoService;  
        private readonly IDialogService \_dialogService;  
        private readonly INavigationHistoryService \_navigationHistoryService;  
        private readonly ValidationService \_validationService;  
        // private readonly IClipboardService \_clipboardService; // Optional abstraction

        // \--- Private Fields: Core Data State \---  
        private DomNode? \_rootDomNode;  
        private readonly Dictionary\<DomNode, SchemaNode?\> \_domToSchemaMap \= new Dictionary\<DomNode, SchemaNode?\>();  
        private readonly Dictionary\<DomNode, List\<ValidationIssue\>\> \_validationIssuesMap \= new Dictionary\<DomNode, List\<ValidationIssue\>\>();  
        private readonly Dictionary\<string, bool\> \_userExpansionStates \= new Dictionary\<string, bool\>(); // Key: DomNode Path  
        private readonly Dictionary\<DomNode, DataGridRowItemViewModel\> \_persistentVmMap \= new Dictionary\<DomNode, DataGridRowItemViewModel\>(); // Cache VMs

        // \--- Private Fields: UI State & Filters/Search \---  
        private string? \_currentFilePath;  
        private bool \_isDirty; // Controlled by UndoRedoService callback  
        private string \_filterText \= string.Empty;  
        private bool \_showOnlyInvalidNodes;  
        private string \_searchText \= string.Empty;  
        private List\<SearchResultItem\> \_comprehensiveSearchResults \= new List\<SearchResultItem\>(); // For combined tree search  
        private int \_currentSearchIndex \= \-1;  
        private DataGridRowItemViewModel? \_selectedRowItem;  
        private bool \_showDomAndSchemaView \= true; // Default to combined view  
        private string \_statusBarMessage \= string.Empty;  
        private DataGridRowItemViewModel? \_currentlyEditedItemVm; // Tracks VM in edit mode

        // \--- Public Properties for DataBinding \---  
        public ObservableCollection\<DataGridRowItemViewModel\> FlatItemsSource { get; }  
        public DataGridRowItemViewModel? SelectedRowItem { get \=\> \_selectedRowItem; set \=\> SetProperty(ref \_selectedRowItem, value); }  
        public string? CurrentFilePath { get \=\> \_currentFilePath; private set \=\> SetProperty(ref \_currentFilePath, value); }  
        public bool IsDirty { get \=\> \_isDirty; private set \=\> SetProperty(ref \_isDirty, value); }  
        public string FilterText { get \=\> \_filterText; set { /\* ... call ApplyFiltersAndRefreshDisplayList ... \*/ } }  
        public bool ShowOnlyInvalidNodes { get \=\> \_showOnlyInvalidNodes; set { /\* ... call ApplyFiltersAndRefreshDisplayList ... \*/ } }  
        public string SearchText { get \=\> \_searchText; set { if(SetProperty(ref \_searchText, value)) \_comprehensiveSearchResults.Clear(); /\* Clear old results \*/ } }  
        public bool ShowDomAndSchemaView { get \=\> \_showDomAndSchemaView; set { if(SetProperty(ref \_showDomAndSchemaView, value)) ApplyFiltersAndRefreshDisplayList(); } }  
        public string StatusBarMessage { get \=\> \_statusBarMessage; private set \=\> SetProperty(ref \_statusBarMessage, value); }

        // Public getters for services if commands need direct access (e.g., for CanExecute)  
        public UndoRedoService UndoRedoService \=\> \_undoRedoService;  
        public INavigationHistoryService NavigationHistoryService \=\> \_navigationHistoryService;

        // \--- Commands \---  
        public ICommand NewFileCommand { get; }  
        public ICommand OpenFileCommand { get; }  
        // ... (All other commands as listed previously) ...  
        public ICommand ToggleDomSchemaViewCommand { get; }  
        public ICommand ShowLogCommand { get; }  
        public ICommand ActivateEditModeCommand { get; }  
        public ICommand ConfirmEditCommand { get; }  
        public ICommand CancelEditCommand { get; }  
        public ICommand JumpToDefinitionCommand { get; }  
        public ICommand NavigateBackCommand { get; }  
        public ICommand NavigateForwardCommand { get; }  
        public ICommand FindNextCommand { get; }  
        public ICommand FindPreviousCommand { get; }  
        // ... context menu commands: DeleteNodeCommand, CopyCommand, PasteCommand, Insert... commands, ResetToNullCommand ...

        // \--- Constructor \---  
        public MainViewModel(  
            IJsonDomParser jsonParser, IDomNodeToJsonSerializer jsonSerializer,  
            ISchemaLoaderService schemaLoader, UndoRedoService undoRedoService,  
            IDialogService dialogService, INavigationHistoryService navigationHistoryService,  
            ValidationService validationService /\*, IClipboardService clipboardService \*/)  
        {  
            \_jsonParser \= jsonParser;  
            \_jsonSerializer \= jsonSerializer;  
            \_schemaLoader \= schemaLoader;  
            \_undoRedoService \= undoRedoService;  
            \_dialogService \= dialogService;  
            \_navigationHistoryService \= navigationHistoryService;  
            \_validationService \= validationService;  
            // \_clipboardService \= clipboardService;

            FlatItemsSource \= new ObservableCollection\<DataGridRowItemViewModel\>();  
            \_undoRedoService.IsDirtyChangedCallback \= (dirtyState) \=\> IsDirty \= dirtyState;

            // Initialize all ICommand properties (e.g., using RelayCommand pattern)  
            // NewFileCommand \= new RelayCommand(async () \=\> await ExecuteNewFileAsync(), () \=\> true);  
            // OpenFileCommand \= new RelayCommand(async () \=\> await ExecuteOpenFileAsync(), () \=\> true);  
            // ... and so on for all commands, linking them to Execute\_Xyz and CanExecute\_Xyz methods.  
        }

        // \--- Public Methods (mostly called by Commands or other ViewModels/Services) \---  
        public async Task LoadJsonAsync(string jsonContent, string? filePath) { /\* ... Implemented based on previous outlines ... \*/ }  
        public void OnExpansionChanged(DataGridRowItemViewModel changedItemVm) { /\* ... Store \_userExpansionStates, call ApplyFiltersAndRefreshDisplayList ... \*/ }  
        public void RebuildDomToSchemaMapping() { /\* ... Implemented: clears map, calls MapNodeAndChildrenRecursive, RefreshAllViewModelsWithSchemaInfo ... \*/ }  
        public string GetDomNodePath(DomNode node) { /\* ... Implemented ... \*/ return ""; }  
        public void MapSingleNewDomNode(DomNode newDomNode, DomNode parentDomNode) { /\* ... Implemented ... \*/ }  
        public void UnmapDomNodeAndDescendants(DomNode domNode) { /\* ... Implemented ... \*/ }  
        public List\<ValidationIssue\> ValidateFullDocument() { /\* ... Implemented: clears \_validationIssuesMap, calls \_validationService, updates VMs, returns issues ... \*/ return new List\<ValidationIssue\>(); }  
        public DomNode? MaterializeDomNodeForSchema(DataGridRowItemViewModel vmToMaterialize) { /\* ... Implemented ... \*/ return null; }  
        public DataGridRowItemViewModel? AddNewItemToArrayFromPlaceholder(DataGridRowItemViewModel placeholderVm) { /\* ... Implemented ... \*/ return null; }  
        public DataGridRowItemViewModel? FindNextTabTarget(DataGridRowItemViewModel currentVm, bool navigateBackwards) { /\* ... Implemented ... \*/ return null; }  
        public void NotifyDomChanged(DomNode changedNodeOrParent, ChangeType changeType) { /\* ... Refresh VM or list, select item ... \*/ }  
        public void UpdateStatusBar(string message, bool isError \= false) { StatusBarMessage \= message; /\* Log if error \*/ }  
        public void SetCurrentlyEditedItem(DataGridRowItemViewModel? editingVm) { \_currentlyEditedItemVm \= editingVm; }  
        public bool IsAnyCellInEditMode() \=\> \_currentlyEditedItemVm \!= null && \_currentlyEditedItemVm.IsInEditMode;  
        public void ClearFilters() { /\* ... Implemented ... \*/ }  
        public void NotifyValidationStatusChanged(DataGridRowItemViewModel itemVm) { /\* ... If ShowOnlyInvalidNodes, ApplyFiltersAndRefreshDisplayList ... \*/ }

        // For UI to request scrolling an item into view  
        public event Action\<DataGridRowItemViewModel\>? ScrollToItemRequested;  
        public void RequestScrollToItem(DataGridRowItemViewModel item) \=\> ScrollToItemRequested?.Invoke(item);

        // For UI to request focusing an editor in a cell  
        public event Action\<DataGridRowItemViewModel\>? FocusEditorRequested;  
        public void RequestFocusOnEditor(DataGridRowItemViewModel item) \=\> FocusEditorRequested?.Invoke(item);

        // \--- Private Helper Methods for Filtering, List Building, Schema Lookup \---  
        private void ApplyFiltersAndRefreshDisplayList() { /\* ... Implemented: Clear FlatItemsSource, DetermineVisibleDomNodes, AddDomNodesToFlatListRecursive ... \*/ }  
        private HashSet\<DomNode\> DetermineVisibleDomNodes() { /\* ... Implemented ... \*/ return new HashSet\<DomNode\>(); }  
        private HashSet\<DomNode\> GetNodesAndTheirAncestors(HashSet\<DomNode\> nodes) { /\* ... Implemented ... \*/ return new HashSet\<DomNode\>(); }  
        private void AddDomNodesToFlatListRecursive(DomNode domNode, int depth, HashSet\<DomNode\> visibleDomNodes, HashSet\<DomNode\> directlyMatchingNodesForExpansion) { /\* ... Implemented, uses GetOrCreateDataGridRowItemViewModel, sets expansion based on filter ... \*/ }  
        private bool ShouldNodeBeExpandedByFilter(DomNode node, DataGridRowItemViewModel vm, HashSet\<DomNode\> finalVisibleNodes, HashSet\<DomNode\> directlyMatchingNodes) { /\* ... Implemented ... \*/ return false; }  
        private DataGridRowItemViewModel GetOrCreateDataGridRowItemViewModel(DomNode domNode, SchemaNode? schemaNode) { /\* ... Implemented: uses \_persistentVmMap, sets IsValid from \_validationIssuesMap ... \*/ return null\!; }  
        private DomNode? FindOrMaterializeParentDomFor(DataGridRowItemViewModel childVm) { /\* ... Complex: Implemented based on previous outlines ... \*/ return null; }  
        private List\<PathSegment\> CalculatePathToParentForSchemaOnlyNode(DataGridRowItemViewModel vm) { /\* ... Complex: Implemented based on previous outlines ... \*/ return new List\<PathSegment\>(); }  
        private SchemaNode? FindSchemaForDomNode(DomNode node) { \_domToSchemaMap.TryGetValue(node, out var schema); return schema; }  
        private SchemaNode? FindSchemaInHierarchy(SchemaNode currentSchemaScope, string fullDomPath, string basePathCoveredByScope) { /\* ... Implemented ... \*/ return null; }  
        private void RefreshAllViewModelsWithSchemaInfo() { /\* ... Implemented ... \*/ }  
        private SchemaNode? GetParentSchemaForSchemaOnlyNode(DataGridRowItemViewModel schemaOnlyVm) { /\* Helper to deduce parent schema for schema-only node (e.g. for NodeName formatting) \*/ return null; }

        // \--- Private Methods for Search Logic (from previous outline) \---  
        private class SearchResultItem { /\* ... Name, Path, IsDomPresent, DomNode, SchemaNode ... \*/ }  
        private async Task ExecuteFind(bool findNext) { /\* ... Implemented: calls BuildComprehensiveSearchResults, RevealAndFindViewModel ... \*/ }  
        private void BuildComprehensiveSearchResults() { /\* ... Implemented: calls AddSearchableNodesRecursive, AddSearchableSchemaOnlyNodesRecursive ... \*/ }  
        private void AddSearchableNodesRecursive(DomNode domNode, SchemaNode? schemaForDomNode, string currentPath) { /\* ... \*/ }  
        private void AddSearchableSchemaOnlyNodesRecursive(SchemaNode schemaNode, string nodeName, string currentPath, SchemaNode? parentSchema) { /\* ... \*/ }  
        private async Task\<DataGridRowItemViewModel?\> RevealAndFindViewModel(SearchResultItem searchResult) { /\* ... Implemented: ensures view active, expands parents, finds VM ... \*/ return null; }  
        private string GetPathForViewModel(DataGridRowItemViewModel vm) { /\* ... Implemented for search result matching ... \*/ return ""; }

        // \--- Private Methods for Command Execution Logic \---  
        // private async Task ExecuteNewFileAsync() { /\* ... \*/ }  
        // ... etc. for all commands listed in ICommand properties ...  
    }

    // PathSegment class (nested in MainViewModel or separate) if used by CalculatePathToParentForSchemaOnlyNode  
    // private class PathSegment { public string Name { get; } public SchemaNode Schema { get; } ... }  
}

**Key Points in these Skeletons:**

* **`ViewModelBase`:** Assumed for `INotifyPropertyChanged` implementation (`SetProperty` and `OnPropertyChanged`).  
* **Service Injection:** `MainViewModel` takes its dependencies (services like parser, loader, dialog, undo/redo) via its constructor, facilitating testing and decoupling.  
* **Commands:** Placeholder `ICommand` properties in `MainViewModel`. These would be initialized with `RelayCommand` or a similar MVVM command pattern, linking to `Execute_Xyz` and `CanExecute_Xyz` methods.  
* **`DataGridRowItemViewModel` Types:** It handles three conceptual types of items:  
  1. Actual `DomNode`s.  
  2. Schema-only property placeholders (e.g., a missing string property from an object's schema).  
  3. The "Add item" array placeholder. The constructors and properties like `IsSchemaOnlyNode`, `IsAddItemPlaceholder` help manage this.  
* **Asynchronous Operations:** Methods like `LoadJsonAsync`, `LoadSchemasFromAssembliesAsync` are marked `async Task` where appropriate.  
* **UI Interaction Methods:** `RequestScrollToItem` and `RequestFocusOnEditor` are defined as events or actions that the View (code-behind) would subscribe to, to interact with the `DataGrid`.  
* **Complexity in Helpers:** Methods like `CalculatePathToParentForSchemaOnlyNode`, `FindOrMaterializeParentDomFor`, and the recursive list builders (`AddDomNodesToFlatListRecursive`, etc.) contain significant logic that has been outlined in previous detailed explanations. The comments point to their complex nature.  
* **Synchronization:** Many actions (editing, undo/redo, filtering, schema loading) will require `ApplyFiltersAndRefreshDisplayList()` or at least targeted updates to `FlatItemsSource` and `DataGridRowItemViewModel` properties to keep the UI consistent.

# DOM node classes

**Project: `JsonConfigEditor.Core` (Core Logic Library)**

C\#  
// \--- File: JsonConfigEditor.Core/Dom/DomNode.cs \---  
using System.Text.Json;

namespace JsonConfigEditor.Core.Dom  
{  
    /// \<summary\>  
    /// Abstract base class for all nodes in the JSON Document Object Model.  
    /// (From specification document, Section 2.1)  
    /// \</summary\>  
    public abstract class DomNode  
    {  
        /// \<summary\>  
        /// The name of the node (property name or array index as a string).  
        /// \</summary\>  
        public string Name { get; protected set; }

        /// \<summary\>  
        /// Reference to the parent node in the DOM tree. Null for the root node.  
        /// \</summary\>  
        public DomNode? Parent { get; protected set; }

        protected DomNode(string name, DomNode? parent) { /\* ... \*/ }

        /// \<summary\>  
        /// Calculates the depth of this node in the DOM tree.  
        /// (From specification document, Section 2.3.1)  
        /// \</summary\>  
        public int Depth { get { /\* ... \*/ return 0; } }  
    }

    public class ObjectNode : DomNode  
    {  
        /// \<summary\>  
        /// Gets the dictionary of child nodes, where the key is the property name.  
        /// (From specification document, Section 2.1)  
        /// \</summary\>  
        public Dictionary\<string, DomNode\> Children { get; }  
        public ObjectNode(string name, DomNode? parent) : base(name, parent) { Children \= new Dictionary\<string, DomNode\>(); }  
    }

    public class ArrayNode : DomNode  
    {  
        /// \<summary\>  
        /// Gets the list of array elements (DomNodes).  
        /// (From specification document, Section 2.1)  
        /// \</summary\>  
        public List\<DomNode\> Items { get; }  
        public ArrayNode(string name, DomNode? parent) : base(name, parent) { Items \= new List\<DomNode\>(); }  
    }

    public class ValueNode : DomNode  
    {  
        /// \<summary\>  
        /// Gets or sets the JsonElement representing the primitive value.  
        /// (From specification document, Section 2.1)  
        /// \</summary\>  
        public JsonElement Value { get; set; }  
        public ValueNode(string name, DomNode? parent, JsonElement value) : base(name, parent) { Value \= value; }  
    }

    public class RefNode : DomNode  
    {  
        /// \<summary\>  
        /// Gets or sets the path string from the "$ref" property.  
        /// (From specification document, Section 2.1)  
        /// \</summary\>  
        public string ReferencePath { get; set; }  
        /// \<summary\>  
        /// Stores the original JsonElement that represented this $ref object.  
        /// (From specification document, Section 2.1)  
        /// \</summary\>  
        public JsonElement OriginalValue { get; }  
        public RefNode(string name, DomNode? parent, string referencePath, JsonElement originalValue) : base(name, parent) { /\* ... \*/ }  
    }  
}

# DOM node schema

**Project: `JsonConfigEditor.Core` (Core Logic Library)**

// \--- File: JsonConfigEditor.Core/Schema/SchemaNode.cs \---  
namespace JsonConfigEditor.Core.Schema  
{  
    /// \<summary\>  
    /// Defines the type of a schema node (Value, Object, or Array).  
    /// (From specification document, Section 2.2 & Clarification 5\)  
    /// \</summary\>  
    public enum SchemaNodeType { Value, Object, Array }

    /// \<summary\>  
    /// Represents a node in the schema tree, derived from C\# model classes and their attributes.  
    /// This class should be treated as immutable after its initial construction.  
    /// (From specification document, Section 2.2 & Best Practice Tip)  
    /// \</summary\>  
    public class SchemaNode  
    {  
        public string Name { get; }  
        public Type ClrType { get; }  
        public bool IsRequired { get; }  
        public bool IsReadOnly { get; }  
        public object? DefaultValue { get; }  
        public double? Min { get; }  
        public double? Max { get; }  
        public string? RegexPattern { get; }  
        public List\<string\>? AllowedValues { get; } // Stored consistently (e.g., lowercase)  
        public bool IsEnumFlags { get; }  
        public IReadOnlyDictionary\<string, SchemaNode\>? Properties { get; }  
        public SchemaNode? AdditionalPropertiesSchema { get; }  
        public bool AllowAdditionalProperties { get; }  
        public SchemaNode? ItemSchema { get; }  
        public string? MountPath { get; }  
        public SchemaNodeType NodeType { get { /\* ... \*/ return SchemaNodeType.Value; } }

        public SchemaNode(  
            string name, Type clrType, bool isRequired, bool isReadOnly, object? defaultValue,  
            double? min, double? max, string? regexPattern, List\<string\>? allowedValues, bool isEnumFlags,  
            IReadOnlyDictionary\<string, SchemaNode\>? properties, SchemaNode? additionalPropertiesSchema, bool allowAdditionalProperties,  
            SchemaNode? itemSchema, string? mountPath \= null)  
        { /\* ... \*/ }  
    }  
}

// \--- File: JsonConfigEditor.Core/SchemaLoading/ISchemaLoaderService.cs \---  
// ... (as defined in the "all c\# interfaces" response) ...

// \--- File: JsonConfigEditor.Core/SchemaLoading/SchemaLoaderService.cs \---  
using JsonConfigEditor.Core.Schema;  
using JsonConfigEditor.Contracts.Attributes;  
using System.Reflection;

namespace JsonConfigEditor.Core.SchemaLoading  
{  
    /// \<summary\>  
    /// Service responsible for loading schema definitions from C\# assemblies.  
    /// (Implements ISchemaLoaderService)  
    /// (From specification document, Section 2.2)  
    /// \</summary\>  
    public class SchemaLoaderService : ISchemaLoaderService  
    {  
        public IReadOnlyDictionary\<string, SchemaNode\> RootSchemas { get; private set; }  
        public IReadOnlyList\<string\> ErrorMessages { get; private set; }

        public SchemaLoaderService() { RootSchemas \= new Dictionary\<string, SchemaNode\>(); ErrorMessages \= new List\<string\>(); }

        /// \<summary\>  
        /// Asynchronously loads schema definitions from assemblies.  
        /// (From specification document, Section 2.2 & Best Practice Tip for async)  
        /// \</summary\>  
        public async Task LoadSchemasFromAssembliesAsync(IEnumerable\<string\> assemblyPaths) { /\* ... \*/ await Task.CompletedTask; }  
        // private void ProcessAssembly(Assembly assembly) { /\* ... \*/ }  
        // private SchemaNode BuildSchemaRecursive(Type currentType, string nodeName, PropertyInfo? sourcePropertyInfo, HashSet\<Type\> processedTypesInPath) { /\* ... \*/ return null; }  
        // Helper methods for reflection: IsTypeRequired, IsPropertyRequired, GetDefaultValue, GetPropertyDefaultValue, IsCollectionType, IsDictionaryType, etc.  
    }  
}

// \--- File: JsonConfigEditor.Core/Serialization/IDomNodeToJsonSerializer.cs \---  
// ... (as defined in the "all c\# interfaces" response) ...

# DOM NOde factory

// \--- File: JsonConfigEditor.Core/Dom/DomFactory.cs \---  
using JsonConfigEditor.Core.Schema;  
using System.Text.Json;  
namespace JsonConfigEditor.Core.Dom  
{  
    /// \<summary\>  
    /// Utility class for creating DomNode instances, especially with default values from schema.  
    /// (From specification document, Section 2.12, 2.4.3)  
    /// \</summary\>  
    public static class DomFactory  
    {  
        /// \<summary\>  
        /// Creates a new DomNode based on a SchemaNode, populating it with default values.  
        /// \</summary\>  
        public static DomNode CreateDefaultFromSchema(string name, SchemaNode schema, DomNode? parent) { /\* ... \*/ return null\!; }  
    }  
}

---

# Combined view of DOM and schema trees

Okay, implementing the combined display of DOM nodes and schema-only (addable) nodes in the `DataGrid` is a key feature for your JSON editor. This allows users to see what's currently in their JSON and what *could* be added according to the schema.

Here's how I would approach implementing this, building upon the existing `MainViewModel` and `DataGridRowItemViewModel` structures, and referencing the specification:

**Core Idea:**

The `MainViewModel.FlatItemsSource` will contain `DataGridRowItemViewModel` instances. Some of these will wrap actual `DomNode`s, while others (when "DOM \+ Schema" view is active) will represent "schema-only" placeholders. These placeholders need to be visually distinct and behave differently upon interaction (e.g., editing them materializes the actual `DomNode`).

**1\. Enhance `DataGridRowItemViewModel`:**

It needs to understand if it's representing a real `DomNode` or a schema-only placeholder.

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/DataGridRowItemViewModel.cs \---  
// ... (existing using statements and class structure) ...  
using JsonConfigEditor.Core.Dom;  
using JsonConfigEditor.Core.Schema;

public class DataGridRowItemViewModel : ViewModelBase  
{  
    // Existing properties: DomNode (can be null for schema-only), AssociatedSchemaNode (must exist for schema-only), ParentViewModel, etc.

    /// \<summary\>  
    /// The actual DomNode this VM wraps. Can be null if this VM represents a schema-only (addable) node.  
    /// \</summary\>  
    public DomNode? DomNode { get; private set; } // Changed from DomNode DomNode \=\> \_domNode;

    /// \<summary\>  
    /// The SchemaNode that this VM represents.  
    /// For schema-only nodes, this is their definition.  
    /// For DomNodes, this is their mapped schema.  
    /// \</summary\>  
    public SchemaNode SchemaContextNode { get; } // SchemaNode must always be present for a row item in this combined view logic.

    /// \<summary\>  
    /// Indicates if this ViewModel represents a node that exists in the DOM.  
    /// If false, it's a schema-only (addable) node.  
    /// \</summary\>  
    public bool IsDomNodePresent \=\> DomNode \!= null;

    /// \<summary\>  
    /// The name to display for the node. For schema-only nodes, it's from the SchemaNode.  
    /// \</summary\>  
    public string NodeName  
    {  
        get  
        {  
            string name \= DomNode?.Name ?? SchemaContextNode.Name;  
            if (DomNode?.Parent is ArrayNode || (\!IsDomNodePresent && \_schemaContextParentNode is ArrayNode)) // Heuristic for array item  
            {  
                // This part needs refinement: how to get the parent context if it's a schema-only node.  
                // Maybe the name passed to the VM for schema-only array items is already formatted.  
                return $"\[{name}\]";  
            }  
            return name \== "$root" ? "(Root)" : name;  
        }  
    }

    /// \<summary\>  
    /// The value to display. For schema-only nodes, this would be their default value from the schema.  
    /// (Spec Section 2.12)  
    /// \</summary\>  
    public string ValueDisplay  
    {  
        get  
        {  
            if (IsDomNodePresent && DomNode \!= null) // DomNode is present  
            {  
                if (DomNode is ValueNode vn) return vn.Value.ToString();  
                if (DomNode is RefNode rn) return $"$ref: {rn.ReferencePath}";  
                if (DomNode is ArrayNode an) return $"\[{an.Items.Count} items\]";  
                if (DomNode is ObjectNode) return "\[Object\]";  
                return string.Empty;  
            }  
            else // Schema-only node  
            {  
                // Display default value from SchemaContextNode.DefaultValue  
                // This requires converting the object? DefaultValue to a display string.  
                // For complex types (Object/Array), show placeholder text or structure.  
                if (SchemaContextNode.NodeType \== SchemaNodeType.Object) return "\[Object (default)\]";  
                if (SchemaContextNode.NodeType \== SchemaNodeType.Array) return "\[Array (default)\]";  
                return SchemaContextNode.DefaultValue?.ToString() ?? (SchemaContextNode.IsRequired ? "(required, null)" : "(null)"); // Example  
            }  
        }  
    }

    /// \<summary\>  
    /// Indicates if the node is a schema-only placeholder and should be rendered differently (e.g., grayed out).  
    /// (Spec Section 2.3, 2.12)  
    /// \</summary\>  
    public bool IsSchemaOnlyNode \=\> \!IsDomNodePresent;

    private SchemaNode? \_schemaContextParentNode; // Helper for determining array item display name for schema-only nodes

    /// \<summary\>  
    /// Constructor for nodes present in the DOM.  
    /// \</summary\>  
    public DataGridRowItemViewModel(DomNode domNode, SchemaNode schemaContextNode, MainViewModel parentViewModel)  
    {  
        DomNode \= domNode;  
        SchemaContextNode \= schemaContextNode; // This schemaNode is the one mapped to the domNode  
        ParentViewModel \= parentViewModel;  
        // ... initialize \_isExpanded based on DomNode type ...  
    }

    /// \<summary\>  
    /// Constructor for schema-only (addable) nodes.  
    /// \</summary\>  
    public DataGridRowItemViewModel(SchemaNode schemaContextNode, string name, SchemaNode? schemaContextParentNode, MainViewModel parentViewModel, int depth) // Depth needed for indentation  
    {  
        DomNode \= null; // Mark as schema-only  
        SchemaContextNode \= schemaContextNode;  
        ParentViewModel \= parentViewModel;  
        \_nameOverride \= name; // Store the intended name (e.g., property name from parent schema)  
        \_schemaContextParentNode \= schemaContextParentNode;  
        \_depthOverride \= depth;  
        // ... initialize \_isExpanded based on SchemaContextNode type ...  
    }

    private string? \_nameOverride; // For schema-only nodes  
    private int \_depthOverride \= \-1; // For schema-only nodes

    public new string NodeName // 'new' keyword to hide base if DomNode.Name was public directly accessed.  
    {  
        get  
        {  
            string name \= DomNode?.Name ?? \_nameOverride ?? SchemaContextNode.Name;  
            // Logic to format as "\[index\]" if it's an array item placeholder might need parent context.  
            // For simplicity, ensure \_nameOverride is correctly set by the MainViewModel.  
             bool isArrayItemContext \= (DomNode?.Parent is ArrayNode) ||  
                                     (\!IsDomNodePresent && \_schemaContextParentNode?.NodeType \== SchemaNodeType.Array);

            if (isArrayItemContext && int.TryParse(name, out \_)) // Check if name is purely numeric (index)  
            {  
                return $"\[{name}\]";  
            }  
            return name \== "$root" ? "(Root)" : name;  
        }  
    }  
    public new Thickness Indentation \=\> new Thickness((DomNode?.Depth ?? \_depthOverride) \* 15, 0, 0, 0);

    // Editability needs to consider schema-only nodes (editing them creates the DomNode)  
    public new bool IsEditable  
    {  
        get  
        {  
            if (SchemaContextNode.IsReadOnly && IsDomNodePresent) return false; // Read-only DOM nodes are not editable  
            if (IsSchemaOnlyNode && SchemaContextNode.NodeType \== SchemaNodeType.Value) return true; // Schema-only values can be edited to create them  
            if (IsSchemaOnlyNode && SchemaContextNode.NodeType \== SchemaNodeType.Object) return false; // Schema-only objects are not directly edited, but their properties can be added/edited  
            if (IsSchemaOnlyNode && SchemaContextNode.NodeType \== SchemaNodeType.Array) return false; // Schema-only arrays, similar to objects

            if (DomNode is ValueNode) return true;  
            if (DomNode is RefNode) return true; // Path of RefNode is editable  
            return false;  
        }  
    }

    // CommitEdit needs to handle creating the DomNode if it's a schema-only node being edited  
    public new bool CommitEdit()  
    {  
        if (\!IsInEditMode || \!IsEditable)  
        {  
            IsInEditMode \= false;  
            return true;  
        }

        if (IsSchemaOnlyNode)  
        {  
            // This is the crucial part: Materialize the DomNode  
            // Tell MainViewModel to create this node (and any necessary parents) in the actual DOM tree.  
            // The MainViewModel would then update this DataGridRowItemViewModel or replace it with one  
            // that wraps the new DomNode.  
            var materializedDomNode \= ParentViewModel.MaterializeSchemaNode(this, SchemaContextNode, \_schemaContextParentNode); // This is a conceptual method  
            if (materializedDomNode \!= null)  
            {  
                this.DomNode \= materializedDomNode; // Now it's a real DOM node  
                // Now proceed with parsing \_editValue and applying it to the newly created DomNode  
            }  
            else  
            {  
                // Failed to materialize (e.g., couldn't create parent path)  
                IsValid \= false;  
                ValidationErrorMessage \= "Failed to create node in DOM.";  
                IsInEditMode \= false; // Or keep in edit mode? Spec implies editing creates node.  
                return false;  
            }  
        }

        // ... existing CommitEdit logic for parsing \_editValue, validating against SchemaContextNode,  
        // updating DomNode.Value (or RefNode.ReferencePath) ...  
        // Ensure to call ParentViewModel.MarkAsDirty() and ParentViewModel.UndoRedoService.RecordOperation()

        // If parsing/validation fails for a schema-only node after materialization:  
        // The node now exists in the DOM but is invalid. This is consistent with spec.

        IsInEditMode \= false;  
        // Refresh ValueDisplay as DomNode might have changed or its value updated  
        OnPropertyChanged(nameof(ValueDisplay));  
        OnPropertyChanged(nameof(IsSchemaOnlyNode)); // It's no longer schema-only  
        return IsValid;  
    }

    // ... rest of the class ...  
}

**2\. Modify `MainViewModel`'s `FlatItemsSource` Population Logic:**

The method that builds `FlatItemsSource` (e.g., `RebuildFlatItemsSource` and its recursive helpers like `AddNodeAndChildrenToFlatList`) needs to be aware of the "Show DOM \+ Schema" toggle (`ShowDomAndSchemaView` property).

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
// ... (previous using statements and class structure) ...

public class MainViewModel : ViewModelBase  
{  
    // ... existing properties: \_rootDomNode, FlatItemsSource, \_schemaLoader, \_domToSchemaMap, ShowDomAndSchemaView ...

    private void RebuildFlatItemsSourceInternal() // Renamed to avoid confusion with public Rebuild...  
    {  
        FlatItemsSource.Clear();  
        if (\_rootDomNode \== null) return;

        // Start traversal from the root DomNode and its mapped root SchemaNode  
        SchemaNode? rootSchemaContext \= \_domToSchemaMap.TryGetValue(\_rootDomNode, out var rs) ? rs : null;  
        if (rootSchemaContext \== null && ShowDomAndSchemaView) {  
            // If no DOM root but schema view is on, try to find a root schema for "" mount path  
             \_schemaLoader.RootSchemas.TryGetValue("", out rootSchemaContext);  
             if (rootSchemaContext \!= null && \_rootDomNode \== null) { // No DOM, but have a root schema  
                // Potentially create a placeholder VM for the root schema if DOM is entirely empty  
                // This scenario needs careful thought: what's the "parent" for the top-level schema items?  
                // For now, let's assume \_rootDomNode always exists (e.g. starts as new ObjectNode).  
             }  
        }

        AddNodeAndPotentialSchemaChildrenToFlatList(\_rootDomNode, rootSchemaContext, 0);  
    }

    private void AddNodeAndPotentialSchemaChildrenToFlatList(DomNode? domNode, SchemaNode? schemaContext, int depth, SchemaNode? parentSchemaForContext \= null)  
    {  
        DataGridRowItemViewModel vm;

        if (domNode \!= null) // If a DOM node exists for this level  
        {  
            // Try to get its specific schema, fallback to context schema (e.g., item schema for array items)  
            SchemaNode? actualSchemaForDomNode \= \_domToSchemaMap.TryGetValue(domNode, out var mappedSchema) ? mappedSchema : schemaContext;  
            if (actualSchemaForDomNode \== null) { /\* This should ideally not happen if mapping is complete or context is passed \*/ }

            vm \= new DataGridRowItemViewModel(domNode, actualSchemaForDomNode ?? schemaContext\!, this); // schemaContext should not be null if domNode has one.  
            FlatItemsSource.Add(vm);

            if (vm.IsExpanded)  
            {  
                if (domNode is ObjectNode on)  
                {  
                    var processedSchemaProps \= new HashSet\<string\>();  
                    foreach (var childDomNode in on.Children.Values.OrderBy(n \=\> n.Name)) // Or original order  
                    {  
                        // Pass the schema for this specific childDomNode if available,  
                        // or the parent object's AdditionalPropertiesSchema if applicable.  
                        SchemaNode? childSchemaContext \= actualSchemaForDomNode?.Properties?.TryGetValue(childDomNode.Name, out var propSchema) \== true ? propSchema  
                                                        : actualSchemaForDomNode?.AdditionalPropertiesSchema;  
                        AddNodeAndPotentialSchemaChildrenToFlatList(childDomNode, childSchemaContext, depth \+ 1, actualSchemaForDomNode);  
                        if (actualSchemaForDomNode?.Properties?.ContainsKey(childDomNode.Name) \== true)  
                        {  
                            processedSchemaProps.Add(childDomNode.Name);  
                        }  
                    }  
                    // Now, if ShowDomAndSchemaView is true, add schema-only properties  
                    if (ShowDomAndSchemaView && actualSchemaForDomNode?.Properties \!= null)  
                    {  
                        foreach (var schemaPropPair in actualSchemaForDomNode.Properties)  
                        {  
                            if (\!processedSchemaProps.Contains(schemaPropPair.Key))  
                            {  
                                // This schema property is not in the DOM, add as schema-only  
                                var schemaOnlyVm \= new DataGridRowItemViewModel(schemaPropPair.Value, schemaPropPair.Key, actualSchemaForDomNode, this, depth \+ 1);  
                                FlatItemsSource.Add(schemaOnlyVm);  
                                // If schema-only objects/arrays are expanded by default, recurse  
                                if (schemaOnlyVm.IsExpanded) {  
                                     AddSchemaOnlyChildrenToFlatList(schemaPropPair.Value, depth \+ 1, actualSchemaForDomNode);  
                                }  
                            }  
                        }  
                    }  
                }  
                else if (domNode is ArrayNode an)  
                {  
                    SchemaNode? itemSchemaContext \= actualSchemaForDomNode?.ItemSchema;  
                    foreach (var childDomNode in an.Items)  
                    {  
                        AddNodeAndPotentialSchemaChildrenToFlatList(childDomNode, itemSchemaContext, depth \+ 1, actualSchemaForDomNode);  
                    }  
                    // "Add item" placeholder for arrays (handled separately or as a special VM type)  
                }  
            }  
        }  
        else if (ShowDomAndSchemaView && schemaContext \!= null) // No DOM node, but schema context exists (this is a schema-only branch)  
        {  
            // This case is for when we start rendering a schema-only branch from an expanded schema-only parent.  
            // The initial call to this function usually has a domNode (the root).  
            // This else-if might be better handled by AddSchemaOnlyChildrenToFlatList.  
        }  
    }

    private void AddSchemaOnlyChildrenToFlatList(SchemaNode parentSchemaNode, int depth, SchemaNode? schemaContextGrandParent \= null)  
    {  
        // Called when an expanded schema-only node needs to show its schema-defined children.  
        if (parentSchemaNode.NodeType \== SchemaNodeType.Object && parentSchemaNode.Properties \!= null)  
        {  
            foreach (var schemaPropPair in parentSchemaNode.Properties)  
            {  
                var schemaOnlyVm \= new DataGridRowItemViewModel(schemaPropPair.Value, schemaPropPair.Key, parentSchemaNode, this, depth);  
                FlatItemsSource.Add(schemaOnlyVm);  
                if (schemaOnlyVm.IsExpanded)  
                {  
                    AddSchemaOnlyChildrenToFlatList(schemaPropPair.Value, depth \+ 1, parentSchemaNode);  
                }  
            }  
        }  
        else if (parentSchemaNode.NodeType \== SchemaNodeType.Array && parentSchemaNode.ItemSchema \!= null)  
        {  
            // For arrays, usually we show an "Add item" placeholder.  
            // Showing multiple default items might be too much, unless the schema default specifies items.  
            // For now, a schema-only array might just show its type, or a placeholder for adding.  
        }  
    }

    /// \<summary\>  
    /// Called when a schema-only DataGridRowItemViewModel is edited, to create the actual DomNode  
    /// and its necessary parents in the \_rootDomNode tree.  
    /// (Spec Section 2.12)  
    /// \</summary\>  
    /// \<param name="schemaOnlyVm"\>The ViewModel representing the schema-only node.\</param\>  
    /// \<param name="targetSchemaNode"\>The schema for the node to create.\</param\>  
    /// \<param name="parentContextSchemaNode"\>The schema of the parent under which this node should be created (if known).\</param\>  
    /// \<returns\>The newly created (materialized) DomNode, or null if creation failed.\</returns\>  
    public DomNode? MaterializeSchemaNode(DataGridRowItemViewModel schemaOnlyVm, SchemaNode targetSchemaNode, SchemaNode? parentContextSchemaNode)  
    {  
        // This is a complex operation:  
        // 1\. Determine the path to the node to be created. This might involve walking up  
        //    the \`FlatItemsSource\` to find parent VMs that \*do\* have \`DomNode\`s, or by  
        //    storing parent \`DomNode\` references directly if schema-only nodes are nested under real ones.  
        //    Alternatively, the \`schemaOnlyVm\` could store enough context about its intended parent.  
        //  
        // 2\. Traverse from \`\_rootDomNode\` down this path.  
        // 3\. If intermediate parent \`ObjectNode\`s or \`ArrayNode\`s in the path do not exist,  
        //    create them using \`DomFactory.CreateDefaultFromSchema\` based on \*their\* respective schemas.  
        //    This means \`RebuildDomToSchemaMapping\` might need to be smart or you pre-fetch parent schemas.  
        //  
        // 4\. Once the immediate parent \`DomNode\` exists (or is created), create the target \`DomNode\`  
        //    using \`DomFactory.CreateDefaultFromSchema(schemaOnlyVm.NodeName, targetSchemaNode, parentDomNode)\`.  
        //  
        // 5\. Add the new \`DomNode\` to its parent's \`Children\` or \`Items\`.  
        //  
        // 6\. \*\*Crucially\*\*: After materializing, you need to update the \`\_domToSchemaMap\` for the new node(s).  
        //    Then, you might need to partially refresh \`FlatItemsSource\` or at least update the  
        //    \`schemaOnlyVm\` to now wrap the \`materializedDomNode\`.  
        //    Alternatively, the \`CommitEdit\` in \`DataGridRowItemViewModel\` could signal the \`MainViewModel\`  
        //    to replace the schema-only VM with a new VM wrapping the materialized \`DomNode\`.  
        //  
        // 7\. This operation should be undoable. The \`UndoRedoService\` should record the creation  
        //    of potentially multiple nodes.  
        //  
        // 8\. Mark document as dirty. (Spec Section 2.8, 2.12)

        // For simplicity in this skeleton, assume we can find the parent DomNode  
        // This is a placeholder for the complex logic described above.  
        // You'll need a robust way to get \`actualParentDomNode\` and \`nodeNameInParent\`.  
        DomNode? actualParentDomNode \= FindOrCreateParentDomPathFor(schemaOnlyVm); // This helper is complex  
        string nodeNameInParent \= schemaOnlyVm.NodeName; // This might need stripping of "\[ \]" for array indices

        if (actualParentDomNode \== null)  
        {  
            // Log error: could not determine/create parent path  
            return null;  
        }

        var newDomNode \= DomFactory.CreateDefaultFromSchema(nodeNameInParent, targetSchemaNode, actualParentDomNode);

        if (actualParentDomNode is ObjectNode on) {  
            on.Children\[nodeNameInParent\] \= newDomNode;  
        } else if (actualParentDomNode is ArrayNode an) {  
            // For arrays, inserting a schema-only node usually means adding to the end  
            // or at a specific conceptual index. This needs more thought for schema-only placeholders.  
            // Typically, the "Add item" placeholder handles array additions.  
            // Editing a schema-only \*property\* that \*is\* an array is different.  
            an.Items.Add(newDomNode); // Simplified  
        } else {  
            return null; // Cannot add to a ValueNode or RefNode parent  
        }

        // Update mapping and potentially refresh parts of the UI  
        \_domToSchemaMap\[newDomNode\] \= targetSchemaNode; // Or re-run full mapping if easier initially  
        // The schemaOnlyVm should now be updated or replaced.  
        // The caller (DataGridRowItemViewModel.CommitEdit) will update its own .DomNode reference.

        ParentViewModel.UndoRedoService.RecordOperation(new AddNodeOperation(actualParentDomNode, newDomNode, /\*index if array\*/));  
        ParentViewModel.MarkAsDirty();

        return newDomNode;  
    }

    private DomNode? FindOrCreateParentDomPathFor(DataGridRowItemViewModel vmToMaterialize) {  
        // Highly complex logic:  
        // 1\. Find the closest ancestor VM in FlatItemsSource that has a real DomNode.  
        // 2\. From that real DomNode, and the schema definitions of intermediate schema-only VMs,  
        //    construct the missing DomNode path down to the parent of vmToMaterialize.DomNode.  
        // This needs careful management of schema context and DOM creation.  
        // Consider storing the 'intended parent DomNode' or 'intended parent SchemaNode' and 'property name'  
        // directly in schema-only DataGridRowItemViewModels to simplify this.  
        return \_rootDomNode; // Gross oversimplification  
    }

    // When ShowDomAndSchemaView changes:  
    public bool ShowDomAndSchemaView  
    {  
        get \=\> \_showDomAndSchemaView;  
        set  
        {  
            if (\_showDomAndSchemaView \!= value)  
            {  
                \_showDomAndSchemaView \= value;  
                OnPropertyChanged();  
                RebuildFlatItemsSourceInternal(); // Rebuild list with or without schema-only nodes  
            }  
        }  
    }  
    private bool \_showDomAndSchemaView \= true; // Default as per spec for config file.

    // Existing OnExpansionChanged needs to call the new RebuildFlatItemsSourceInternal  
    // or be modified to handle both DOM and schema-only children based on ShowDomAndSchemaView.  
    public new void OnExpansionChanged(DataGridRowItemViewModel changedItem)  
    {  
        // The old logic added/removed children directly.  
        // A simpler way now might be to just call RebuildFlatItemsSourceInternal()  
        // which will respect the new expansion state of \`changedItem\`.  
        // This is less efficient for single expand/collapse but easier to get right initially  
        // with the combined view logic.  
        RebuildFlatItemsSourceInternal();  
    }  
}

**3\. XAML Updates:**

In your `DataGrid` `DataTemplate` for rows:

* Bind to `IsSchemaOnlyNode` to apply a different style (e.g., `Foreground="Gray"`, `FontStyle="Italic"`) for schema-only nodes.  
* The "Value" column `TextBlock` should now display `ValueDisplay` which handles showing default values for schema-only nodes.

**Key Implementation Challenges & Considerations:**

* **`RebuildFlatItemsSourceInternal` / `AddNodeAndPotentialSchemaChildrenToFlatList`:** This becomes the heart of the display logic. It needs to correctly:  
  * Iterate existing `DomNode`s.  
  * If `ShowDomAndSchemaView` is true, for each `ObjectNode` (that has a schema), iterate its `SchemaNode.Properties`. If a schema property doesn't have a corresponding `DomNode` child, create a schema-only `DataGridRowItemViewModel` for it.  
  * Respect the `IsExpanded` state for both `DomNode`\-backed VMs and schema-only VMs when deciding to recurse.  
* **`MaterializeSchemaNode` in `MainViewModel`:** This is critical. When a user edits a schema-only node, this method needs to:  
  * Figure out the correct path in the *actual* `_rootDomNode` tree where the new node(s) should live.  
  * Create any missing parent `ObjectNode`s/`ArrayNode`s along that path using their default schema values.  
  * Create the target `DomNode` itself using its schema default.  
  * Update the `_domToSchemaMap`.  
  * The `DataGridRowItemViewModel` that was schema-only needs to be updated to wrap the new, real `DomNode`.  
  * This entire materialization should be an undoable operation.  
* **State Management in `DataGridRowItemViewModel`:** It needs to clearly distinguish its state (wrapping a real `DomNode` vs. schema-only placeholder) and ensure its properties (`NodeName`, `ValueDisplay`, `IsEditable`, `Indentation`) reflect this correctly.  
* **Default Values for Display:** Converting `SchemaNode.DefaultValue` (which is `object?`) to a user-friendly string for `ValueDisplay` needs careful handling for various types.  
* **Performance:** Rebuilding the entire `FlatItemsSource` on every expansion/collapse can be slow for large documents. The previous, more surgical add/remove approach in `OnExpansionChanged` would be more performant but is harder to integrate initially with the combined schema view. You might start with full rebuilds and optimize later if needed.

This approach provides a clear visual distinction and allows users to seamlessly turn schema definitions into actual data by simply editing them.

# DOM tree validation

Okay, implementing DOM tree validation is a crucial step to ensure data integrity against the defined schemas. Here's how you can approach it, integrating with the structures we've discussed:

**Core Idea:**

Validation involves comparing each `DomNode` in your tree with its corresponding `SchemaNode` (if one exists). This process should be recursive for objects and arrays. A dedicated service can encapsulate the validation logic, and the results will update the `IsValid` and `ValidationErrorMessage` properties in your `DataGridRowItemViewModel`s, triggering UI feedback.

**1\. `ValidationResult` or `ValidationIssue` Class (`JsonConfigEditor.Core`)**

It's useful to have a class to represent the outcome of a validation check for a single node.

C\#  
// \--- File: JsonConfigEditor.Core/Validation/ValidationIssue.cs \---  
using JsonConfigEditor.Core.Dom;

namespace JsonConfigEditor.Core.Validation  
{  
    /// \<summary\>  
    /// Represents a single validation issue found for a DomNode.  
    /// \</summary\>  
    public class ValidationIssue  
    {  
        /// \<summary\>  
        /// Gets the DomNode to which this validation issue pertains.  
        /// \</summary\>  
        public DomNode Node { get; }

        /// \<summary\>  
        /// Gets the error message describing the validation failure.  
        /// \</summary\>  
        public string Message { get; }

        /// \<summary\>  
        /// Optionally, the severity of the issue (e.g., Error, Warning). For now, all are errors.  
        /// \</summary\>  
        // public ValidationSeverity Severity { get; }

        public ValidationIssue(DomNode node, string message)  
        {  
            Node \= node;  
            Message \= message;  
        }  
    }  
}

**2\. `ValidationService` (`JsonConfigEditor.Core`)**

This service will contain the core logic for validating `DomNode`s against `SchemaNode`s.

C\#  
// \--- File: JsonConfigEditor.Core/Validation/ValidationService.cs \---  
using JsonConfigEditor.Core.Dom;  
using JsonConfigEditor.Core.Schema;  
using System.Text.Json; // For JsonElement comparisons  
using System.Text.RegularExpressions; // For Regex

namespace JsonConfigEditor.Core.Validation  
{  
    /// \<summary\>  
    /// Provides services for validating DomNodes against their SchemaNodes.  
    /// (As per spec sections 2.4.1, 2.8, and schema constraints in 2.2)  
    /// \</summary\>  
    public class ValidationService  
    {  
        /// \<summary\>  
        /// Validates a single DomNode against its corresponding SchemaNode.  
        /// This method will be called recursively for children of ObjectNodes and ArrayNodes.  
        /// \</summary\>  
        /// \<param name="domNode"\>The DomNode to validate.\</param\>  
        /// \<param name="schemaNode"\>The SchemaNode defining the rules for the domNode. Can be null if domNode is unschematized.\</param\>  
        /// \<param name="issues"\>A list to which any found validation issues will be added.\</param\>  
        /// \<param name="domToSchemaMap"\>The complete map of DomNodes to SchemaNodes, used for validating children recursively.\</param\>  
        public void ValidateNodeRecursively(  
            DomNode domNode,  
            SchemaNode? schemaNode,  
            List\<ValidationIssue\> issues,  
            IReadOnlyDictionary\<DomNode, SchemaNode?\> domToSchemaMap)  
        {  
            // 1\. If schemaNode is null, the domNode is unschematized.  
            //    No schema validation applies beyond basic JSON well-formedness (handled by parser).  
            //    (Spec Section 2.2 \- Schema Fallback Behavior for unschematized nodes)  
            if (schemaNode \== null)  
            {  
                // If it's a RefNode, still validate path syntax even if overall unschematized.  
                if (domNode is RefNode rn)  
                {  
                    ValidateRefNodePath(rn, issues);  
                }  
                // Recursively call for children, they might hit a mounted schema deeper.  
                // Or, if a node is unschematized, its children are also considered unschematized unless a new schema mount point is hit.  
                // For now, if parent is unschematized, children are validated without schema context unless they map to a different root schema.  
                 if (domNode is ObjectNode on) {  
                    foreach (var child in on.Children.Values) {  
                        domToSchemaMap.TryGetValue(child, out var childSchema); // Check if child hits a new mount  
                        ValidateNodeRecursively(child, childSchema, issues, domToSchemaMap);  
                    }  
                } else if (domNode is ArrayNode an) {  
                    foreach (var item in an.Items) {  
                        domToSchemaMap.TryGetValue(item, out var itemSchema); // Check if item hits a new mount (less common for array items)  
                        ValidateNodeRecursively(item, itemSchema, issues, domToSchemaMap);  
                    }  
                }  
                return;  
            }

            // 2\. Handle RefNode specifically (Spec Section 2.1)  
            if (domNode is RefNode refNode)  
            {  
                ValidateRefNodePath(refNode, issues);  
                // The spec states: "Schema validation (type checking) for the RefNode itself  
                // should not mark it as invalid if the schema expects a different value type".  
                // The target of the reference will be validated independently if it's part of the DOM.  
                // No further validation of the RefNode itself against the schemaNode's type constraints.  
                return; // Stop further validation for the RefNode itself against this schemaNode  
            }

            // 3\. Type Compatibility (ValueNode vs SchemaNode.ClrType)  
            //    (Spec Section 2.4.1 \- Partial Validation / JSON Compatibility implies this)  
            //    This is a complex check. \`SchemaNode.ClrType\` vs \`ValueNode.Value.ValueKind\`.  
            if (domNode is ValueNode vn)  
            {  
                if (\!IsJsonElementCompatibleWithClrType(vn.Value, schemaNode.ClrType))  
                {  
                    issues.Add(new ValidationIssue(domNode, $"Type mismatch: DOM value kind '{vn.Value.ValueKind}' is not compatible with schema type '{schemaNode.ClrType.Name}'."));  
                    // Depending on strictness, you might stop further value-based validation here.  
                }  
                else  
                {  
                    // 4\. Value-based Schema Constraints (only if type is compatible and it's a ValueNode)  
                    //    (Spec Section 2.2 defines these constraints in SchemaNode)  
                    ValidateValueConstraints(vn, schemaNode, issues);  
                }  
            }

            // 5\. Structural Validation (ObjectNode and ArrayNode)  
            if (domNode is ObjectNode objectNode)  
            {  
                ValidateObjectNode(objectNode, schemaNode, issues, domToSchemaMap);  
            }  
            else if (domNode is ArrayNode arrayNode)  
            {  
                ValidateArrayNode(arrayNode, schemaNode, issues, domToSchemaMap);  
            }  
        }

        private void ValidateRefNodePath(RefNode refNode, List\<ValidationIssue\> issues)  
        {  
            // Basic path syntax check (e.g., not empty, starts with '/', no invalid chars)  
            // (Spec Section 2.1 \- "Only the link path syntax should be checked")  
            if (string.IsNullOrWhiteSpace(refNode.ReferencePath) || \!refNode.ReferencePath.StartsWith("/"))  
            {  
                issues.Add(new ValidationIssue(refNode, $"Invalid $ref path syntax: '{refNode.ReferencePath}'. Must start with '/'."));  
            }  
            // More advanced syntax checks can be added.  
        }

        private bool IsJsonElementCompatibleWithClrType(JsonElement jsonElement, Type clrType)  
        {  
            // Implementation:  
            // \- Check jsonElement.ValueKind against expected kinds for clrType.  
            // \- E.g., if clrType is int/double, ValueKind should be Number.  
            // \- If clrType is bool, ValueKind should be True/False.  
            // \- If clrType is string, ValueKind should be String (or Null if string is nullable in schema).  
            // \- If clrType is an enum, ValueKind should be String (for enum name) or Number (for underlying value, if supported).  
            // This can get quite detailed.  
            return true; // Placeholder  
        }

        private void ValidateValueConstraints(ValueNode valueNode, SchemaNode schemaNode, List\<ValidationIssue\> issues)  
        {  
            // Implementation:  
            // a. Min/Max: If schemaNode.Min or schemaNode.Max is set, parse valueNode.Value (if number) and compare.  
            //    Add issue if out of range.  
            // b. RegexPattern: If schemaNode.RegexPattern is set, check valueNode.Value.GetString() against it.  
            //    Add issue if no match.  
            // c. AllowedValues: If schemaNode.AllowedValues is set, check (case-insensitively) if  
            //    valueNode.Value.GetString() (or enum name) is in the list. Add issue if not.  
            //    (Spec Section 2.2)  
        }

        private void ValidateObjectNode(  
            ObjectNode objectNode,  
            SchemaNode schemaNode,  
            List\<ValidationIssue\> issues,  
            IReadOnlyDictionary\<DomNode, SchemaNode?\> domToSchemaMap)  
        {  
            // Implementation:  
            // a. Required Properties: Iterate schemaNode.Properties. If a schema property IsRequired  
            //    and not present in objectNode.Children, add an issue.  
            //    (This interacts with "addable" nodes \- if a required node is missing, it might be an "addable" placeholder  
            //     rather than an immediate validation error if the combined view is on.  
            //     For strict validation pass, it means the DOM \*must\* contain it.)  
            //     (Spec Section 2.12, 2.2)  
            //  
            // b. Unexpected Properties: If \!schemaNode.AllowAdditionalProperties, iterate objectNode.Children.  
            //    If a child's name is not in schemaNode.Properties, add an issue.  
            //    (Spec Section 2.15 \- Context Menu for closed objects, Clarification 7\)  
            //  
            // c. Recursively Validate Children: For each child DomNode in objectNode.Children:  
            //    \- Find its corresponding childSchemaNode (from schemaNode.Properties or schemaNode.AdditionalPropertiesSchema).  
            //    \- Call ValidateNodeRecursively(childDomNode, childSchemaNode, issues, domToSchemaMap).  
             if (schemaNode.Properties \!= null) {  
                foreach(var requiredProp in schemaNode.Properties.Where(p \=\> p.Value.IsRequired)) {  
                    if (\!objectNode.Children.ContainsKey(requiredProp.Key)) {  
                        issues.Add(new ValidationIssue(objectNode, $"Required property '{requiredProp.Key}' is missing."));  
                    }  
                }  
            }

            if (\!schemaNode.AllowAdditionalProperties && schemaNode.Properties \!= null) { // Only check if schema defines properties  
                foreach(var domChildPair in objectNode.Children) {  
                    if (\!schemaNode.Properties.ContainsKey(domChildPair.Key)) {  
                        issues.Add(new ValidationIssue(domChildPair.Value, $"Property '{domChildPair.Key}' is not allowed by the schema for object '{objectNode.Name}'."));  
                    }  
                }  
            }

            foreach (var childPair in objectNode.Children)  
            {  
                SchemaNode? childSchema \= schemaNode.Properties?.TryGetValue(childPair.Key, out var cs) \== true ? cs  
                                        : schemaNode.AdditionalPropertiesSchema;  
                ValidateNodeRecursively(childPair.Value, childSchema, issues, domToSchemaMap);  
            }  
        }

        private void ValidateArrayNode(  
            ArrayNode arrayNode,  
            SchemaNode schemaNode,  
            List\<ValidationIssue\> issues,  
            IReadOnlyDictionary\<DomNode, SchemaNode?\> domToSchemaMap)  
        {  
            // Implementation:  
            // a. Min/Max Items (Not explicitly in spec for arrays, but a common schema feature. If added to SchemaNode for arrays):  
            //    Check arrayNode.Items.Count against schema constraints.  
            //  
            // b. Recursively Validate Items: If schemaNode.ItemSchema is not null, for each item DomNode in arrayNode.Items:  
            //    Call ValidateNodeRecursively(itemDomNode, schemaNode.ItemSchema, issues, domToSchemaMap).  
            //    (Spec Section 2.11 \- Pasting into schematized arrays implies items are validated post-insertion).  
            if (schemaNode.ItemSchema \!= null)  
            {  
                foreach (var itemNode in arrayNode.Items)  
                {  
                    ValidateNodeRecursively(itemNode, schemaNode.ItemSchema, issues, domToSchemaMap);  
                }  
            }  
        }  
    }  
}

**3\. `MainViewModel` \- Orchestrating Full Document Validation:**

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
// ... (previous using statements and class structure) ...  
using JsonConfigEditor.Core.Validation;

public class MainViewModel : ViewModelBase  
{  
    private readonly ValidationService \_validationService;  
    // ...

    public MainViewModel(  
        IJsonDomParser jsonParser, /\*...,\*/ ValidationService validationService, /\*... other services \*/)  
    {  
        // ... initialize services ...  
        \_validationService \= validationService;  
    }

    /// \<summary\>  
    /// Validates the entire currently loaded DOM tree against the loaded schemas.  
    /// Updates IsValid and ValidationErrorMessage on all DataGridRowItemViewModels.  
    /// (Spec Section 2.8 \- Post-Load Validation, Pre-Save Validation)  
    /// \</summary\>  
    public List\<ValidationIssue\> ValidateFullDocument()  
    {  
        var allIssues \= new List\<ValidationIssue\>();  
        if (\_rootDomNode \== null)  
        {  
            // No document to validate, or clear previous validation states.  
            foreach (var vm in FlatItemsSource) { vm.SetValidationState(true, string.Empty); }  
            return allIssues;  
        }

        // Clear previous validation states on all VMs  
        // Create a temporary map of DomNode to its VM for quick updates.  
        var domNodeToVmMap \= FlatItemsSource.Where(vm \=\> vm.DomNode \!= null)  
                                           .DistinctBy(vm \=\> vm.DomNode) // Ensure DomNode is not null  
                                           .ToDictionary(vm \=\> vm.DomNode\!);

        // First, set all to valid, then mark specific ones as invalid.  
        foreach (var vm in domNodeToVmMap.Values)  
        {  
            vm.SetValidationState(true, string.Empty);  
        }  
        // For schema-only VMs, their validity is more about being addable; they don't represent "invalid data" yet.  
        // Unless a required schema-only node is missing and we are doing a "final" validation.

        // Perform recursive validation starting from the root  
        \_validationService.ValidateNodeRecursively(\_rootDomNode, \_domToSchemaMap.TryGetValue(\_rootDomNode, out var rootSchema) ? rootSchema : null, allIssues, \_domToSchemaMap);

        // Apply the found issues to the ViewModels  
        foreach (var issue in allIssues)  
        {  
            if (domNodeToVmMap.TryGetValue(issue.Node, out var vm))  
            {  
                vm.SetValidationState(false, issue.Message);  
            }  
            // If an issue is on a node not currently in FlatItemsSource (e.g., collapsed),  
            // the "Show Just Invalid Nodes" filter will rely on a separate list or re-validation.  
        }  
          
        // Update any global validation summary (e.g., count of errors in status bar)  
        UpdateStatusBar(allIssues.Any() ? $"Validation failed: {allIssues.Count} issue(s) found." : "Validation successful.", allIssues.Any());

        return allIssues;  
    }

    // LoadJsonAsync would call ValidateFullDocument() after parsing and mapping.  
    // SaveFileCommand would call ValidateFullDocument() before attempting to save.  
}

// \--- File: JsonConfigEditor.Wpf/ViewModels/DataGridRowItemViewModel.cs \---  
public class DataGridRowItemViewModel : ViewModelBase  
{  
    // ...  
    /// \<summary\>  
    /// Sets the validation state and message for this item.  
    /// Called by MainViewModel after full document validation or by CommitEdit for single item validation.  
    /// \</summary\>  
    public void SetValidationState(bool isValid, string message)  
    {  
        // Check if IsValid actual state changes to avoid unnecessary notifications  
        // if many issues apply to the same node (though ValidationService should probably provide one per node).  
        if (IsValid \!= isValid || ValidationErrorMessage \!= message) {  
            IsValid \= isValid; // Setter should call OnPropertyChanged  
            ValidationErrorMessage \= message; // Setter should call OnPropertyChanged  
        }  
    }  
    // ...  
}

**4\. Integrating Validation into `DataGridRowItemViewModel.CommitEdit()`:**

The `CommitEdit` method already has placeholders for validation. You would now call `_validationService.ValidateNodeRecursively` (or a non-recursive version `ValidateSingleNodeValue`) for the specific `DomNode` being edited.

C\#  
// \--- In JsonConfigEditor.Wpf/ViewModels/DataGridRowItemViewModel.cs \---  
public class DataGridRowItemViewModel // ...  
{  
    public bool CommitEdit()  
    {  
        // ... (initial checks, parsing EditValue to a temporary new JsonElement or value) ...  
        // Assume \`parsedValueOrElement\` holds the successfully parsed input.

        // Validation Phase (after parsing EditValue)  
        var issues \= new List\<ValidationIssue\>();  
        if (DomNode \!= null && AssociatedSchemaNode \!= null) // DomNode should be non-null if materialized  
        {  
            // Temporarily update the DomNode's value to validate the new state  
            // This is tricky if DomNode.Value is JsonElement (immutable).  
            // You might need to create a temporary ValueNode with the new value for validation.  
            // Or, the ValidationService needs a method to validate a \*potential\* value against a schema.

            // For simplicity here, assume ValidationService can take current DomNode, its schema, and the NEW proposed value.  
            // ParentViewModel.ValidationService.ValidateProposedValue(DomNode, AssociatedSchemaNode, \_editValue, issues);  
            // This is a conceptual simplification. More likely, you construct a temporary state or the ValidationService  
            // has methods tailored to specific constraints.

            // Let's refine: After successful parsing of \_editValue to a new JsonElement \`newJsonValue\`:  
            if (DomNode is ValueNode vn) // And parsing \_editValue was successful into newJsonValue  
            {  
                // Validate the newJsonValue against AssociatedSchemaNode constraints  
                // ParentViewModel.ValidationService.ValidateValueConstraints(newJsonValue, AssociatedSchemaNode, issues);

                // Example direct check (subset of what ValidationService would do)  
                if (AssociatedSchemaNode \!= null)  
                {  
                    // Type check (already partially done by parsing \_editValue to newJsonValue's type)  
                    // Min/Max  
                    if (AssociatedSchemaNode.Min.HasValue && newJsonValue.TryGetDouble(out double dValMin) && dValMin \< AssociatedSchemaNode.Min.Value)  
                        issues.Add(new ValidationIssue(DomNode, $"Value must be \>= {AssociatedSchemaNode.Min.Value}."));  
                    // ... other constraint checks ...  
                }  
            }  
            else if (DomNode is RefNode rn)  
            {  
                // ParentViewModel.ValidationService.ValidateRefNodePathSyntax(\_editValue, issues);  
            }  
        }

        if (issues.Any())  
        {  
            SetValidationState(false, string.Join("; ", issues.Select(i \=\> i.Message)));  
            // Spec: Partial Validation Success (2.4.1) \- if JSON compatible, save it even if schema invalid.  
            // So, if parsing \_editValue to newJsonValue succeeded, we update DomNode here.  
        }  
        else  
        {  
            SetValidationState(true, string.Empty);  
        }

        // Update DomNode if parsing \_editValue was successful, regardless of schema validation outcome (as per partial success)  
        if (/\* parsing \_editValue to newJsonValue was successful \*/ DomNode is ValueNode vnToUpdate && /\* newJsonValue exists \*/ true)  
        {  
            // vnToUpdate.Value \= newJsonValue; // This line needs the actual new JsonElement  
            // ParentViewModel.MarkAsDirty();  
            // ParentViewModel.UndoRedoService.RecordOperation(...);  
        }  
        else if (/\* parsing \_editValue for RefNode path was successful \*/ DomNode is RefNode rnToUpdate)  
        {  
            // rnToUpdate.ReferencePath \= \_editValue;  
            // ParentViewModel.MarkAsDirty();  
            // ParentViewModel.UndoRedoService.RecordOperation(...);  
        }

        IsInEditMode \= false;  
        // Refresh ValueDisplay as DomNode might have changed or its value updated  
        OnPropertyChanged(nameof(ValueDisplay));  
        return IsValid; // Or return true if partially saved and only schema validation failed.  
    }  
}

**Key Points for Implementation:**

* **Recursive Nature:** Validation of objects and arrays inherently requires recursion.  
* **Schema Mapping:** Accurate mapping from `DomNode` to `SchemaNode` (from `_domToSchemaMap`) is critical.  
* **State Update:** After validation, `IsValid` and `ValidationErrorMessage` on `DataGridRowItemViewModel`s must be updated to reflect in the UI.  
* **Performance:** For very large documents, validating the entire tree can be intensive. Consider if background validation with incremental updates is needed (beyond current spec, but good to keep in mind). The current spec implies full validation on load/pre-save.  
* **"Show Just Invalid Nodes" Filter:** This filter (Spec Section 2.5) will use the `IsValid` flag on the ViewModels. When this filter is active, `MainViewModel.RefreshDisplayList()` will need to consider this flag to include only invalid nodes and their ancestors.

This structure provides a comprehensive way to handle validation throughout the DOM tree according to the defined schemas.

# Full text search over combined dom+schema nodes

Okay, implementing a "fulltext search" (which we've clarified as extending the current **search by name** to the combined DOM \+ schema tree) requires careful consideration of how to identify and navigate to both existing DOM nodes and schema-only (addable) nodes.

Here's how you could approach implementing this, focusing on modifications to the `MainViewModel` and `DataGridRowItemViewModel`:

**Core Idea:**

The search should conceptually traverse the entire combined tree (actual DOM nodes \+ potential schema-only nodes). When a match is found, the UI must update to make that node visible (activating "DOM \+ Schema View" if necessary, expanding parents) and then highlight and select it.

**1\. `DataGridRowItemViewModel` Enhancements (Minor for Search):**

* **`IsHighlightedInSearch` (bool property):** This existing property (from the previous search outline) will be used for both DOM-present and schema-only nodes.  
  * Ensure its setter calls `OnPropertyChanged`.  
* **Expose Path Information (Potentially):** For easier matching during the reveal process, having a readily available canonical path string within the VM could be helpful, though the `MainViewModel` can also construct this.

**2\. `MainViewModel` \- Search Logic Modifications:**

The `ExecuteFind` method (and any preparatory search result gathering) needs to be significantly updated.

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
// ... (previous using statements and class structure) ...

public class MainViewModel : ViewModelBase  
{  
    // ... (existing properties: \_searchText, \_searchResults (type might change), \_currentSearchIndex) ...

    // To store comprehensive search results from the combined tree  
    private List\<SearchResultItem\> \_comprehensiveSearchResults \= new List\<SearchResultItem\>();

    // Represents an item found during the comprehensive search  
    private class SearchResultItem  
    {  
        public string NodeName { get; }  
        public string NodePath { get; } // Full path to the node, e.g., "root/obj1/propA"  
        public bool IsDomPresent { get; }  
        public DomNode? DomNode { get; } // Null if schema-only  
        public SchemaNode SchemaNode { get; } // The schema defining this node

        public DataGridRowItemViewModel? ViewModelCache { get; set; } // Cache the VM once found in FlatItemsSource

        public SearchResultItem(string name, string path, bool isDomPresent, DomNode? domNode, SchemaNode schemaNode)  
        {  
            NodeName \= name;  
            NodePath \= path;  
            IsDomPresent \= isDomPresent;  
            DomNode \= domNode;  
            SchemaNode \= schemaNode;  
        }  
    }

    // Modify ExecuteFind to use the comprehensive search results  
    private async Task ExecuteFind(bool findNext) // Made async for potential UI updates during reveal  
    {  
        // 0\. Clear previous highlights from FlatItemsSource  
        foreach (var vm in FlatItemsSource) { vm.IsHighlightedInSearch \= false; }

        if (string.IsNullOrWhiteSpace(SearchText))  
        {  
            \_comprehensiveSearchResults.Clear();  
            \_currentSearchIndex \= \-1;  
            UpdateStatusBar("Search text cleared.", false);  
            return;  
        }

        // 1\. Re-gather search results if SearchText changed or first search  
        if (\!\_comprehensiveSearchResults.Any() ||  
            \_comprehensiveSearchResults.FirstOrDefault()?.NodeName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) \== \-1) // Heuristic: check if current results are for this text  
        {  
            BuildComprehensiveSearchResults();  
            \_currentSearchIndex \= \-1;  
        }

        if (\!\_comprehensiveSearchResults.Any())  
        {  
            UpdateStatusBar($"No results found for '{SearchText}'.", false);  
            return;  
        }

        // 2\. Update \_currentSearchIndex  
        if (findNext) \_currentSearchIndex++;  
        else \_currentSearchIndex--;

        if (\_currentSearchIndex \>= \_comprehensiveSearchResults.Count) \_currentSearchIndex \= 0;  
        if (\_currentSearchIndex \< 0\) \_currentSearchIndex \= \_comprehensiveSearchResults.Count \- 1;

        // 3\. Get the target search result  
        SearchResultItem targetResult \= \_comprehensiveSearchResults\[\_currentSearchIndex\];

        // 4\. Reveal and Select the node (this is the complex part)  
        DataGridRowItemViewModel? targetVm \= await RevealAndFindViewModel(targetResult);

        if (targetVm \!= null)  
        {  
            targetVm.IsHighlightedInSearch \= true;  
            SelectedRowItem \= targetVm; // Assuming DataGrid selection follows this

            // Ensure item is scrolled into view (requires DataGrid reference or eventing)  
            RequestScrollToItem(targetVm);  
            UpdateStatusBar($"Found '{targetResult.NodeName}' ({\_currentSearchIndex \+ 1}/{\_comprehensiveSearchResults.Count})", false);  
        }  
        else  
        {  
            UpdateStatusBar($"Could not display result for '{targetResult.NodeName}'. It might be in a non-expandable schema-only section.", true);  
        }  
    }

    private void BuildComprehensiveSearchResults()  
    {  
        \_comprehensiveSearchResults.Clear();  
        if (\_rootDomNode \== null && (\!ShowDomAndSchemaView || \_schemaLoader.RootSchemas.TryGetValue("", out var rootSchema) \== false))  
        {  
            return; // Nothing to search  
        }

        // Start traversal from the root.  
        // If DOM exists, traverse it and its schema.  
        // If only schema exists (for root "" path) and ShowDomAndSchemaView, traverse that.  
        SchemaNode? rootSchemaContext \= null;  
        if (\_rootDomNode \!= null) {  
            \_domToSchemaMap.TryGetValue(\_rootDomNode, out rootSchemaContext);  
            AddSearchableNodesRecursive(\_rootDomNode, rootSchemaContext, GetDomNodePath(\_rootDomNode) ?? "");  
        } else if (ShowDomAndSchemaView && \_schemaLoader.RootSchemas.TryGetValue("", out rootSchemaContext)) {  
            // No DOM root, but we have a schema root.  
            AddSearchableSchemaOnlyNodesRecursive(rootSchemaContext, rootSchemaContext.Name ?? "$root\_schema", "", null);  
        }  
    }

    // Recursive helper for DOM-present nodes and their potential schema-only siblings  
    private void AddSearchableNodesRecursive(DomNode domNode, SchemaNode? schemaForDomNode, string currentPath)  
    {  
        // Add the DomNode itself if its name matches  
        if (domNode.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) \>= 0\)  
        {  
            // Ensure schemaForDomNode is not null, if it's null, this node is unschematized effectively for this search.  
            // However, every node in combined view implies a schema context.  
            SchemaNode actualSchema \= schemaForDomNode ?? FindSchemaForDomNode(domNode) ?? new SchemaNode(domNode.Name, domNode.GetType(), false, false, null, null, null, null, null, false, null, null, true, null); // Fallback dummy schema  
            \_comprehensiveSearchResults.Add(new SearchResultItem(domNode.Name, currentPath, true, domNode, actualSchema));  
        }

        if (domNode is ObjectNode objectNode && schemaForDomNode \!= null)  
        {  
            var processedSchemaProps \= new HashSet\<string\>();  
            foreach (var childDomPair in objectNode.Children)  
            {  
                string childPath \= $"{currentPath}/{childDomPair.Key}";  
                SchemaNode? childSchema \= schemaForDomNode.Properties?.TryGetValue(childDomPair.Key, out var cs) \== true ? cs  
                                        : schemaForDomNode.AdditionalPropertiesSchema;  
                AddSearchableNodesRecursive(childDomPair.Value, childSchema, childPath);  
                if (schemaForDomNode.Properties?.ContainsKey(childDomPair.Key) \== true)  
                {  
                    processedSchemaProps.Add(childDomPair.Key);  
                }  
            }  
            // Add schema-only properties for this object node  
            if (schemaForDomNode.Properties \!= null)  
            {  
                foreach (var schemaPropPair in schemaForDomNode.Properties)  
                {  
                    if (\!processedSchemaProps.Contains(schemaPropPair.Key))  
                    {  
                        string schemaOnlyPath \= $"{currentPath}/{schemaPropPair.Key}";  
                        if (schemaPropPair.Key.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) \>= 0\)  
                        {  
                            \_comprehensiveSearchResults.Add(new SearchResultItem(schemaPropPair.Key, schemaOnlyPath, false, null, schemaPropPair.Value));  
                        }  
                        // Recursively add children of this schema-only object if it's an object/array itself  
                         if (schemaPropPair.Value.NodeType \== SchemaNodeType.Object || schemaPropPair.Value.NodeType \== SchemaNodeType.Array) {  
                            AddSearchableSchemaOnlyNodesRecursive(schemaPropPair.Value, schemaPropPair.Key, schemaOnlyPath, schemaForDomNode);  
                         }  
                    }  
                }  
            }  
        }  
        else if (domNode is ArrayNode arrayNode && schemaForDomNode?.ItemSchema \!= null)  
        {  
            SchemaNode itemSchema \= schemaForDomNode.ItemSchema;  
            for (int i \= 0; i \< arrayNode.Items.Count; i++)  
            {  
                string childPath \= $"{currentPath}/{i}";  
                // Array items typically don't match by "Name" unless their \*own\* name property is searched if they are objects.  
                // For now, we assume search is on property names or explicit item schema names.  
                // If array items can be directly named and searched, this needs adjustment.  
                AddSearchableNodesRecursive(arrayNode.Items\[i\], itemSchema, childPath);  
            }  
        }  
    }

    // Recursive helper specifically for branches that are purely schema-only  
    private void AddSearchableSchemaOnlyNodesRecursive(SchemaNode schemaNode, string nodeName, string currentPath, SchemaNode? parentSchema)  
    {  
        if (nodeName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) \>= 0\)  
        {  
             // Check if this schema-only node already added (e.g. as a missing property of an existing DOM object)  
            if (\!\_comprehensiveSearchResults.Any(sr \=\> sr.NodePath \== currentPath && \!sr.IsDomPresent)) {  
                \_comprehensiveSearchResults.Add(new SearchResultItem(nodeName, currentPath, false, null, schemaNode));  
            }  
        }

        if (schemaNode.NodeType \== SchemaNodeType.Object && schemaNode.Properties \!= null)  
        {  
            foreach (var schemaPropPair in schemaNode.Properties)  
            {  
                string childPath \= $"{currentPath}/{schemaPropPair.Key}";  
                AddSearchableSchemaOnlyNodesRecursive(schemaPropPair.Value, schemaPropPair.Key, childPath, schemaNode);  
            }  
        }  
        else if (schemaNode.NodeType \== SchemaNodeType.Array && schemaNode.ItemSchema \!= null)  
        {  
            // For schema-only arrays, we typically don't list N items.  
            // We search the array node itself by name, or if its ItemSchema has a name-like property, that's more complex.  
            // For now, we only search the array container name.  
        }  
    }

    private async Task\<DataGridRowItemViewModel?\> RevealAndFindViewModel(SearchResultItem searchResult)  
    {  
        // 1\. Ensure "DOM \+ Schema View" is active if the target is schema-only  
        if (\!searchResult.IsDomPresent && \!ShowDomAndSchemaView)  
        {  
            ShowDomAndSchemaView \= true; // This will trigger RefreshDisplayList  
            // Wait for UI to update if RefreshDisplayList is async or dispatches  
            await Task.Delay(50); // Small delay, ideally use a more robust synchronization  
        }

        // 2\. Expand all parent nodes in the path  
        string\[\] pathSegments \= searchResult.NodePath.Split('/');  
        string currentPathSegment \= "";  
        DataGridRowItemViewModel? lastParentVm \= null;

        for (int i \= 0; i \< pathSegments.Length \-1; i++) // Expand up to the parent of the target  
        {  
            currentPathSegment \= string.IsNullOrEmpty(currentPathSegment) ? pathSegments\[i\] : $"{currentPathSegment}/{pathSegments\[i\]}";  
            // Find VM for currentPathSegment in FlatItemsSource  
            var parentVm \= FlatItemsSource.FirstOrDefault(vm \=\> GetPathForViewModel(vm) \== currentPathSegment);  
            if (parentVm \!= null)  
            {  
                if (\!parentVm.IsExpanded && parentVm.IsExpandable)  
                {  
                    parentVm.IsExpanded \= true; // This will trigger OnExpansionChanged \-\> RefreshDisplayList  
                    // Wait for UI to update  
                    await Task.Delay(50); // Again, ideally more robust sync  
                }  
                lastParentVm \= parentVm;  
            }  
            else  
            {  
                 // Parent VM not found, something is wrong or path is incorrect for current FlatItemsSource state  
                 // This can happen if RefreshDisplayList hasn't fully caught up or if the path is for a node  
                 // that cannot be currently displayed (e.g. schema-only child of a schema-only array not showing items)  
                 return null;  
            }  
        }

        // 3\. Find the actual target ViewModel  
        // It should now be present in FlatItemsSource after expansions  
        var targetVm \= FlatItemsSource.FirstOrDefault(vm \=\> GetPathForViewModel(vm) \== searchResult.NodePath &&  
                                                            ((vm.DomNode \== searchResult.DomNode && searchResult.IsDomPresent) ||  
                                                             (\!searchResult.IsDomPresent && vm.IsSchemaOnlyNode && vm.SchemaContextNode \== searchResult.SchemaNode && vm.NodeName.Contains(searchResult.NodeName)))); // More precise match for schema-only

        // Cache it for quicker re-finding if user cycles through same results  
        searchResult.ViewModelCache \= targetVm;  
        return targetVm;  
    }

    // Helper to get a canonical path for a ViewModel (must be consistent with searchResult.NodePath)  
    private string GetPathForViewModel(DataGridRowItemViewModel vm)  
    {  
        if (vm.DomNode \!= null) return GetDomNodePath(vm.DomNode) ?? ""; // Use existing helper

        // For schema-only nodes, reconstruct path based on their position relative to DOM-present ancestors or schema root.  
        // This requires schema-only VMs to store enough context (e.g., their intended name, parent schema context).  
        // This part is complex and was simplified in MaterializeSchemaNode.  
        // For search, it's crucial to have a consistent path representation.  
        // Let's assume for schema-only VMs created by AddNodeAndPotentialSchemaChildrenToListRecursive,  
        // their name is set correctly and we can find their parent VM to build the path.  
        List\<string\> segments \= new List\<string\>();  
        DataGridRowItemViewModel? currentVm \= vm;  
        while(currentVm \!= null)  
        {  
            segments.Add(currentVm.NodeName.Replace("\[","").Replace("\]","")); // Strip array brackets for path segments  
            int currentIndex \= FlatItemsSource.IndexOf(currentVm);  
            if (currentIndex \<= 0\) break;  
            int currentDepth \= currentVm.Indentation.Left / 15;  
            DataGridRowItemViewModel? parentCandidate \= null;  
            for (int i \= currentIndex \-1; i \>=0; i--) {  
                var prevVm \= FlatItemsSource\[i\];  
                if ((prevVm.Indentation.Left / 15\) \< currentDepth) {  
                    parentCandidate \= prevVm;  
                    break;  
                }  
            }  
            currentVm \= parentCandidate;  
        }  
        segments.Reverse();  
        return string.Join("/", segments.Where(s \=\> s \!= "(Root)" && s \!= "$root\_schema")); // Filter out root placeholders  
    }

    // In your MainViewModel, you'll need a way to ask the DataGrid to scroll an item into view.  
    // This usually involves an event or a shared service if the DataGrid is in a separate View.  
    public event Action\<DataGridRowItemViewModel\>? RequestScrollToItemEvent;  
    private void RequestScrollToItem(DataGridRowItemViewModel item) \=\> RequestScrollToItemEvent?.Invoke(item);

}

**3\. UI (XAML) and `DataGridRowItemViewModel` for Highlighting:**

**`DataGridRowItemViewModel.IsHighlightedInSearch`**: This boolean property, when true, should trigger a style change in the `DataGrid` row or relevant cells (e.g., background color).  
 XML  
\<DataTrigger Binding="{Binding IsHighlightedInSearch}" Value="True"\>  
    \<Setter Property="Background" Value="Yellow"/\> \</DataTrigger\>

* 

**Key Implementation Steps & Challenges:**

1. **`BuildComprehensiveSearchResults()` Logic:**  
   * This is the core. It needs to recursively traverse:  
     * The actual `_rootDomNode` tree.  
     * For each `ObjectNode` encountered that has an `AssociatedSchemaNode`, iterate through `schemaNode.Properties`. If a schema property is *not* found in the `DomNode.Children`, it's a potential schema-only search result.  
     * It must correctly construct canonical paths for both DOM and schema-only nodes to be stored in `SearchResultItem.NodePath`.  
2. **`RevealAndFindViewModel()` Logic:**  
   * **Activating View:** If a `SearchResultItem` is schema-only (`IsDomPresent == false`), ensure `ShowDomAndSchemaView = true`. This will trigger `RefreshDisplayList()`.  
   * **Expanding Parents:** This is the trickiest part. You need to take the `NodePath` from `SearchResultItem`, segment it, and iteratively find the `DataGridRowItemViewModel` for each parent segment in the `FlatItemsSource`. If a parent VM is found and is expandable but not expanded, set `IsExpanded = true`. This will trigger `MainViewModel.OnExpansionChanged()`, which in turn calls `RefreshDisplayList()`.  
   * **Synchronization after Expansions:** Because expansions can modify `FlatItemsSource`, performing multiple expansions to reveal a deeply nested node requires careful handling. The `await Task.Delay(50);` is a crude way to wait for UI updates; a more robust method would involve waiting for `FlatItemsSource` to settle or using a state machine.  
   * **Finding the Final VM:** After all parents are expanded, the `DataGridRowItemViewModel` for the `SearchResultItem` should exist in `FlatItemsSource`. Locate it based on its path and identity (`DomNode` or `SchemaNode`).  
3. **Path Consistency:** The `GetPathForViewModel` helper must generate paths in the exact same format as those stored in `SearchResultItem.NodePath`. This is vital for matching.  
4. **Performance:** `BuildComprehensiveSearchResults()` could be slow for very large DOMs/schemas. Consider if it needs to be cancellable or run on a background thread (with results dispatched to UI thread). The current search is on-demand in `ExecuteFind`.  
5. **"Add item" Placeholders:** The spec mentions these for arrays. They are not typically "named" in a way that this search would find them unless they are given a specific, searchable name or the search is extended to "types" of nodes. The current search focuses on named properties.

This extension of search to the combined tree is significantly more complex than searching just the existing DOM, mainly due to the need to potentially modify the UI state (activating views, expanding nodes) to reveal schema-only results. Start by focusing on correctly building the `_comprehensiveSearchResults` list.

# Editing a schema only node, including materialization of missing dom nodes and instantiation of default values

Okay, implementing the editing of a schema-only node, which includes its materialization in the DOM tree with default values, is a cornerstone of the editor's schema-aware functionality. It bridges the gap between what the schema defines and what actually exists in the user's JSON document.

Here's a breakdown of how you would implement this, focusing on the interplay between `DataGridRowItemViewModel`, `MainViewModel`, and `DomFactory`, based on our specification:

**Core Workflow:**

1. User identifies a "schema-only" node in the `DataGrid` (visually distinct, e.g., grayed out).  
2. User initiates an edit on this schema-only node (e.g., double-clicks its value cell or presses Enter).  
3. The `DataGridRowItemViewModel` for this node enters edit mode. Its `EditValue` is initially populated with a string representation of its `SchemaContextNode.DefaultValue`.  
4. User modifies `EditValue` and confirms the edit (e.g., presses Enter or focus is lost).  
5. `DataGridRowItemViewModel.CommitEdit()` is called. This method detects it's a schema-only node and triggers the materialization process.  
6. The `MainViewModel` orchestrates the creation of the actual `DomNode` (and any necessary parent `DomNode`s that were also schema-only) in the main DOM tree using default values from the schema.  
7. Once the `DomNode` is materialized, the user's entered `EditValue` is parsed and applied to this new `DomNode`.  
8. The `DataGridRowItemViewModel` is updated to wrap the new, real `DomNode`, losing its "schema-only" status.  
9. The entire operation (node materialization \+ value change) is recorded for undo/redo.  
10. The document is marked as dirty.

**1\. `DataGridRowItemViewModel` \- Initiating Materialization:**

The `CommitEdit()` method is the key starting point.

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/DataGridRowItemViewModel.cs \---  
// ... (assuming other properties like DomNode, SchemaContextNode, IsSchemaOnlyNode, EditValue, ParentViewModel exist) ...

public class DataGridRowItemViewModel : ViewModelBase  
{  
    // ...

    /// \<summary\>  
    /// Commits the edited value. If this VM represents a schema-only node,  
    /// it will first trigger its materialization in the actual DOM tree.  
    /// (From specification document, Section 2.4, 2.12)  
    /// \</summary\>  
    public bool CommitEdit()  
    {  
        if (\!IsInEditMode) return true; // Nothing to commit if not in edit mode

        if (\!IsEditable && IsDomNodePresent) // Trying to edit a non-editable existing DOM node  
        {  
            CancelEdit(); // Revert UI state  
            return false;  
        }

        DomNode? nodeToUpdate \= this.DomNode; // Might be null if schema-only

        if (IsSchemaOnlyNode)  
        {  
            // Materialize the DomNode first  
            // The MaterializeDomNodeForSchema method needs enough context to find the correct parent  
            // DomNode in the actual tree and the name/index this new node should have.  
            // This might involve passing this VM, its SchemaContextNode, its intended name, and its parent's VM/SchemaNode.  
            nodeToUpdate \= ParentViewModel.MaterializeDomNodeForSchema(this);

            if (nodeToUpdate \== null)  
            {  
                // Materialization failed. The MainViewModel should have set a status bar message.  
                IsValid \= false; // Mark this VM as invalid for now as the operation failed  
                ValidationErrorMessage \= "Failed to create the node in the DOM.";  
                IsInEditMode \= false; // Exit edit mode  
                return false;  
            }  
            // Successfully materialized, update this VM to wrap the new DomNode  
            this.DomNode \= nodeToUpdate; // This makes IsSchemaOnlyNode return false  
            // Important: The underlying DomNode now exists and is initialized with schema defaults.  
            // The user's EditValue will now be applied to this newly created node.  
        }

        // \--- At this point, nodeToUpdate (this.DomNode) is guaranteed to be non-null \---  
        // \--- and represents an actual node in the DOM tree.                     \---

        // 1\. Parse the user's EditValue according to SchemaContextNode.ClrType or DomNode type  
        //    (This parsing logic is complex and was outlined in Milestone 3\)  
        JsonElement newJsonValue; // Placeholder for parsed value  
        object? parsedClrValue;   // Placeholder for CLR typed parsed value

        try  
        {  
            // Example parsing (highly simplified):  
            if (nodeToUpdate is RefNode) {  
                // EditValue is the new path, specific validation for path syntax  
                // (this was partially outlined in ValidationService)  
            } else {  
                // For ValueNode, parse EditValue to a JsonElement based on SchemaContextNode.ClrType  
                // For example:  
                // if (SchemaContextNode.ClrType \== typeof(int)) {  
                //     int val \= int.Parse(EditValue);  
                //     newJsonValue \= JsonDocument.Parse(val.ToString()).RootElement.Clone();  
                //     parsedClrValue \= val;  
                // } else if (SchemaContextNode.ClrType \== typeof(string)) {  
                //     newJsonValue \= JsonDocument.Parse($"\\"{JsonEncodedText.Encode(EditValue)}\\"").RootElement.Clone();  
                //     parsedClrValue \= EditValue;  
                // } // ... and so on for other types (bool, double, etc.)  
                // This parsing needs to be robust.  
                // For now, let's assume \`TryParseEditValue\` handles this and populates \`newJsonValue\` and \`parsedClrValue\`.  
                if (\!TryParseEditValue(EditValue, SchemaContextNode.ClrType, out newJsonValue, out parsedClrValue))  
                {  
                    SetValidationState(false, $"Invalid format for type {SchemaContextNode.ClrType.Name}.");  
                    // As per spec 2.4.1, if not convertible to JsonValueKind, focus returns to editor.  
                    // This implies CommitEdit might not fully exit IsInEditMode here.  
                    // For simplicity now, we'll exit edit mode.  
                    IsInEditMode \= false;  
                    return false;  
                }  
            }  
        }  
        catch (FormatException ex)  
        {  
            SetValidationState(false, $"Format error: {ex.Message}");  
            IsInEditMode \= false;  
            return false;  
        }

        // 2\. Validate the parsed value against SchemaContextNode constraints (Min, Max, Regex, AllowedValues)  
        //    (This logic is also complex and was outlined for ValidationService / Milestone 3\)  
        var issues \= new List\<Validation.ValidationIssue\>();  
        // ParentViewModel.ValidationService.ValidateProposedValue(nodeToUpdate, SchemaContextNode, parsedClrValue, issues);  
        // For now, simplified:  
        if (AssociatedSchemaNode \!= null)  
        {  
             // Call a refined validation method using parsedClrValue or newJsonValue  
             // Example: CheckMinMax(parsedClrValue, AssociatedSchemaNode, issues);  
             // CheckRegex(parsedClrValue as string, AssociatedSchemaNode, issues);  
             // CheckAllowedValues(parsedClrValue, AssociatedSchemaNode, issues);  
        }

        bool wasPreviouslyValid \= IsValid;  
        string previousValidationMessage \= ValidationErrorMessage;  
        JsonElement? oldValue \= (nodeToUpdate as ValueNode)?.Value; // For undo

        if (issues.Any())  
        {  
            SetValidationState(false, string.Join("; ", issues.Select(i \=\> i.Message)));  
            // Partial Save: If EditValue was parsable into newJsonValue, update DomNode even if schema validation fails  
        }  
        else  
        {  
            SetValidationState(true, string.Empty);  
        }

        // 3\. Update DomNode's value (if parsing was successful)  
        if (nodeToUpdate is ValueNode vn)  
        {  
            if (vn.Value.ValueKind \!= newJsonValue.ValueKind || vn.Value.ToString() \!= newJsonValue.ToString()) // Check if value actually changed  
            {  
                var originalJsonElement \= vn.Value; // For undo  
                vn.Value \= newJsonValue; // Update the DOM  
                ParentViewModel.UndoRedoService.RecordOperation(new ChangeValueOperation(vn, originalJsonElement, newJsonValue, this));  
                ParentViewModel.MarkAsDirty();  
            }  
        }  
        else if (nodeToUpdate is RefNode rn)  
        {  
            if (rn.ReferencePath \!= EditValue) // EditValue here is the path for RefNode  
            {  
                var originalPath \= rn.ReferencePath;  
                var originalJsonRef \= rn.OriginalValue;  
                rn.ReferencePath \= EditValue;  
                // The OriginalValue for RefNode (the {"$ref":..} object) also needs to be updated if we want to serialize it.  
                // This requires creating a new JsonElement for the $ref object.  
                // For simplicity, assume RefNode.OriginalValue is reconstructed on save or not directly mutated here post-creation.  
                ParentViewModel.UndoRedoService.RecordOperation(new ChangeRefPathOperation(rn, originalPath, EditValue, originalJsonRef, this));  
                ParentViewModel.MarkAsDirty();  
            }  
        }

        IsInEditMode \= false;  
        OnPropertyChanged(nameof(ValueDisplay)); // Reflects new value or default  
        OnPropertyChanged(nameof(IsSchemaOnlyNode)); // Will now be false  
        // If IsValid changed, UI will update via its binding.  
        return IsValid || (\!issues.Any() && wasPreviouslyValid); // Return true if valid OR if it was valid and parsing succeeded but schema checks failed (partial save)  
    }

    // Helper for parsing, to be fleshed out  
    private bool TryParseEditValue(string textValue, Type targetClrType, out JsonElement resultElement, out object? resultClrValue)  
    {  
        // Complex implementation:  
        // Handle string, int, double, bool, enum parsing based on targetClrType.  
        // Convert to JsonElement and also return the CLR typed value for easier validation.  
        resultElement \= default;  
        resultClrValue \= null;  
        // ...  
        return false; // Placeholder  
    }  
    // ...  
}

**2\. `MainViewModel` \- Materializing the DOM Node and its Path:**

This is where the actual DOM manipulation happens.

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
// ... (assuming other properties like \_rootDomNode, \_domToSchemaMap, \_schemaLoader, \_undoRedoService exist) ...

public class MainViewModel : ViewModelBase  
{  
    // ...

    /// \<summary\>  
    /// Creates the actual DomNode (and any necessary parents) in the \_rootDomNode tree  
    /// for a DataGridRowItemViewModel that was previously representing a schema-only node.  
    /// This is called from DataGridRowItemViewModel.CommitEdit() when a schema-only node is edited.  
    /// (From specification document, Section 2.12, "Handling Missing DOM Tree Nodes")  
    /// \</summary\>  
    /// \<param name="vmToMaterialize"\>The ViewModel representing the schema-only node to be materialized.\</param\>  
    /// \<returns\>The newly created (materialized) DomNode, or null if creation failed.\</returns\>  
    public DomNode? MaterializeDomNodeForSchema(DataGridRowItemViewModel vmToMaterialize)  
    {  
        if (vmToMaterialize.DomNode \!= null) return vmToMaterialize.DomNode; // Already exists

        SchemaNode targetSchema \= vmToMaterialize.SchemaContextNode;  
        string nodeNameForTarget \= vmToMaterialize.NodeName; // This should be the actual key/property name.  
                                                             // For array items from schema, name would be index, which is complex here.  
                                                             // This flow is primarily for object properties.

        // 1\. Determine the path and find/create the actual parent DomNode.  
        //    This is the most complex part. We need the path from the root to the \*parent\* of the node to be materialized.  
        List\<PathSegment\> pathSegmentsToParent \= CalculatePathToParentForSchemaOnlyNode(vmToMaterialize);

        if (pathSegmentsToParent \== null) {  
            UpdateStatusBar($"Error: Could not determine DOM path for '{nodeNameForTarget}'.", true);  
            return null;  
        }

        DomNode currentParentDomNode \= \_rootDomNode ?? (\_rootDomNode \= DomFactory.CreateDefaultFromSchema("$root", FindRootSchemaOrDefault(), null)); // Ensure root exists

        SchemaNode? currentParentSchemaContext \= FindRootSchemaOrDefault();  
         \_domToSchemaMap.TryAdd(currentParentDomNode, currentParentSchemaContext);

        List\<DomNode\> createdNodesForUndo \= new List\<DomNode\>(); // Track all nodes created in this operation for undo

        foreach (var segment in pathSegmentsToParent)  
        {  
            if (\!(currentParentDomNode is ObjectNode currentObjectParent))  
            {  
                UpdateStatusBar($"Error: Expected object parent at '{segment.Name}' but found {currentParentDomNode?.GetType().Name}.", true);  
                return null; // Cannot proceed if path structure is wrong  
            }

            currentParentSchemaContext \= currentObjectParent \== \_rootDomNode ?  
                                         \_domToSchemaMap\[currentObjectParent\] : // Schema for root already mapped  
                                         currentParentSchemaContext?.Properties?.GetValueOrDefault(segment.Name); // Schema for current segment

            if (currentParentSchemaContext \== null && segment.IsSchemaProperty) { // Segment refers to a named property  
                 UpdateStatusBar($"Error: No schema found for intermediate path segment '{segment.Name}'.", true);  
                 return null;  
            }

            if (\!currentObjectParent.Children.TryGetValue(segment.Name, out DomNode? nextParentDomNode))  
            {  
                // This parent segment doesn't exist, materialize it.  
                if (segment.Schema \== null) { // Should not happen if pathSegmentsToParent is built correctly  
                     UpdateStatusBar($"Error: No schema to materialize intermediate parent '{segment.Name}'.", true);  
                     return null;  
                }  
                nextParentDomNode \= DomFactory.CreateDefaultFromSchema(segment.Name, segment.Schema, currentObjectParent);  
                currentObjectParent.Children\[segment.Name\] \= nextParentDomNode;  
                \_domToSchemaMap\[nextParentDomNode\] \= segment.Schema;  
                createdNodesForUndo.Add(nextParentDomNode); // Add to undo list  
            }  
            currentParentDomNode \= nextParentDomNode;  
        }

        // At this point, currentParentDomNode is the actual DOM parent for the node we want to materialize.  
        // nodeNameForTarget is the name of the node to create under currentParentDomNode.  
        // targetSchema is the schema for the node to create.

        if (\!(currentParentDomNode is ObjectNode finalParentObjectNode)) {  
            UpdateStatusBar($"Error: Final parent for '{nodeNameForTarget}' is not an object.", true);  
            return null;  
        }  
        if (finalParentObjectNode.Children.ContainsKey(nodeNameForTarget)) {  
             UpdateStatusBar($"Error: Node '{nodeNameForTarget}' already exists in parent '{finalParentObjectNode.Name}'. Materialization conflict.", true);  
            return finalParentObjectNode.Children\[nodeNameForTarget\]; // Return existing if conflict  
        }

        var newDomNode \= DomFactory.CreateDefaultFromSchema(nodeNameForTarget, targetSchema, finalParentObjectNode);  
        finalParentObjectNode.Children\[nodeNameForTarget\] \= newDomNode;  
        \_domToSchemaMap\[newDomNode\] \= targetSchema;  
        createdNodesForUndo.Add(newDomNode);

        // Record a composite undo operation if multiple nodes were created,  
        // or a single AddNodeOperation if only the target was created.  
        if (createdNodesForUndo.Any()) {  
            // For simplicity, let's assume a single AddNodeOperation for the target node,  
            // assuming parents were either existing or their creation is part of a larger implicit transaction.  
            // A robust undo would capture all creations.  
            // \_undoRedoService.RecordOperation(new MaterializeNodesOperation(createdNodesForUndo, finalParentObjectNode, nodeNameForTarget));  
             \_undoRedoService.RecordOperation(new AddNodeOperation(finalParentObjectNode, newDomNode, \-1)); // \-1 for object property  
        }

        MarkAsDirty();  
        UpdateStatusBar($"Node '{newDomNode.Name}' created from schema.", false);  
        return newDomNode;  
    }

    // Represents a segment in the path to a schema-only node's parent  
    private class PathSegment {  
        public string Name { get; } // Name of the property  
        public SchemaNode Schema { get; } // Schema for this segment  
        public bool IsSchemaProperty { get; } // True if this segment is from a schema property definition

        public PathSegment(string name, SchemaNode schema, bool isSchemaProperty \= true) { Name \= name; Schema \= schema; IsSchemaProperty \= isSchemaProperty; }  
    }

    // This is a highly complex helper method.  
    private List\<PathSegment\> CalculatePathToParentForSchemaOnlyNode(DataGridRowItemViewModel vm)  
    {  
        // Goal: Return a list of PathSegments from the root to the PARENT of vm.  
        // Each PathSegment contains the property name and its SchemaNode.  
        //  
        // Strategy:  
        // 1\. Find vm in FlatItemsSource.  
        // 2\. Iterate backwards through FlatItemsSource to find its hierarchical chain of parent VMs.  
        //    A parent VM is one at a shallower indentation level.  
        // 3\. Collect the SchemaContextNode and NodeName (actual property name) for each parent VM in the path.  
        //    Stop when you reach a VM that has a real DomNode, or the root.  
        //  
        // This needs to be robust. For now, this is a placeholder for that complex logic.  
        // The list should be ordered from root-most parent down to immediate parent.  
        var path \= new List\<PathSegment\>();  
        // ... complex logic to build path by inspecting FlatItemsSource and VM depths/parent relationships ...  
        // For example, if vm is for "propC" in "/propA/objB/propC"  
        // And propA is a real DomNode, objB is schema-only.  
        // Path would be: \[PathSegment("propA", schemaForPropA), PathSegment("objB", schemaForObjB)\]  
        // The node to materialize is "propC" under the materialized "objB".

        // This simplified example assumes vm directly knows its intended parent schema and name for simplicity  
        // In reality, this needs to be derived from the UI hierarchy or stored context.  
        if (vm.SchemaContextNode.Name \!= null && vm.ParentViewModel.\_rootDomNode \!= null && vm.ParentViewModel.\_domToSchemaMap.TryGetValue(vm.ParentViewModel.\_rootDomNode, out var rootSchema))  
        {  
            // This is a HUGELY simplified placeholder logic.  
            // It doesn't actually trace the path of schema-only intermediate parents.  
            // It just assumes the node to materialize is a direct child of the root.  
            // A real implementation would need to reconstruct the conceptual path.  
            //  
            // For a schema-only node \`vm\` representing property \`P\` of schema \`S\_parent\`,  
            // and \`S\_parent\` corresponds to \`D\_parent\` (which might itself be schema-only),  
            // you need to find the path to \`D\_parent\`.  
            //  
            // Let's assume for this skeleton that vm has a field like:  
            // \`vm.IntendedParentSchemaNodeKey\` and \`vm.IntendedPropertyNameInParent\`  
            // This context would be set when the schema-only VM is created in \`RefreshDisplayList\`.

            // If we can't find a direct DomNode parent, we assume it's off the root for now.  
            // This part NEEDS robust implementation.  
        }

        return path; // Placeholder, this method needs to be carefully designed  
    }  
     private SchemaNode? FindRootSchemaOrDefault()  
    {  
        \_schemaLoader.RootSchemas.TryGetValue("", out var rootSchema);  
        return rootSchema ?? new SchemaNode("$root\_fallback", typeof(object), false, false, null, null, null, null, null, false, new Dictionary\<string, SchemaNode\>(), null, true, null, "");  
    }  
    // ...  
}

**3\. `DomFactory.CreateDefaultFromSchema` Enhancements:**

Ensure this method correctly uses `SchemaNode.DefaultValue` which now can come from C\# property initializers or parameterless constructors. If `SchemaNode.DefaultValue` is a complex object instance, `DomFactory` might need to serialize it to JSON and then parse it into `DomNode`s, or reflect over the instance to build the `DomNode` tree. The spec states: "If an object has mandatory properties (as per its schema), these are included with their default values when a new array item is created" \- this should also apply here.

C\#  
// \--- File: JsonConfigEditor.Core/Dom/DomFactory.cs \---  
public static class DomFactory  
{  
    public static DomNode CreateDefaultFromSchema(string name, SchemaNode schema, DomNode? parent)  
    {  
        // 1\. Handle schema.DefaultValue first (highest priority)  
        if (schema.DefaultValue \!= null)  
        {  
            // If DefaultValue is a simple type (string, int, bool, double), create JsonElement directly.  
            // If DefaultValue is a complex object (instance from parameterless ctor or initializer),  
            // you might need to serialize this object to a temporary JSON string, then parse that string  
            // using JsonDomParser to build the DomNode structure.  
            // This ensures that if a default object itself has initialized properties, they are captured.  
            // Example (conceptual):  
            // string tempJson \= System.Text.Json.JsonSerializer.Serialize(schema.DefaultValue);  
            // using (JsonDocument tempDoc \= JsonDocument.Parse(tempJson))  
            // {  
            //    // Now use JsonDomParser.ConvertToDomNode logic to build from tempDoc.RootElement  
            //    // This is a bit circular but ensures consistency with JSON representation.  
            //    // Or, reflect over schema.DefaultValue directly if it's a known complex type.  
            // }  
            // For this skeleton, assume a simpler conversion for now or that DefaultValue is already JsonElement-like.  
             if (schema.DefaultValue is JsonElement je) {  
                 return new ValueNode(name, parent, je.Clone()); // Or other node type if je is object/array  
             }  
             // Fall through to type-based defaults if DefaultValue isn't directly usable or simple.  
        }

        // 2\. If no direct DefaultValue or it was null, create based on NodeType  
        switch (schema.NodeType)  
        {  
            case SchemaNodeType.Object:  
                var objectNode \= new ObjectNode(name, parent);  
                if (schema.Properties \!= null)  
                {  
                    foreach (var propPair in schema.Properties)  
                    {  
                        // If a property is required OR has a (non-null) default value itself, instantiate it.  
                        if (propPair.Value.IsRequired || propPair.Value.DefaultValue \!= null)  
                        {  
                            objectNode.Children\[propPair.Key\] \= CreateDefaultFromSchema(propPair.Key, propPair.Value, objectNode);  
                        }  
                    }  
                }  
                return objectNode;

            case SchemaNodeType.Array:  
                // Arrays typically default to empty unless schema.DefaultValue (from C\# initializer)  
                // provided specific items. If so, that was handled above.  
                return new ArrayNode(name, parent);

            case SchemaNodeType.Value:  
            default:  
                // Create JsonElement for C\# type defaults (0, false, null string etc.)  
                JsonElement defaultValueElement \= GetJsonElementForClrTypeDefault(schema.ClrType);  
                return new ValueNode(name, parent, defaultValueElement);  
        }  
    }

    private static JsonElement GetJsonElementForClrTypeDefault(Type clrType)  
    {  
        // Implementation: Create JsonElement for common CLR type defaults.  
        if (clrType \== typeof(int) || clrType \== typeof(double) /\* ... other numerics \*/) return JsonDocument.Parse("0").RootElement.Clone();  
        if (clrType \== typeof(bool)) return JsonDocument.Parse("false").RootElement.Clone();  
        if (clrType \== typeof(string)) return JsonDocument.Parse("null").RootElement.Clone(); // Or ""  
        // ... etc.  
        return JsonDocument.Parse("null").RootElement.Clone(); // Default fallback  
    }  
}

**Key Challenges in this Implementation:**

* **`MainViewModel.CalculatePathToParentForSchemaOnlyNode` / `FindOrMaterializeParentDomFor`:** This is the most complex piece. It needs to reliably determine the insertion point in the *actual* `_rootDomNode` tree for a schema-only node, potentially creating several levels of parent `DomNode`s that were also schema-only. This requires careful navigation of the `FlatItemsSource` (which reflects the *display* tree) and mapping that back to the *logical* schema tree and the *actual* DOM tree. Storing more contextual information (like intended parent `SchemaNode` and property name) in `DataGridRowItemViewModel` for schema-only nodes can simplify this.  
* **Undo/Redo for Materialization:** Materializing a node (and its parents) can involve multiple DOM changes. This should ideally be grouped into a single composite undo operation.  
* **`DomFactory.CreateDefaultFromSchema` with C\# Initializers:** If `SchemaNode.DefaultValue` is a complex C\# object instance (from an initializer or parameterless constructor), correctly translating that instance into a default `DomNode` structure requires either reflection over that instance or serializing it to JSON and re-parsing. The latter is often simpler and ensures the default adheres to JSON structure.  
* **State Synchronization:** After materialization, the `DataGridRowItemViewModel` that was schema-only needs its `DomNode` property updated, and its state (like `IsSchemaOnlyNode`, `ValueDisplay`) must refresh. The `_domToSchemaMap` also needs updating for all newly created nodes.

This detailed outline should guide you through implementing this advanced and crucial feature. It requires careful attention to detail in how the display list, schema tree, and actual DOM tree interact.

# Filtering the tree by node name, showing just invalid items with all their ancestors

Okay, implementing the filtering logic for the tree view by node name and for showing only invalid items (with their ancestors) is a key part of making the editor usable for larger or more complex JSON structures. This will primarily involve changes in your `MainViewModel` and how it populates the `FlatItemsSource`.

Here's a breakdown of the implementation strategy:

**Core Principles:**

1. **Centralized Filtering Logic:** The `MainViewModel` will manage the filter state (`FilterText`, `ShowOnlyInvalidNodes`) and will be responsible for applying these filters to determine which `DomNode`s (and their corresponding `DataGridRowItemViewModel`s) are visible.  
2. **Ancestor Visibility:** A crucial requirement for both filters is that if a node is visible due to matching the criteria, all of its parent nodes up to the root must also be visible to provide context.  
3. **Dynamic List Rebuilding:** Whenever a filter changes or the underlying data that affects filtering changes (like a node's validation status), the `FlatItemsSource` needs to be rebuilt.  
4. **Respecting Expansion State:** Filtering should still respect the `IsExpanded` state of parent nodes. A matching node deep in a collapsed branch will only become visible if its parent branches are expanded by the user (or by another mechanism like search results automatically expanding parents).

**1\. `MainViewModel` \- Managing Filter State and Triggering Updates:**

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
// ... (previous using statements and class structure) ...

public class MainViewModel : ViewModelBase  
{  
    private string \_filterText \= string.Empty;  
    private bool \_showOnlyInvalidNodes \= false;  
    private DataGridRowItemViewModel? \_currentlyEditedItemVm; // To ensure edited item is always visible

    /// \<summary\>  
    /// Gets or sets the text used to filter nodes by name.  
    /// When set, triggers a rebuild of the displayed items.  
    /// (From specification document, Section 2.5)  
    /// \</summary\>  
    public string FilterText  
    {  
        get \=\> \_filterText;  
        set  
        {  
            if (SetProperty(ref \_filterText, value)) // SetProperty is a common ViewModelBase helper for INotifyPropertyChanged  
            {  
                // If FilterText is active, ShowOnlyInvalidNodes should ideally be off,  
                // as "ShowOnlyInvalidNodes" replaces other filters.  
                if (\!string.IsNullOrWhiteSpace(\_filterText) && \_showOnlyInvalidNodes)  
                {  
                    // Turn off ShowOnlyInvalidNodes if user types in filter text,  
                    // or manage this interaction via UI logic (e.g., disable filter text when checkbox is on).  
                    // For now, let's assume they are mutually exclusive in application.  
                    ShowOnlyInvalidNodes \= false; // This will trigger its own refresh via its setter.  
                                                // Avoid double refresh by checking if already false.  
                }  
                else  
                {  
                    ApplyFiltersAndRefreshDisplayList();  
                }  
            }  
        }  
    }

    /// \<summary\>  
    /// Gets or sets a value indicating whether to display only nodes that have failed validation  
    /// (and their ancestors). This filter replaces the name filter.  
    /// (From specification document, Section 2.5)  
    /// \</summary\>  
    public bool ShowOnlyInvalidNodes  
    {  
        get \=\> \_showOnlyInvalidNodes;  
        set  
        {  
            if (SetProperty(ref \_showOnlyInvalidNodes, value))  
            {  
                if (\_showOnlyInvalidNodes && \!string.IsNullOrWhiteSpace(\_filterText))  
                {  
                    // Clear name filter when "ShowOnlyInvalidNodes" is activated.  
                    // Need to ensure FilterText's setter doesn't cause re-entrancy.  
                    string oldFilter \= \_filterText;  
                    \_filterText \= string.Empty; // Directly set field to avoid recursive call via property setter.  
                    OnPropertyChanged(nameof(FilterText)); // Notify UI that FilterText changed.  
                }  
                ApplyFiltersAndRefreshDisplayList();  
            }  
        }  
    }

    /// \<summary\>  
    /// Sets the currently edited item. Used to ensure it remains visible during filtering.  
    /// \</summary\>  
    public void SetCurrentlyEditedItem(DataGridRowItemViewModel? editingVm)  
    {  
        \_currentlyEditedItemVm \= editingVm;  
        // If filters are active, a re-filter might be needed if the edited item wasn't previously matching  
        // but now should be shown. For simplicity, can be part of the next full refresh.  
    }

    /// \<summary\>  
    /// Clears all active filters and refreshes the display.  
    /// \</summary\>  
    public void ClearFilters()  
    {  
        bool needsRefresh \= false;  
        if (\!string.IsNullOrWhiteSpace(\_filterText))  
        {  
            \_filterText \= string.Empty;  
            OnPropertyChanged(nameof(FilterText));  
            needsRefresh \= true;  
        }  
        if (\_showOnlyInvalidNodes)  
        {  
            \_showOnlyInvalidNodes \= false;  
            OnPropertyChanged(nameof(ShowOnlyInvalidNodes));  
            needsRefresh \= true;  
        }  
        if (needsRefresh)  
        {  
            ApplyFiltersAndRefreshDisplayList();  
        }  
    }

    // This method is called when filters change, or when data impacting visibility changes (e.g., validation status, expansion)  
    private void ApplyFiltersAndRefreshDisplayList()  
    {  
        FlatItemsSource.Clear();  
        if (\_rootDomNode \== null) return;

        // Determine which DomNodes should be visible based on current filters  
        HashSet\<DomNode\> visibleDomNodes \= DetermineVisibleDomNodes();

        // Recursively build the FlatItemsSource, only adding VMs whose DomNode is in visibleDomNodes  
        // and respecting their IsExpanded state.  
        AddDomNodesToFlatListRecursive(\_rootDomNode, 0, visibleDomNodes);  
    }

    /// \<summary\>  
    /// Determines the set of DomNodes that should be visible based on current filter settings.  
    /// This includes direct matches and all their ancestors.  
    /// (From specification document, Section 2.5)  
    /// \</summary\>  
    private HashSet\<DomNode\> DetermineVisibleDomNodes()  
    {  
        var directlyMatchingNodes \= new HashSet\<DomNode\>();  
        if (\_rootDomNode \== null) return directlyMatchingNodes;

        // Recursive function to find direct matches  
        Action\<DomNode\> findDirectMatchesRecursive \= null\!;  
        findDirectMatchesRecursive \= (DomNode currentNode) \=\>  
        {  
            bool matches \= false;  
            var vm \= GetViewModelForDomNode(currentNode); // Helper to get or create a temporary VM to check IsValid

            if (\_showOnlyInvalidNodes)  
            {  
                // A node is a direct match if it itself is invalid.  
                // The VM's IsValid property needs to be up-to-date from the ValidationService.  
                if (vm \!= null && \!vm.IsValid) // Assumes vm.IsValid is accurate  
                {  
                    matches \= true;  
                }  
            }  
            else if (\!string.IsNullOrWhiteSpace(\_filterText))  
            {  
                if (currentNode.Name.IndexOf(\_filterText, StringComparison.OrdinalIgnoreCase) \>= 0\)  
                {  
                    matches \= true;  
                }  
            }  
            else  
            {  
                // No filters active, so every node is a "direct match" in terms of filter criteria.  
                matches \= true;  
            }

            // The currently edited node is always a "direct match" for visibility purposes  
            if (\_currentlyEditedItemVm \!= null && \_currentlyEditedItemVm.DomNode \== currentNode)  
            {  
                matches \= true;  
            }

            if (matches)  
            {  
                directlyMatchingNodes.Add(currentNode);  
            }

            // Traverse children regardless of current node's match, as children might match  
            if (currentNode is ObjectNode on)  
            {  
                foreach (var child in on.Children.Values) findDirectMatchesRecursive(child);  
            }  
            else if (currentNode is ArrayNode an)  
            {  
                foreach (var child in an.Items) findDirectMatchesRecursive(child);  
            }  
        };

        findDirectMatchesRecursive(\_rootDomNode);

        // Now, ensure all ancestors of directly matching nodes are included  
        var finalVisibleNodes \= new HashSet\<DomNode\>();  
        foreach (var matchedNode in directlyMatchingNodes)  
        {  
            DomNode? current \= matchedNode;  
            while (current \!= null)  
            {  
                finalVisibleNodes.Add(current);  
                current \= current.Parent;  
            }  
        }  
        return finalVisibleNodes;  
    }

    // Recursive helper to build the flat list for display  
    private void AddDomNodesToFlatListRecursive(DomNode domNode, int depth, HashSet\<DomNode\> visibleDomNodes)  
    {  
        if (\!visibleDomNodes.Contains(domNode))  
        {  
            return; // This node (and its subtree, unless a descendant is independently visible) is not visible  
        }

        // Try to find an existing VM or create a new one.  
        // This assumes we have a way to map DomNode to its VM if it was previously created,  
        // or we create new VMs. For filtering, it's often easier to recreate the FlatItemsSource.  
        \_domToSchemaMap.TryGetValue(domNode, out var schemaNode); // Get schema for the VM  
        var vm \= new DataGridRowItemViewModel(domNode, schemaNode, this);  
        // The VM's IsValid state must be set by the ValidationService prior to filtering.  
        // If not, the "ShowOnlyInvalidNodes" filter won't work correctly.  
        // This implies validation status should be associated with the DomNode or a map.  
        // For now, assume vm.IsValid is correctly reflecting the node's validation status.

        FlatItemsSource.Add(vm);

        if (vm.IsExpanded) // Respect current expansion state  
        {  
            if (domNode is ObjectNode on)  
            {  
                foreach (var child in on.Children.Values.OrderBy(c \=\> c.Name)) // Consistent ordering  
                {  
                    AddDomNodesToFlatListRecursive(child, depth \+ 1, visibleDomNodes);  
                }  
            }  
            else if (domNode is ArrayNode an)  
            {  
                foreach (var child in an.Items)  
                {  
                    AddDomNodesToFlatListRecursive(child, depth \+ 1, visibleDomNodes);  
                }  
            }  
        }  
    }

    /// \<summary\>  
    /// Helper to get or create a temporary ViewModel for a DomNode, primarily to check its IsValid status.  
    /// This is a simplified concept; ideally, IsValid is a state directly queryable or stored with the DomNode metadata.  
    /// \</summary\>  
    private DataGridRowItemViewModel GetViewModelForDomNode(DomNode node)  
    {  
        // In a real scenario, you would either:  
        // 1\. Have a persistent mapping of DomNode \-\> DataGridRowItemViewModel.  
        // 2\. Store validation status more directly associated with DomNode or via ValidationService results.  
        // For this filtering logic, we need to know if a DomNode \*would be\* invalid.  
        // This implies the validation status is readily available for each DomNode.  
        // Let's assume a method exists on MainViewModel or a service:  
        // \`bool isNodeActuallyInvalid \= \_validationService.IsNodeMarkedInvalid(node);\`  
        // For the skeleton, we'll create a temporary VM.  
        \_domToSchemaMap.TryGetValue(node, out var schemaNode);  
        var tempVm \= new DataGridRowItemViewModel(node, schemaNode, this);  
        // Manually refresh its validation state if not already done.  
        // This is conceptual; validation state is usually updated after validation runs.  
        // tempVm.SetValidationState(... from validation results ...);  
        return tempVm;  
    }

    // OnExpansionChanged needs to call ApplyFiltersAndRefreshDisplayList to ensure  
    // newly expanded children are correctly filtered.  
    public new void OnExpansionChanged(DataGridRowItemViewModel changedItem) // 'new' if hiding a base version  
    {  
        // The actual changedItem.IsExpanded has already been set by its property setter.  
        ApplyFiltersAndRefreshDisplayList();  
    }

    // Call ApplyFiltersAndRefreshDisplayList when a node's IsValid status might have changed  
    // (e.g., after an edit and commit which runs validation).  
    public void NotifyValidationStatusChanged(DataGridRowItemViewModel itemVm)  
    {  
        if (\_showOnlyInvalidNodes) // Only re-filter if this specific filter is active  
        {  
            ApplyFiltersAndRefreshDisplayList();  
        }  
    }  
}

**2\. `DataGridRowItemViewModel` \- Interaction with Filtering:**

* The existing `IsValid` property is key for the "Show Just Invalid Nodes" filter.  
* When `CommitEdit()` results in a change to `IsValid`, it might need to notify `MainViewModel` if the "ShowOnlyInvalidNodes" filter is active, so the list can be refreshed. (This is handled by `NotifyValidationStatusChanged` conceptual call above).

**3\. UI (XAML):**

* Add a `TextBox` in your View, two-way bound to `MainViewModel.FilterText`.  
* Add a `CheckBox` in your View, two-way bound to `MainViewModel.ShowOnlyInvalidNodes`.  
* Consider UI logic: if `ShowOnlyInvalidNodes` is checked, the `FilterText` `TextBox` could be disabled, as the "invalid nodes" filter takes precedence.

**Implementation Strategy Summary:**

1. **State Management:** Add `FilterText` and `ShowOnlyInvalidNodes` to `MainViewModel`.  
2. **Trigger Refresh:** Any change to these properties calls `ApplyFiltersAndRefreshDisplayList()`.  
3. **`DetermineVisibleDomNodes()`:**  
   * This is the core logic. It first identifies all `DomNode`s that are "direct matches" based on the active filter (either `FilterText` search on name, or `!vm.IsValid` for `ShowOnlyInvalidNodes`).  
   * The node currently being edited is always considered a direct match.  
   * Then, it iterates through these direct matches and adds all their ancestors (parent, grandparent, etc., up to the root) to a final set of nodes that must be visible.  
4. **`AddDomNodesToFlatListRecursive()`:**  
   * This method (or your existing list-building method) is modified. It takes the `visibleDomNodes` set as a parameter.  
   * It recursively traverses the *entire* `_rootDomNode` structure.  
   * For each `DomNode`, it checks if it's in the `visibleDomNodes` set.  
   * If it is, a `DataGridRowItemViewModel` is created/retrieved and added to `FlatItemsSource`.  
   * Recursion into children only happens if the current node is in `visibleDomNodes` AND its `IsExpanded` flag is true.  
5. **Validation State Dependency:** The "Show Just Invalid Nodes" filter relies on the `IsValid` property of `DataGridRowItemViewModel`s being accurate. This means that after any operation that might change validation status (loading a file, committing an edit), the full validation process (from Milestone 3-4 outline) should run and update these `IsValid` flags *before* filtering is applied or re-applied.

This approach ensures that the displayed tree correctly reflects the filter criteria while maintaining the necessary hierarchical context by showing all ancestors of matched nodes. The use of a `HashSet` for `visibleDomNodes` provides efficient lookup during the list-building phase.

previous outline for filtering implied that the expansion state set by the user would still be the primary gatekeeper for visibility *after* the filter identifies potential candidates.

The specification for **Search (Spec Section 2.6)** actually provides the better model for this behavior: *"If a node is currently collapsed, but a search matches one of its hidden children, it should be automatically expanded."*

This principle should ideally apply to **Filtering** as well if the goal is to reveal *all* nodes that match the filter criteria, regardless of their initial collapsed state.

Let's adjust the implementation strategy for filtering to incorporate this "auto-expansion" behavior.

**Revised Core Principle for Filtering with Auto-Expansion:**

1. **Identify All Matching Nodes:** First, determine the set of all `DomNode`s in the entire tree that directly match the filter criteria (`FilterText` on name, or `!IsValid` for "Show Invalid").  
2. **Determine Final Visible Set (Matches \+ Ancestors):** For every directly matching node, ensure it and all its ancestors are marked for display.  
3. **Auto-Expand for Visibility:** When building the `FlatItemsSource`:  
   * If a node is in the "final visible set":  
     * Add its `DataGridRowItemViewModel` to `FlatItemsSource`.  
     * If this node is an ancestor of a *directly matching node* (or is a direct match itself and is expandable), its `IsExpanded` property in the `DataGridRowItemViewModel` should be programmatically set to `true` to ensure the match is visible. This might be a temporary, filter-induced expansion state or could modify the user's actual expansion state. The latter is simpler to implement.  
   * If a node is *not* in the "final visible set," it's not added to `FlatItemsSource`.

**Revised `MainViewModel` Filtering Logic:**

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
// ... (previous using statements and class structure) ...

public class MainViewModel : ViewModelBase  
{  
    // ... (FilterText, ShowOnlyInvalidNodes, \_currentlyEditedItemVm properties as before) ...

    // Stores the user's actual expansion states, separate from filter-induced expansion  
    private Dictionary\<string, bool\> \_userExpansionStates \= new Dictionary\<string, bool\>(); // Key: DomNode Path, Value: IsExpanded

    private void ApplyFiltersAndRefreshDisplayList()  
    {  
        FlatItemsSource.Clear();  
        if (\_rootDomNode \== null) return;

        // 1\. Determine direct matches and the comprehensive set of nodes to make visible (matches \+ their ancestors)  
        HashSet\<DomNode\> directlyMatchingNodes \= FindDirectlyMatchingDomNodes();  
        HashSet\<DomNode\> finalVisibleNodesIncludingAncestors \= GetNodesAndTheirAncestors(directlyMatchingNodes);

        if (\!finalVisibleNodesIncludingAncestors.Any() && (ShowOnlyInvalidNodes || \!string.IsNullOrWhiteSpace(FilterText)))  
        {  
            // No nodes match the filter criteria (and it's not an empty filter)  
            UpdateStatusBar(ShowOnlyInvalidNodes ? "No invalid nodes found." : $"No nodes found matching '{FilterText}'.", false);  
            return;  
        }  
        if (finalVisibleNodesIncludingAncestors.Any()) {  
             UpdateStatusBar("", false); // Clear previous messages if results found  
        }

        // 2\. Recursively build the FlatItemsSource.  
        //    During this process, if a node is an ancestor of a direct match (or is a direct match itself),  
        //    ensure its VM's IsExpanded state is set to true.  
        AddDomNodesToFlatListRecursive(\_rootDomNode, 0, finalVisibleNodesIncludingAncestors, directlyMatchingNodes);  
    }

    private HashSet\<DomNode\> FindDirectlyMatchingDomNodes()  
    {  
        var directMatches \= new HashSet\<DomNode\>();  
        if (\_rootDomNode \== null) return directMatches;

        Action\<DomNode\> findRecursive \= null\!;  
        findRecursive \= (DomNode currentNode) \=\>  
        {  
            bool matches \= false;  
            // GetViewModelForDomNode is a helper that might temporarily create a VM or fetch validation status  
            var tempVm \= GetViewModelForDomNode(currentNode); // You need an efficient way to check IsValid

            if (\_showOnlyInvalidNodes)  
            {  
                if (tempVm \!= null && \!tempVm.IsValid) // This requires IsValid to be accurately known for all DomNodes  
                {  
                    matches \= true;  
                }  
            }  
            else if (\!string.IsNullOrWhiteSpace(\_filterText))  
            {  
                if (currentNode.Name.IndexOf(\_filterText, StringComparison.OrdinalIgnoreCase) \>= 0\)  
                {  
                    matches \= true;  
                }  
            }  
            else  
            {  
                // No active filter criteria, so no nodes are "directly matching" due to a filter.  
                // However, if \_currentlyEditedItemVm is set, it should be shown.  
                // The "all nodes visible" case is handled by GetNodesAndTheirAncestors if directMatches is empty and filters are off.  
            }

            if (\_currentlyEditedItemVm \!= null && \_currentlyEditedItemVm.DomNode \== currentNode)  
            {  
                matches \= true; // Always include the edited node if filters are active  
            }

            if (matches)  
            {  
                directMatches.Add(currentNode);  
            }

            if (currentNode is ObjectNode on)  
            {  
                foreach (var child in on.Children.Values) findRecursive(child);  
            }  
            else if (currentNode is ArrayNode an)  
            {  
                foreach (var child in an.Items) findRecursive(child);  
            }  
        };

        findRecursive(\_rootDomNode);  
        return directMatches;  
    }

    private HashSet\<DomNode\> GetNodesAndTheirAncestors(HashSet\<DomNode\> nodes)  
    {  
        var resultSet \= new HashSet\<DomNode\>();  
        if (\!nodes.Any() && string.IsNullOrWhiteSpace(FilterText) && \!ShowOnlyInvalidNodes) {  
            // No specific filter, show all nodes (all nodes are effectively "visible")  
            Action\<DomNode\> addAllRecursive \= null\!;  
            addAllRecursive \= (DomNode node) \=\> {  
                resultSet.Add(node);  
                if (node is ObjectNode on) foreach(var child in on.Children.Values) addAllRecursive(child);  
                else if (node is ArrayNode an) foreach(var item in an.Items) addAllRecursive(item);  
            };  
            if(\_rootDomNode \!= null) addAllRecursive(\_rootDomNode);  
            return resultSet;  
        }

        foreach (var node in nodes)  
        {  
            DomNode? current \= node;  
            while (current \!= null)  
            {  
                resultSet.Add(current);  
                current \= current.Parent;  
            }  
        }  
        return resultSet;  
    }

    // Modified recursive helper  
    private void AddDomNodesToFlatListRecursive(  
        DomNode domNode,  
        int depth,  
        HashSet\<DomNode\> finalVisibleNodes,  
        HashSet\<DomNode\> directlyMatchingNodes)  
    {  
        if (\!finalVisibleNodes.Contains(domNode))  
        {  
            return; // This node is not part of the filtered set (neither a match nor an ancestor of a match)  
        }

        \_domToSchemaMap.TryGetValue(domNode, out var schemaNode);  
        var vm \= GetOrCreateDataGridRowItemViewModel(domNode, schemaNode); // Method to get existing or create new VM

        // Determine if this node needs to be programmatically expanded  
        bool shouldBeExpandedByFilter \= ShouldNodeBeExpandedByFilter(domNode, vm, finalVisibleNodes, directlyMatchingNodes);

        // Preserve user's expansion state if not affected by filter's need to reveal a child  
        string nodePath \= GetDomNodePath(domNode) ?? Guid.NewGuid().ToString(); // Ensure unique path for state storage  
        if (\_userExpansionStates.TryGetValue(nodePath, out bool userSetExpansion) && \!shouldBeExpandedByFilter)  
        {  
            vm.SetExpansionStateInternal(userSetExpansion); // Internal set to avoid re-triggering full refresh  
        }  
        else  
        {  
            vm.SetExpansionStateInternal(shouldBeExpandedByFilter); // Filter forces expansion or defaults to it  
        }

        FlatItemsSource.Add(vm);

        if (vm.IsExpandedInternal) // Use the VM's actual current expansion state  
        {  
            if (domNode is ObjectNode on)  
            {  
                foreach (var child in on.Children.Values.OrderBy(c \=\> c.Name))  
                {  
                    AddDomNodesToFlatListRecursive(child, depth \+ 1, finalVisibleNodes, directlyMatchingNodes);  
                }  
            }  
            else if (domNode is ArrayNode an)  
            {  
                foreach (var child in an.Items)  
                {  
                    AddDomNodesToFlatListRecursive(child, depth \+ 1, finalVisibleNodes, directlyMatchingNodes);  
                }  
            }  
        }  
    }

    private bool ShouldNodeBeExpandedByFilter(DomNode node, DataGridRowItemViewModel vm, HashSet\<DomNode\> finalVisibleNodes, HashSet\<DomNode\> directlyMatchingNodes)  
    {  
        if (\!vm.IsExpandable) return false;

        // If any \*direct descendant\* that is a \*direct filter match\* exists, this node must be expanded.  
        // This requires a traversal down from 'node' checking against 'directlyMatchingNodes'.  
        // For simplicity in this outline: if any child in 'finalVisibleNodes' exists, expand.  
        // A more precise check: is this node an ancestor of any node in 'directlyMatchingNodes'?  
        if (directlyMatchingNodes.Any(match \=\> IsAncestor(node, match) && node \!= match))  
        {  
            return true; // This node is an ancestor of a direct match, so expand it  
        }

        // Default expansion state if not forced by filter (e.g., root is often expanded)  
        // Or, if no active filter, revert to user's preferences or default.  
        if (string.IsNullOrWhiteSpace(FilterText) && \!ShowOnlyInvalidNodes) {  
             string nodePath \= GetDomNodePath(node) ?? Guid.NewGuid().ToString();  
            return \_userExpansionStates.TryGetValue(nodePath, out bool userExpansion) ? userExpansion : (node.Depth \< 1); // Default expand root  
        }

        return false; // Default to collapsed unless it's an ancestor of a direct match  
    }

    // Helper to check if 'potentialAncestor' is an ancestor of 'node'  
    private bool IsAncestor(DomNode potentialAncestor, DomNode node)  
    {  
        DomNode? parent \= node.Parent;  
        while (parent \!= null)  
        {  
            if (parent \== potentialAncestor) return true;  
            parent \= parent.Parent;  
        }  
        return false;  
    }

    // When user manually expands/collapses a node:  
    public new void OnExpansionChanged(DataGridRowItemViewModel changedItem)  
    {  
        // Store the user's explicit expansion choice  
        string nodePath \= GetDomNodePath(changedItem.DomNode) ?? Guid.NewGuid().ToString();  
        \_userExpansionStates\[nodePath\] \= changedItem.IsExpandedInternal; // Assuming IsExpandedInternal directly reflects checkbox

        // Re-apply filters and rebuild, because the number of visible items might change.  
        // The rebuild will now respect the new user-set expansion state for this node  
        // unless the filter overrides it to show a descendant.  
        ApplyFiltersAndRefreshDisplayList();  
    }

    // \`GetViewModelForDomNode\` would ideally fetch from a persistent dictionary mapping DomNode to its VM  
    // or create a new one if not found, for consistent IsValid checks.  
    // This helps ensure that IsValid reflects the true state from the last validation pass.  
    private Dictionary\<DomNode, DataGridRowItemViewModel\> \_persistentVmMap \= new Dictionary\<DomNode, DataGridRowItemViewModel\>();

    private DataGridRowItemViewModel GetOrCreateDataGridRowItemViewModel(DomNode domNode, SchemaNode? schemaNode)  
    {  
        if (\_persistentVmMap.TryGetValue(domNode, out var existingVm))  
        {  
            // Ensure its schema context is up-to-date if schemas can change  
            existingVm.UpdateSchemaInfo(schemaNode); // Assuming this method exists  
            return existingVm;  
        }  
        var newVm \= new DataGridRowItemViewModel(domNode, schemaNode, this);  
        // IMPORTANT: \`newVm.IsValid\` needs to be set here based on the last global validation run.  
        // This requires storing validation results per DomNode.  
        // For now, assume new VMs are valid until proven otherwise or a global validation updates them.  
        if (\_validationIssuesMap.TryGetValue(domNode, out var issues) && issues.Any()) {  
            newVm.SetValidationState(false, issues.First().Message);  
        } else {  
            newVm.SetValidationState(true, string.Empty);  
        }

        \_persistentVmMap\[domNode\] \= newVm; // Cache it  
        return newVm;  
    }  
    // Call Clear on \_persistentVmMap when loading new file / clearing document.  
    // Map of validation issues updated by ValidateFullDocument()  
    private Dictionary\<DomNode, List\<ValidationIssue\>\> \_validationIssuesMap \= new Dictionary\<DomNode, List\<ValidationIssue\>\>();

}

// In DataGridRowItemViewModel:  
public class DataGridRowItemViewModel : ViewModelBase  
{  
    // ...  
    private bool \_isExpandedInternal; // Stores the actual expansion state  
    public bool IsExpandedInternal \=\> \_isExpandedInternal;

    public bool IsExpanded // This is bound to the ToggleButton  
    {  
        get \=\> \_isExpandedInternal;  
        set  
        {  
            if (\_isExpandedInternal \!= value)  
            {  
                \_isExpandedInternal \= value;  
                OnPropertyChanged(); // Notify UI to change toggle state  
                ParentViewModel.OnExpansionChanged(this); // Notify MainViewModel to rebuild list  
            }  
        }  
    }

    /// \<summary\>  
    /// Allows MainViewModel to set expansion state without triggering a full refresh cycle via property setter.  
    /// Used when filter logic determines expansion.  
    /// \</summary\>  
    public void SetExpansionStateInternal(bool expanded)  
    {  
        if (\_isExpandedInternal \!= expanded)  
        {  
            \_isExpandedInternal \= expanded;  
            OnPropertyChanged(nameof(IsExpanded)); // Update UI if needed  
        }  
    }  
    // ...  
}

**Key Changes in this Revised Approach:**

1. **`_userExpansionStates` Dictionary:** `MainViewModel` now attempts to store the user's preferred expansion state for each node (keyed by a unique path).  
2. **`DetermineVisibleDomNodes()` & `GetNodesAndTheirAncestors()`:** These helpers first find all nodes that directly match the filter and then ensure all their ancestors are also included in the `finalVisibleNodes` set. If no filter is active, all nodes are included.  
3. **`AddDomNodesToFlatListRecursive()` and `ShouldNodeBeExpandedByFilter()`:**  
   * The recursive list builder only adds nodes present in `finalVisibleNodes`.  
   * `ShouldNodeBeExpandedByFilter()` determines if a node *must* be expanded by the filter (because it's an ancestor of a direct match).  
   * If the filter doesn't force expansion, the node's expansion state attempts to use the stored `_userExpansionStates`. Otherwise, filter-induced expansion takes precedence.  
4. **`DataGridRowItemViewModel.SetExpansionStateInternal()`:** A way for `MainViewModel` to set the visual expansion state of a VM without triggering the `OnExpansionChanged` full refresh cycle that the UI-bound `IsExpanded` setter would. The UI still updates due to `OnPropertyChanged`.  
5. **`OnExpansionChanged()` in `MainViewModel`:** When the user *manually* clicks the toggle:  
   * Update `_userExpansionStates` for that node.  
   * Then, call `ApplyFiltersAndRefreshDisplayList()`. The list rebuild will now respect this new user preference unless the filter logic for revealing a deeper match overrides it.  
6. **`GetOrCreateDataGridRowItemViewModel` and `_persistentVmMap`**: To maintain VM state (like `IsValid` or user-set `IsExpanded`) across filter refreshes, it's better to cache VMs associated with `DomNode`s. `_validationIssuesMap` would be populated by `ValidateFullDocument()`.

**Workflow with Auto-Expansion:**

1. User applies a filter (e.g., types "Port" in `FilterText` or checks "Show Invalid").  
2. `MainViewModel.ApplyFiltersAndRefreshDisplayList()` is called.  
3. `DetermineVisibleDomNodes()` finds all `DomNode`s that directly match (e.g., `DomNode.Name` contains "Port").  
4. `GetNodesAndTheirAncestors()` creates a set (`finalVisibleNodes`) containing these matches and all their parents up to the root.  
5. `AddDomNodesToFlatListRecursive()` iterates the original `_rootDomNode`:  
   * If a `DomNode` is in `finalVisibleNodes`:  
     * Its VM is added to `FlatItemsSource`.  
     * `ShouldNodeBeExpandedByFilter()` checks if this `DomNode` is an ancestor of any *directly matching node*.  
     * If yes, `vm.SetExpansionStateInternal(true)` is called (its `IsExpanded` becomes true for the UI).  
     * If no, and the user had a previous expansion state for this node, that's used.  
     * If the VM is now considered expanded, the recursion continues for its children.  
   * If a `DomNode` is NOT in `finalVisibleNodes`, it and its children are skipped (unless a child is independently in `finalVisibleNodes` due to being an ancestor of another match – `finalVisibleNodes` handles this global visibility).

This revised approach ensures that if a filter (name or invalid state) matches a node, that node and its entire parental chain will be made visible, with necessary ancestors being programmatically expanded. The user's own expansion choices are preserved for nodes not directly affected by the filter's need to reveal a match.

# In-place renderers and editors for common data types

Okay, implementing the in-place renderers and editors for common data types is key to the editor's usability. This involves making the "Value" column of your `DataGrid` dynamic, switching between a display representation (renderer) and an appropriate input control (editor) based on the data type and edit state.

Here’s a breakdown of how you would implement this, focusing on the XAML structure with `DataTemplateSelector` and the necessary ViewModel support, as per **Spec Section 2.3.2 (Value Rendering)** and **Spec Section 2.4 (In-Place Editing)**.

**Core Strategy:**

1. **`DataGridRowItemViewModel` State:** The ViewModel will continue to hold `IsInEditMode`, `EditValue` (for the editor's binding), `ValueDisplay` (for the renderer's binding), and crucially, `AssociatedSchemaNode` (which provides `ClrType` and constraints like `AllowedValues`).  
2. **`DataTemplateSelector`:** A custom `DataTemplateSelector` will be used for the "Value" column's cell content.  
   * If `IsInEditMode` is `false`, it can select a generic "display" template or type-specific display templates.  
   * If `IsInEditMode` is `true`, it will select an appropriate "editor" template based on `AssociatedSchemaNode.ClrType` (or `DomNode.Value.ValueKind` for unschematized nodes).  
3. **Specific `DataTemplate`s:** You'll define separate `DataTemplate`s for:  
   * Displaying strings, numbers, booleans.  
   * Editing strings (`TextBox`).  
   * Editing numbers (`TextBox` with numeric validation/behavior).  
   * Editing booleans (`CheckBox`).  
   * Editing enums or strings with `AllowedValues` (`ComboBox`).  
   * Editing `RefNode` paths (`TextBox` \+ button for modal).

**1\. `DataGridRowItemViewModel` Support:**

Ensure these properties are robust:

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/DataGridRowItemViewModel.cs \---  
public class DataGridRowItemViewModel : ViewModelBase  
{  
    // ... (DomNode, AssociatedSchemaNode, IsInEditMode, IsEditable, IsValid, ValidationErrorMessage, etc.)

    /// \<summary\>  
    /// Gets the string representation of the current value for display.  
    /// Handles formatting for different types, including placeholders for objects/arrays.  
    /// (From specification document, Section 2.3.2)  
    /// \</summary\>  
    public string ValueDisplay  
    {  
        get  
        {  
            if (IsSchemaOnlyNode) // Display default from schema  
            {  
                if (AssociatedSchemaNode.DefaultValue \!= null)  
                    return AssociatedSchemaNode.DefaultValue.ToString() ?? "null"; // Or better formatting  
                if (AssociatedSchemaNode.NodeType \== SchemaNodeType.Object) return "\[Object (Default)\]";  
                if (AssociatedSchemaNode.NodeType \== SchemaNodeType.Array) return "\[Array (Default)\]";  
                return GetDefaultDisplayForClrType(AssociatedSchemaNode.ClrType);  
            }  
            if (DomNode is ValueNode vn) return vn.Value.ToString(); // Needs culture-aware formatting for numbers/dates  
            if (DomNode is RefNode rn) return $"$ref: {rn.ReferencePath}";  
            if (DomNode is ArrayNode an) return $"\[{an.Items.Count} items\]";  
            if (DomNode is ObjectNode) return "\[Object\]";  
            return string.Empty;  
        }  
    }

    /// \<summary\>  
    /// Gets or sets the value being edited, always as a string.  
    /// Conversion to/from the actual data type happens during CommitEdit and when entering edit mode.  
    /// (From specification document, Section 2.4)  
    /// \</summary\>  
    private string \_editValue \= string.Empty;  
    public string EditValue  
    {  
        get \=\> \_editValue;  
        set \=\> SetProperty(ref \_editValue, value); // SetProperty is a helper from ViewModelBase  
    }

    // When IsInEditMode is set to true:  
    // Populate \_editValue from DomNode.Value or SchemaContextNode.DefaultValue  
    // Example within IsInEditMode setter:  
    // if (value \== true && IsEditable) {  
    //    if (IsSchemaOnlyNode) {  
    //        EditValue \= AssociatedSchemaNode.DefaultValue?.ToString() ?? GetDefaultStringForClrType(AssociatedSchemaNode.ClrType);  
    //    } else if (DomNode is ValueNode vn) {  
    //        EditValue \= vn.Value.ToString(); // Or more specific formatting  
    //    } else if (DomNode is RefNode rn) {  
    //        EditValue \= rn.ReferencePath;  
    //    }  
    // }

    // CommitEdit() method (already outlined) will handle parsing \_editValue back to the appropriate type.

    private string GetDefaultDisplayForClrType(Type type)  
    {  
        if (type \== typeof(string)) return "(empty string)";  
        if (type \== typeof(int) || type \== typeof(double) /\*...\*/) return "0";  
        if (type \== typeof(bool)) return "false";  
        return "null";  
    }  
}

**2\. XAML for the "Value" Column using `ContentControl` and `DataTemplateSelector`:**

XML  
\<DataTemplate x:Key="DisplayStringRendererTemplate" DataType="{x:Type vm:DataGridRowItemViewModel}"\>  
    \<TextBlock Text="{Binding ValueDisplay}" VerticalAlignment="Center" Style="{StaticResource DisplayValueTextBlockStyle}"/\>  
\</DataTemplate\>

\<DataTemplate x:Key="DisplayBooleanRendererTemplate" DataType="{x:Type vm:DataGridRowItemViewModel}"\>  
    \<CheckBox IsChecked="{Binding DomNode.Value, Converter={StaticResource JsonElementToBoolConverter}, Mode=OneWay}"  
              IsEnabled="False" VerticalAlignment="Center" HorizontalAlignment="Left"/\>  
\</DataTemplate\>

\<DataTemplate x:Key="StringEditorTemplate" DataType="{x:Type vm:DataGridRowItemViewModel}"\>  
    \<TextBox Text="{Binding EditValue, UpdateSourceTrigger=LostFocus}" VerticalAlignment="Center" Style="{StaticResource EditingTextBoxStyle}"\>  
        \<TextBox.InputBindings\>  
            \<KeyBinding Key="Enter" Command="{Binding ParentViewModel.ConfirmEditCommand}" CommandParameter="{Binding}"/\>  
            \<KeyBinding Key="Escape" Command="{Binding ParentViewModel.CancelEditCommand}" CommandParameter="{Binding}"/\>  
        \</TextBox.InputBindings\>  
    \</TextBox\>  
\</DataTemplate\>

\<DataTemplate x:Key="BooleanEditorTemplate" DataType="{x:Type vm:DataGridRowItemViewModel}"\>  
    \<CheckBox IsChecked="{Binding EditValue, Converter={StaticResource StringToNullableBoolConverter}, UpdateSourceTrigger=PropertyChanged}" VerticalAlignment="Center"\>  
        \</CheckBox\>  
\</DataTemplate\>

\<DataTemplate x:Key="EnumEditorTemplate" DataType="{x:Type vm:DataGridRowItemViewModel}"\>  
    \<ComboBox ItemsSource="{Binding AssociatedSchemaNode.AllowedValues}"  
              Text="{Binding EditValue, UpdateSourceTrigger=LostFocusOrEnter}" IsEditable="True" VerticalAlignment="Center" Style="{StaticResource EditingComboBoxStyle}"\>  
        \<ComboBox.InputBindings\>  
            \<KeyBinding Key="Enter" Command="{Binding ParentViewModel.ConfirmEditCommand}" CommandParameter="{Binding}"/\>  
            \<KeyBinding Key="Escape" Command="{Binding ParentViewModel.CancelEditCommand}" CommandParameter="{Binding}"/\>  
        \</ComboBox.InputBindings\>  
    \</ComboBox\>  
\</DataTemplate\>

\<DataTemplate x:Key="RefNodePathEditorTemplate" DataType="{x:Type vm:DataGridRowItemViewModel}"\>  
    \<DockPanel\>  
        \<Button DockPanel.Dock="Right" Content="..." Padding="3,0"  
                Command="{Binding ParentViewModel.OpenRefNodePathEditorModalCommand}"  
                CommandParameter="{Binding}"  
                ToolTip="Open advanced reference editor"/\>  
        \<TextBox Text="{Binding EditValue, UpdateSourceTrigger=LostFocus}" VerticalAlignment="Center" Style="{StaticResource EditingTextBoxStyle}"\>  
            \<TextBox.InputBindings\>  
                \<KeyBinding Key="Enter" Command="{Binding ParentViewModel.ConfirmEditCommand}" CommandParameter="{Binding}"/\>  
                \<KeyBinding Key="Escape" Command="{Binding ParentViewModel.CancelEditCommand}" CommandParameter="{Binding}"/\>  
            \</TextBox.InputBindings\>  
        \</TextBox\>  
    \</DockPanel\>  
\</DataTemplate\>

\<DataTemplate x:Key="NumberEditorTemplate" DataType="{x:Type vm:DataGridRowItemViewModel}" BasedOn="{StaticResource StringEditorTemplate}"/\>

\<selectors:ValueColumnTemplateSelector x:Key="MyValueColumnTemplateSelector"  
    DisplayStringRendererTemplate="{StaticResource DisplayStringRendererTemplate}"  
    DisplayBooleanRendererTemplate="{StaticResource DisplayBooleanRendererTemplate}"  
    StringEditorTemplate="{StaticResource StringEditorTemplate}"  
    NumberEditorTemplate="{StaticResource NumberEditorTemplate}"  
    BooleanEditorTemplate="{StaticResource BooleanEditorTemplate}"  
    EnumEditorTemplate="{StaticResource EnumEditorTemplate}"  
    RefNodePathEditorTemplate="{StaticResource RefNodePathEditorTemplate}"/\>

\<DataGridTemplateColumn Header="Value" Width="2\*"\>  
    \<DataGridTemplateColumn.CellTemplate\>  
        \<DataTemplate\>  
            \<ContentControl Content="{Binding}"  
                            ContentTemplateSelector="{StaticResource MyValueColumnTemplateSelector}"/\>  
        \</DataTemplate\>  
    \</DataGridTemplateColumn.CellTemplate\>  
\</DataGridTemplateColumn\>

\<Style x:Key="DisplayValueTextBlockStyle" TargetType="TextBlock" BasedOn="{StaticResource LeftAlignedTextBlock}"\>  
    \<Setter Property="ToolTipService.ToolTip" Value="{Binding ValidationErrorMessage}"/\>  
    \<Style.Triggers\>  
        \<DataTrigger Binding="{Binding IsValid}" Value="False"\>  
            \<Setter Property="Foreground" Value="Red"/\>  
        \</DataTrigger\>  
        \<DataTrigger Binding="{Binding IsNodeReadOnly}" Value="True"\>  
            \<Setter Property="FontStyle" Value="Italic"/\>  
            \<Setter Property="Foreground" Value="Gray"/\>  
        \</DataTrigger\>  
        \<DataTrigger Binding="{Binding IsSchemaOnlyNode}" Value="True"\>  
            \<Setter Property="Foreground" Value="Gray"/\> \<Setter Property="FontStyle" Value="Italic"/\>  
        \</DataTrigger\>  
        \<DataTrigger Binding="{Binding IsRefLinkToExternalOrMissing}" Value="True"\>  
            \<Setter Property="Foreground" Value="DarkMagenta"/\>  
        \</DataTrigger\>  
    \</Style.Triggers\>  
\</Style\>

\<Style x:Key="EditingTextBoxStyle" TargetType="TextBox"\>  
    \<Setter Property="BorderThickness" Value="{Binding IsValid, Converter={StaticResource BoolToErrorBorderThicknessConverter}, FallbackValue=1}"/\>  
    \<Setter Property="BorderBrush" Value="Red"/\> \</Style\>  
\<Style x:Key="EditingComboBoxStyle" TargetType="ComboBox" BasedOn="{StaticResource EditingTextBoxStyle}"/\> \`\`\`  
\* You'll need \`JsonElementToBoolConverter\`, \`StringToNullableBoolConverter\`, \`BoolToErrorBorderThicknessConverter\`.  
\* \`UpdateSourceTrigger=LostFocusOrEnter\` for ComboBox text is a common pattern requiring a small attached behavior or custom binding.

\*\*3. \`ValueColumnTemplateSelector\` (C\# Class):\*\*

This class decides which \`DataTemplate\` (renderer or editor) to use.

\`\`\`csharp  
// \--- File: JsonConfigEditor.Wpf.Selectors.ValueColumnTemplateSelector.cs \---  
using System.Windows;  
using System.Windows.Controls;  
using JsonConfigEditor.Core.Dom;  
using JsonConfigEditor.Core.Schema; // For SchemaNode  
using JsonConfigEditor.Wpf.ViewModels;

namespace JsonConfigEditor.Wpf.Selectors  
{  
    public class ValueColumnTemplateSelector : DataTemplateSelector  
    {  
        // Templates for Display Mode (Renderers)  
        public DataTemplate? DisplayStringRendererTemplate { get; set; }  
        public DataTemplate? DisplayBooleanRendererTemplate { get; set; }  
        // Add more display templates if needed for specific types (e.g., numbers with specific formatting)

        // Templates for Edit Mode (Editors)  
        public DataTemplate? StringEditorTemplate { get; set; }  
        public DataTemplate? NumberEditorTemplate { get; set; }  
        public DataTemplate? BooleanEditorTemplate { get; set; }  
        public DataTemplate? EnumEditorTemplate { get; set; }  
        public DataTemplate? RefNodePathEditorTemplate { get; set; }  
        // FallbackEditorTemplate can be StringEditorTemplate

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)  
        {  
            if (item is not DataGridRowItemViewModel vm)  
                return base.SelectTemplate(item, container);

            if (\!vm.IsInEditMode || \!vm.IsEditable) // DISPLAY MODE  
            {  
                // Select appropriate renderer based on type  
                if (vm.AssociatedSchemaNode?.ClrType \== typeof(bool) ||  
                    (vm.DomNode is ValueNode vnBool && vnBool.Value.ValueKind \== JsonValueKind.True || vnBool.Value.ValueKind \== JsonValueKind.False))  
                {  
                    return DisplayBooleanRendererTemplate ?? DisplayStringRendererTemplate;  
                }  
                // Add other specific renderers if needed (e.g. for dates, custom formatted numbers)  
                return DisplayStringRendererTemplate; // Default display renderer  
            }  
            else // EDIT MODE  
            {  
                if (vm.DomNode is RefNode)  
                {  
                    return RefNodePathEditorTemplate ?? StringEditorTemplate;  
                }

                SchemaNode? schema \= vm.AssociatedSchemaNode;  
                Type? clrType \= schema?.ClrType;

                if (clrType \== null && vm.DomNode is ValueNode vn) // Unschematized, infer from JsonValueKind  
                {  
                    switch (vn.Value.ValueKind)  
                    {  
                        case JsonValueKind.True:  
                        case JsonValueKind.False:  
                            return BooleanEditorTemplate ?? StringEditorTemplate;  
                        case JsonValueKind.Number:  
                            return NumberEditorTemplate ?? StringEditorTemplate;  
                        case JsonValueKind.String:  
                            return StringEditorTemplate;  
                        default: // Null, Undefined  
                            return StringEditorTemplate; // Allow editing null as a string initially  
                    }  
                }

                if (clrType \== typeof(bool) || clrType \== typeof(bool?))  
                {  
                    return BooleanEditorTemplate ?? StringEditorTemplate;  
                }  
                if (clrType \!= null && clrType.IsEnum || (schema?.AllowedValues \!= null && schema.AllowedValues.Any()))  
                {  
                    return EnumEditorTemplate ?? StringEditorTemplate;  
                }  
                if (clrType \== typeof(int) || clrType \== typeof(int?) ||  
                    clrType \== typeof(double) || clrType \== typeof(double?) ||  
                    /\* other numeric types \*/ )  
                {  
                    return NumberEditorTemplate ?? StringEditorTemplate;  
                }  
                // Default to string editor for other types (like string itself, or unknown)  
                return StringEditorTemplate;  
            }  
        }  
    }  
}

**Implementation Steps:**

1. **Define `DataTemplate`s:** Create the XAML `DataTemplate`s for each common renderer and editor type in your `Window.Resources` or a `ResourceDictionary`.  
2. **Implement `ValueColumnTemplateSelector`:** Write the C\# logic to choose the correct template.  
3. **Update `DataGrid` XAML:** Modify the "Value" column's `CellTemplate` to use the `ContentControl` with your `ValueColumnTemplateSelector`.  
4. **`DataGridRowItemViewModel` Logic:**  
   * Ensure `EditValue` is correctly populated when `IsInEditMode` becomes `true` (converting from `DomNode.Value` or `SchemaNode.DefaultValue` to string).  
   * Enhance `CommitEdit()`:  
     * Parse `EditValue` (string) back into the appropriate C\# type based on `AssociatedSchemaNode.ClrType`.  
     * Perform validation against schema constraints using this parsed CLR value.  
     * If valid, create a new `JsonElement` from the parsed CLR value and update `DomNode.Value`.  
     * For `RefNode`s, `EditValue` is the path string; update `RefNode.ReferencePath`.  
5. **Converters:** Implement any necessary `IValueConverter`s (e.g., `JsonElementToBoolConverter`, `StringToNullableBoolConverter`, `BoolToErrorBorderThicknessConverter`).  
6. **Commands:** Ensure `ConfirmEditCommand` and `CancelEditCommand` in `MainViewModel` correctly call `vm.CommitEdit()` and `vm.CancelEdit()` respectively.

This approach provides a robust and extensible way to handle different data types for display and editing directly within the `DataGrid`, adhering to the defined schema. Start with string and boolean types, then incrementally add support for numbers, enums, and `RefNode`s.

# Flat table navigation using mouse and keyboard

Okay, let's outline how to implement the flat table navigation using both mouse and keyboard, based on the established specification. This primarily involves handling events on the `DataGrid` and translating them into actions within your `MainViewModel` and `DataGridRowItemViewModel`.

**Core Requirements from Specification:**

* **Row Selection**: Full row should be selectable (Spec Section 2.9).  
* **Mouse Interaction**:  
  * `Ctrl+Mouse Left Click` for multi-selection (Spec Section 2.9).  
  * Double-clicking a `ValueNode`'s value activates edit mode (Spec Section 2.4).  
  * Right-clicking opens a context menu (Spec Section 2.15).  
* **Keyboard Navigation (Browse Mode \- Spec Section 2.9):**  
  * `Up/Down` arrow keys: Move selected row up/down.  
  * `PageUp/PageDown`: Move selection one page up/down, ensuring the selected line is visible.  
  * `Left/Right` arrow keys: Collapse/expand the currently selected `ObjectNode` or `ArrayNode`.  
  * `Delete` key: Deletes selected item(s).  
  * `Insert` key: Inserts a new item above the current row (behavior depends on context).  
  * `Ctrl+C` / `Ctrl+Insert`: Copy.  
  * `Ctrl+V` / `Shift+Insert`: Paste (for array nodes).  
  * `Shift+Up/Down arrow`: Enable multi-selection.  
  * `Tab` (Browse Mode): Jumps to the next row like Arrow Down, does not enter edit mode.  
* **Keyboard Navigation (Edit Mode \- Spec Section 2.9.1):**  
  * `Enter`: Confirms edit.  
  * `Esc`: Cancels edit.  
  * `Tab`: Confirms edit, jumps to the next editable cell on the same level or next row, starts editing if applicable; otherwise, selects the next row in browse mode.  
* **Edit Mode Activation (Spec Section 2.4):**  
  * Double-clicking value or pressing `Enter` on the row.

**Implementation Strategy:**

**1\. `MainViewModel` \- Managing Selection and Commands:**

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
// ... (previous using statements and class structure) ...

public class MainViewModel : ViewModelBase  
{  
    // ... (FlatItemsSource, Commands, etc.) ...

    private DataGridRowItemViewModel? \_selectedRowItem;  
    /// \<summary\>  
    /// Gets or sets the currently selected item in the DataGrid.  
    /// Bound to DataGrid.SelectedItem.  
    /// \</summary\>  
    public DataGridRowItemViewModel? SelectedRowItem  
    {  
        get \=\> \_selectedRowItem;  
        set \=\> SetProperty(ref \_selectedRowItem, value);  
    }

    // Store multiple selected items if your DataGrid supports it (SelectionMode="Extended")  
    private ObservableCollection\<DataGridRowItemViewModel\> \_selectedItemsList \= new ObservableCollection\<DataGridRowItemViewModel\>();  
    /// \<summary\>  
    /// Gets the list of currently selected items in the DataGrid when multi-select is enabled.  
    /// This might require careful binding or handling of DataGrid.SelectedItems.  
    /// \</summary\>  
    public ObservableCollection\<DataGridRowItemViewModel\> SelectedItemsList \=\> \_selectedItemsList;

    // \--- Commands for Keyboard Actions (Many already defined in previous milestones) \---  
    // DeleteNodeCommand  
    // CopyCommand  
    // PasteCommand  
    // InsertNewItemCommand (might be a generic command that calls specific logic based on context)  
    // ToggleExpandCollapseCommand (for Left/Right arrow keys)  
    // NavigatePageUpCommand  
    // NavigatePageDownCommand  
    // NavigateNextRowCommand (for Tab in browse mode, and Arrow Down)  
    // NavigatePreviousRowCommand (for Arrow Up)

    public MainViewModel(/\*...services...\*/)  
    {  
        // ...  
        // ToggleExpandCollapseCommand \= new RelayCommand\<DataGridRowItemViewModel\>(ExecuteToggleExpandCollapse, CanExecuteToggleExpandCollapse);  
        // NavigateNextRowCommand \= new RelayCommand(ExecuteNavigateNextRow, CanExecuteNavigateNextRow);  
        // ... other command initializations ...  
    }

    private bool CanExecuteToggleExpandCollapse(DataGridRowItemViewModel? item) \=\> item \!= null && item.IsExpandable;  
    private void ExecuteToggleExpandCollapse(DataGridRowItemViewModel? item)  
    {  
        if (item \!= null && item.IsExpandable)  
        {  
            item.IsExpanded \= \!item.IsExpanded; // This triggers OnExpansionChanged \-\> RefreshDisplayList  
        }  
    }

    private bool CanExecuteNavigateNextRow() \=\> SelectedRowItem \!= null && FlatItemsSource.IndexOf(SelectedRowItem) \< FlatItemsSource.Count \- 1;  
    private void ExecuteNavigateNextRow()  
    {  
        if (SelectedRowItem \== null) {  
            if (FlatItemsSource.Any()) SelectedRowItem \= FlatItemsSource.First();  
            return;  
        }  
        int currentIndex \= FlatItemsSource.IndexOf(SelectedRowItem);  
        if (currentIndex \< FlatItemsSource.Count \- 1\)  
        {  
            SelectedRowItem \= FlatItemsSource\[currentIndex \+ 1\];  
            // RequestScrollToItem(SelectedRowItem); // Ensure it's visible  
        }  
    }  
    // Similar for ExecuteNavigatePreviousRow, PageUp, PageDown  
    // PageUp/Down will need to calculate an index approximately one "page" away  
    // and then select that item and ensure it's scrolled into view.  
}

**2\. `DataGrid` XAML \- Event Handling and Bindings:**

You'll handle keyboard events primarily at the `DataGrid` level. Mouse events like double-click can be handled on cells or rows.

XML  
\<DataGrid ItemsSource="{Binding FlatItemsSource}"  
          SelectedItem="{Binding SelectedRowItem, Mode=TwoWay}"  
          SelectionMode="Extended" VirtualizingStackPanel.IsVirtualizing="True"  
          VirtualizingStackPanel.VirtualizationMode="Recycling"  
          EnableRowVirtualization="True"  
          EnableColumnVirtualization="True"  
          AutoGenerateColumns="False"  
          CanUserAddRows="False"  
          HeadersVisibility="Column"  
          x:Name="MainDataGrid"\> \<DataGrid.InputBindings\>  
        \<KeyBinding Key="Up" Command="{Binding NavigatePreviousRowCommand}"/\>  
        \<KeyBinding Key="Down" Command="{Binding NavigateNextRowCommand}"/\>  
        \<KeyBinding Key="PageUp" Command="{Binding NavigatePageUpCommand}"/\>  
        \<KeyBinding Key="PageDown" Command="{Binding NavigatePageDownCommand}"/\>

        \<KeyBinding Key="Left" Command="{Binding ToggleExpandCollapseCommand}" CommandParameter="{Binding SelectedItem, ElementName=MainDataGrid}"/\>  
        \<KeyBinding Key="Right" Command="{Binding ToggleExpandCollapseCommand}" CommandParameter="{Binding SelectedItem, ElementName=MainDataGrid}"/\> \<KeyBinding Key="Delete" Command="{Binding DeleteNodeCommand}" CommandParameter="{Binding SelectedItems, ElementName=MainDataGrid}"/\> \<KeyBinding Key="Insert" Command="{Binding InsertNewItemCommand}" CommandParameter="{Binding SelectedItem, ElementName=MainDataGrid}"/\>

        \<KeyBinding Key="Enter" Command="{Binding ActivateEditModeCommand}" CommandParameter="{Binding SelectedItem, ElementName=MainDataGrid}"/\>  
        \</DataGrid.InputBindings\>

    \<DataGrid.ContextMenu\>  
        \<MenuItem Header="Copy" Command="{Binding CopyCommand}" CommandParameter="{Binding SelectedItems, ElementName=MainDataGrid}"/\>  
        \</DataGrid.ContextMenu\>

    \<DataGrid.Columns\>  
        \<DataGridTemplateColumn Header="Name" Width="\*"\>  
            \<DataGridTemplateColumn.CellTemplate\>  
                \<DataTemplate DataType="{x:Type vm:DataGridRowItemViewModel}"\>  
                    \<StackPanel Orientation="Horizontal" Background="Transparent"\> \<StackPanel.ContextMenu\>  
                            \<ContextMenu DataContext="{Binding PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}"\>  
                                \<MenuItem Header="Copy" Command="{Binding ParentViewModel.CopyCommand}" CommandParameter="{Binding}"/\>  
                                \</ContextMenu\>  
                        \</StackPanel.ContextMenu\>  
                    \</StackPanel\>  
                \</DataTemplate\>  
            \</DataGridTemplateColumn.CellTemplate\>  
        \</DataGridTemplateColumn\>

        \<DataGridTemplateColumn Header="Value" Width="2\*"\>  
            \<DataGridTemplateColumn.CellTemplate\>  
                \<DataTemplate DataType="{x:Type vm:DataGridRowItemViewModel}"\>  
                    \<ContentControl Content="{Binding}"  
                                    ContentTemplateSelector="{StaticResource MyValueColumnTemplateSelector}"  
                                    Focusable="False"\> \<ContentControl.ContextMenu\>  
                             \<ContextMenu DataContext="{Binding PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}"\>  
                                \<MenuItem Header="Copy" Command="{Binding ParentViewModel.CopyCommand}" CommandParameter="{Binding}"/\>  
                                \</ContextMenu\>  
                        \</ContentControl.ContextMenu\>  
                        \<i:Interaction.Triggers\> \<i:EventTrigger EventName="MouseDoubleClick"\>  
                                \<i:InvokeCommandAction Command="{Binding ParentViewModel.ActivateEditModeCommand}" CommandParameter="{Binding}"/\>  
                            \</i:EventTrigger\>  
                        \</i:Interaction.Triggers\>  
                    \</ContentControl\>  
                \</DataTemplate\>  
            \</DataGridTemplateColumn.CellTemplate\>  
        \</DataGridTemplateColumn\>  
    \</DataGrid.Columns\>  
\</DataGrid\>

* You'll need to add `System.Windows.Interactivity.dll` (or `Microsoft.Xaml.Behaviors.Wpf` for newer projects) to use `EventTrigger` for `MouseDoubleClick`.  
* The `DataContext` for `ContextMenu` needs to be carefully set to ensure commands from `MainViewModel` can be reached, often by proxying through the row's `DataContext` (`DataGridRowItemViewModel`) which has a reference to `MainViewModel` (`ParentViewModel`).

**3\. Handling Tab Key Navigation (Complex):**

Tab key behavior is particularly nuanced:

* **Browse Mode Tab (Spec Section 2.9.1):** "If in Browse mode, `Tab` should jump to the next row just like pressing an arrow down key. It should not enter edit mode."  
  * This often requires handling `PreviewKeyDown` on the `DataGrid`. If `Tab` is pressed and no cell is in edit mode, you'd manually change `SelectedRowItem` and mark the event as handled (`e.Handled = true;`) to prevent default focus traversal.  
* **Editing Mode Tab (Spec Section 2.9.1):** "If in in-place editing mode... `Tab` shall: Confirm the edit. Jump to the next row on the same level... Start editing it... If no in-place edit is possible or if at the last property... the next row shall be selected in Browse mode."  
  * When an editor control (e.g., `TextBox`) has focus:  
    1. Handle `PreviewKeyDown` for `Tab` on the editor.  
    2. Call `currentVm.CommitEdit()`.  
    3. Determine the "next row on the same level" or the next logical row. This can be complex in a flat list representing a hierarchy.  
    4. Select that next row's VM.  
    5. If it's editable, set `nextVm.IsInEditMode = true` and programmatically focus its editor.  
    6. If not editable or at the end, select it in browse mode.  
    7. Set `e.Handled = true;`.

**4\. Multi-Selection (Spec Section 2.9):**

* **`DataGrid.SelectionMode="Extended"`** enables this.  
* **`Shift+Up/Down`**: Default `DataGrid` behavior usually handles this for extended selection.  
* **`Ctrl+Mouse Left Click`**: Default `DataGrid` behavior for toggling selection in extended mode.  
* Your commands (`DeleteNodeCommand`, `CopyCommand`) need to be aware that `DataGrid.SelectedItems` (which you might mirror in `MainViewModel.SelectedItemsList`) can contain multiple items. Iterate through them to perform the action.  
  * Pasting (`Ctrl+V`) with multiple rows selected in an array is specified to fail with a status bar message (Clarification 7).

**5\. Ensuring Visibility (PageUp/PageDown, Jump to Definition, Search):**

After changing `SelectedRowItem` programmatically, you need to ensure the `DataGrid` scrolls the new item into view.  
 C\#  
// In MainViewModel or a helper service  
public void RequestScrollToItem(DataGridRowItemViewModel item)  
{  
    // This event would be subscribed to by the View (e.g., MainWindow.xaml.cs)  
    // The View's handler would then call DataGrid.ScrollIntoView(item).  
    ScrollToItemRequested?.Invoke(item);  
}  
public event Action\<DataGridRowItemViewModel\>? ScrollToItemRequested;

// In MainWindow.xaml.cs  
// public MainWindow()  
// {  
//     InitializeComponent();  
//     if (DataContext is MainViewModel vm)  
//     {  
//         vm.ScrollToItemRequested \+= (itemToScroll) \=\>  
//         {  
//             if (itemToScroll \!= null) MainDataGrid.ScrollIntoView(itemToScroll);  
//         };  
//     }  
// }

* 

**Implementation Steps Summary:**

1. **ViewModel Properties:** Ensure `MainViewModel` has `SelectedRowItem` and potentially `SelectedItemsList` for multi-select.  
2. **Basic Keyboard Commands in `MainViewModel`:** Implement `ICommand`s for Up, Down, Left, Right (toggle expand), Delete, Insert, Enter (activate edit).  
3. **`DataGrid` `InputBindings`:** Bind keyboard shortcuts in browse mode to these commands.  
4. **Mouse Double-Click:** Use `EventTrigger` or similar to call `ActivateEditModeCommand`.  
5. **Context Menu:** Define in XAML and bind its `MenuItem`s to `ICommand`s in `MainViewModel`, passing the selected item(s) as parameters.  
6. **Editor `InputBindings`:** Add `Enter` (confirm) and `Esc` (cancel) `KeyBinding`s to your editor `DataTemplate`s.  
7. **Tab Navigation (Advanced):**  
   * Handle `DataGrid.PreviewKeyDown` for browse mode Tab.  
   * Handle `PreviewKeyDown` on editor controls for edit mode Tab. This requires careful coordination to commit, find the next target, and activate its edit mode.  
8. **Multi-Selection Logic:** Ensure commands like Delete and Copy can iterate over `DataGrid.SelectedItems`.  
9. **ScrollIntoView:** Implement the `RequestScrollToItem` event mechanism.

This is a complex set of interactions. Start with basic single-row selection and keyboard navigation (Up, Down, Enter to edit, Esc to cancel edit) and then incrementally add multi-select, more advanced keyboard actions, and refine Tab key behavior.

# Tab navigation

Okay, implementing custom Tab key navigation, especially the two distinct behaviors for "Browse Mode" and "Editing Mode" specified in **Spec Section 2.9.1**, often requires handling the `PreviewKeyDown` event. This is because you need to intercept the Tab key before the default WPF focus traversal logic takes over.

Here's a conceptual outline of how you could implement this in your `MainWindow.xaml.cs` (or an attached behavior for better MVVM separation if you prefer). This assumes your `DataGrid` is named `MainDataGrid` and its `DataContext` is your `MainViewModel`.

**Core Idea:**

* In `PreviewKeyDown` for the `DataGrid`:  
  * If `Tab` is pressed:  
    * Check if any cell is currently in edit mode.  
    * If **not** in edit mode (Browse Mode Tab): Implement "jump to next row" logic and set `e.Handled = true`.  
    * If **in** edit mode (Editing Mode Tab): This is more complex. The editor control itself (e.g., `TextBox` inside a cell) should ideally handle its own Tab key press to confirm, then notify the `MainViewModel` to move to the next appropriate cell/row and potentially start editing there.

**1\. Browse Mode Tab Navigation (`DataGrid.PreviewKeyDown`):**

This handles the case where the `DataGrid` has focus, but no specific cell editor is active.

C\#  
// \--- In MainWindow.xaml.cs \---  
using System.Windows.Input; // For KeyEventArgs, Key  
using JsonConfigEditor.Wpf.ViewModels; // For MainViewModel, DataGridRowItemViewModel

public partial class MainWindow : Window  
{  
    public MainWindow()  
    {  
        InitializeComponent();  
        MainDataGrid.PreviewKeyDown \+= MainDataGrid\_PreviewKeyDown\_BrowseMode;  
    }

    private void MainDataGrid\_PreviewKeyDown\_BrowseMode(object sender, KeyEventArgs e)  
    {  
        if (e.Key \== Key.Tab)  
        {  
            var mainViewModel \= DataContext as MainViewModel;  
            if (mainViewModel \== null || mainViewModel.IsAnyCellInEditMode()) // IsAnyCellInEditMode() is a conceptual check  
            {  
                // If in edit mode, let the editor's Tab handling take over (see next section)  
                // or if no VM, do nothing.  
                return;  
            }

            // BROWSE MODE TAB LOGIC (Spec Section 2.9.1)  
            // "If in Browse mode, Tab should jump to the next row just like pressing an arrow down key.  
            //  It should not enter edit mode."

            if (mainViewModel.SelectedRowItem \== null && mainViewModel.FlatItemsSource.Any())  
            {  
                mainViewModel.SelectedRowItem \= mainViewModel.FlatItemsSource.First();  
                mainViewModel.RequestScrollToItem(mainViewModel.SelectedRowItem); // Ensure visible  
                e.Handled \= true;  
                return;  
            }

            int currentIndex \= mainViewModel.FlatItemsSource.IndexOf(mainViewModel.SelectedRowItem\!);  
            DataGridRowItemViewModel? nextItem \= null;

            if (Keyboard.Modifiers \== ModifierKeys.Shift) // Shift \+ Tab (navigate backwards)  
            {  
                if (currentIndex \> 0\)  
                {  
                    nextItem \= mainViewModel.FlatItemsSource\[currentIndex \- 1\];  
                }  
            }  
            else // Tab (navigate forwards)  
            {  
                if (currentIndex \< mainViewModel.FlatItemsSource.Count \- 1\)  
                {  
                    nextItem \= mainViewModel.FlatItemsSource\[currentIndex \+ 1\];  
                }  
            }

            if (nextItem \!= null)  
            {  
                mainViewModel.SelectedRowItem \= nextItem;  
                mainViewModel.RequestScrollToItem(nextItem); // Ensure the new item is visible  
            }  
            e.Handled \= true; // Prevent default Tab focus traversal  
        }  
    }  
}

// \--- In MainViewModel.cs \---  
public class MainViewModel : ViewModelBase  
{  
    // ... (existing code) ...

    /// \<summary\>  
    /// Conceptual property/method to check if any cell is actively being edited.  
    /// This might involve checking IsInEditMode on a \_currentlyFocusedRowItemVm or similar.  
    /// \</summary\>  
    public bool IsAnyCellInEditMode()  
    {  
        // If you have a reference to the VM of the row currently in edit mode:  
        // return \_activeEditingRowVm \!= null && \_activeEditingRowVm.IsInEditMode;  
        // Alternatively, iterate FlatItemsSource, but that's less efficient for a quick check.  
        // This state might be better managed by tracking which VM is in edit mode.  
        var editingVm \= FlatItemsSource.FirstOrDefault(vm \=\> vm.IsInEditMode);  
        return editingVm \!= null;  
    }

    // ...  
}

**2\. Editing Mode Tab Navigation (Handled by Editor Controls and `MainViewModel`):**

This part is trickier because the `Tab` key event originates from within the active editor control (e.g., `TextBox`, `ComboBox`) inside the `DataGrid` cell.

**Specification (Spec Section 2.9.1):**

* "If in in-place editing mode (the right cell 'value' column is focused), `Tab` shall:  
  * Confirm the edit.  
  * Jump to the next row on the same level (e.g., next property of the object, next item of the array).  
  * Start editing it (only if in-place edit is supported).  
  * If no in-place edit is possible or if at the last property of an object node/last item of an array node, the next row shall be selected in Browse mode."

**Implementation Steps for Editing Mode Tab:**

**A. Editor Control's `PreviewKeyDown`:** Each editor template (`StringEditorTemplate`, `NumberEditorTemplate`, etc.) would have its control (e.g., `TextBox`) handle `PreviewKeyDown`.

 XML  
\<DataTemplate x:Key="StringEditorTemplate" DataType="{x:Type vm:DataGridRowItemViewModel}"\>  
    \<TextBox Text="{Binding EditValue, UpdateSourceTrigger=LostFocus}"  
             VerticalAlignment="Center"  
             Style="{StaticResource EditingTextBoxStyle}"  
             PreviewKeyDown="Editor\_PreviewKeyDown"\> \<TextBox.InputBindings\>  
            \<KeyBinding Key="Enter" Command="{Binding ParentViewModel.ConfirmEditAndStayCommand}" CommandParameter="{Binding}"/\> \<KeyBinding Key="Escape" Command="{Binding ParentViewModel.CancelEditCommand}" CommandParameter="{Binding}"/\>  
        \</TextBox.InputBindings\>  
    \</TextBox\>  
\</DataTemplate\>  
 C\#  
// \--- In relevant View's code-behind (e.g., MainWindow.xaml.cs) \---  
private void Editor\_PreviewKeyDown(object sender, KeyEventArgs e)  
{  
    if (e.Key \== Key.Tab)  
    {  
        var control \= sender as FrameworkElement;  
        var currentVm \= control?.DataContext as DataGridRowItemViewModel;  
        var mainVm \= DataContext as MainViewModel;

        if (currentVm \!= null && mainVm \!= null)  
        {  
            e.Handled \= true; // We will handle the Tab navigation

            // 1\. Confirm the edit on the current VM  
            bool commitSuccess \= currentVm.CommitEdit();  
            currentVm.IsInEditMode \= false; // Exit edit mode for current cell regardless of commit success for Tab behavior

            if (commitSuccess) // Only proceed to navigate if commit was at least partially successful  
            {  
                // 2\. Determine the next item to navigate to and potentially edit  
                DataGridRowItemViewModel? nextTargetVm \= mainVm.FindNextTabTarget(currentVm, Keyboard.Modifiers \== ModifierKeys.Shift);

                if (nextTargetVm \!= null)  
                {  
                    mainVm.SelectedRowItem \= nextTargetVm; // Select it  
                    mainVm.RequestScrollToItem(nextTargetVm); // Scroll to it

                    // 3\. Start editing if possible  
                    if (nextTargetVm.IsEditable)  
                    {  
                        nextTargetVm.IsInEditMode \= true;  
                        // Focus the new editor (this is the hard part from code-behind,  
                        // often requires waiting for UI to update then finding the control)  
                        Dispatcher.BeginInvoke(new Action(() \=\>  
                        {  
                            // Try to find the cell and then the editor within it to focus.  
                            // This usually involves DataGrid methods to get the cell container,  
                            // then VisualTreeHelper to find the editor.  
                            FocusEditorInCell(nextTargetVm);  
                        }), System.Windows.Threading.DispatcherPriority.Background);  
                    }  
                }  
                // If nextTargetVm is null, Tab effectively "escapes" the grid or goes to next focusable element  
                // (because e.Handled \= true might prevent this if not careful, or a subsequent control gets focus)  
            }  
            // If commit failed due to invalid format, spec says focus returns to current editor.  
            // The CommitEdit should handle this by not setting IsInEditMode to false.  
            // But for Tab, we explicitly exit edit mode then decide.  
            // If commitSuccess is false due to formatting, perhaps we should not navigate.  
            // Let's assume CommitEdit sets IsInEditMode \= false if validation error is NOT a parsing/format error.  
        }  
    }  
}

private void FocusEditorInCell(DataGridRowItemViewModel vmToFocus)  
{  
    // This is a known tricky part in WPF.  
    // 1\. Ensure the DataGrid has updated and the row for vmToFocus is present.  
    MainDataGrid.UpdateLayout(); // Force layout update  
    var row \= MainDataGrid.ItemContainerGenerator.ContainerFromItem(vmToFocus) as DataGridRow;  
    if (row \!= null)  
    {  
        // 2\. Find the cell (e.g., the "Value" column, usually index 1 if Name is 0).  
        DataGridCellsPresenter presenter \= GetVisualChild\<DataGridCellsPresenter\>(row);  
        if (presenter \== null) return;

        DataGridCell cell \= presenter.ItemContainerGenerator.ContainerFromIndex(1) as DataGridCell; // Assuming value column is index 1  
        if (cell \!= null)  
        {  
            // 3\. Find the editor control within the cell's template.  
            // This depends on your DataTemplate structure.  
            // If using ContentControl with DataTemplateSelector:  
            ContentPresenter contentPresenter \= GetVisualChild\<ContentPresenter\>(cell);  
            if (contentPresenter \!= null)  
            {  
                 // Wait for template to apply  
                contentPresenter.ApplyTemplate();  
                FrameworkElement editor \= VisualTreeHelper.GetChild(contentPresenter, 0\) as FrameworkElement;

                // If your editor template directly contains the TextBox/ComboBox:  
                // FrameworkElement editor \= GetVisualChild\<TextBox\>(cell) ?? (FrameworkElement)GetVisualChild\<ComboBox\>(cell) ?? GetVisualChild\<CheckBox\>(cell);

                editor?.Focus();  
                if (editor is TextBox tb) tb.SelectAll();  
            }  
        }  
    }  
}

// Helper to find visual children (standard WPF utility)  
public static T? GetVisualChild\<T\>(DependencyObject parent) where T : Visual  
{  
    T? child \= default(T);  
    int numVisuals \= VisualTreeHelper.GetChildrenCount(parent);  
    for (int i \= 0; i \< numVisuals; i++)  
    {  
        Visual v \= (Visual)VisualTreeHelper.GetChild(parent, i);  
        child \= v as T;  
        if (child \== null)  
        {  
            child \= GetVisualChild\<T\>(v);  
        }  
        if (child \!= null)  
        {  
            break;  
        }  
    }  
    return child;  
}

* 

**B. `MainViewModel` \- Logic to find the next Tabbable item:**

 C\#  
// \--- In MainViewModel.cs \---  
public DataGridRowItemViewModel? FindNextTabTarget(DataGridRowItemViewModel currentVm, bool navigateBackwards)  
{  
    if (currentVm \== null) return null;

    int startIndex \= FlatItemsSource.IndexOf(currentVm);  
    if (startIndex \== \-1) return null;

    int direction \= navigateBackwards ? \-1 : 1;  
    int count \= FlatItemsSource.Count;

    // Try to find next on the same level first  
    int currentDepth \= currentVm.DomNode?.Depth ?? currentVm.SchemaContextNode\_Depth\_If\_SchemaOnly; // Need a way to get depth for schema-only from VM

    for (int i \= startIndex \+ direction; i \>= 0 && i \< count; i \+= direction)  
    {  
        var nextVm \= FlatItemsSource\[i\];  
        int nextDepth \= nextVm.DomNode?.Depth ?? nextVm.SchemaContextNode\_Depth\_If\_SchemaOnly;

        if (nextDepth \== currentDepth && nextVm.IsEditable) // Found next sibling on same level that's editable  
        {  
            return nextVm;  
        }  
        if (nextDepth \< currentDepth) // Moved out of current level, stop searching on this level  
        {  
            break;  
        }  
    }

    // If no editable sibling on same level, or at the end/beginning of that level,  
    // move to the next/previous visible row in the entire grid (Spec: "the next row shall be selected in Browse mode").  
    // For simplicity, we'll just find the next/previous editable item in the whole list.  
    // The spec is a bit ambiguous: "next row on the same level... If no in-place edit is possible or if at the last property... the next row shall be selected in Browse mode."  
    // This implies if the next item (even if on different level) is not editable, it's just selected.  
    // If it \*is\* editable, it should start editing.

    for (int i \= startIndex \+ direction; i \>= 0 && i \< count; i \+= direction)  
    {  
        var nextVm \= FlatItemsSource\[i\];  
        // If the spec means "next sequential row, and if it's editable, edit it":  
        if (nextVm.IsEditable) return nextVm; // Start editing this one  
        else return nextVm; // Select this one in browse mode  
    }

    // If truly at the start/end of the grid and no further item found.  
    return null; // Or, could return currentVm to just select it in browse mode.  
}

// DataGridRowItemViewModel needs to expose its depth (e.g., via Indentation.Left or a Depth property)  
// For schema-only nodes, this depth was passed during VM creation.

*  The `SchemaContextNode_Depth_If_SchemaOnly` is a placeholder for how you'd get the conceptual depth of a schema-only VM if its `DomNode` is null. This was part of the `DataGridRowItemViewModel` constructor for schema-only nodes.

**Challenges & Refinements:**

* **Focus Management (`FocusEditorInCell`):** Programmatically setting focus to a control inside a newly materialized `DataGrid` cell/row is notoriously tricky in WPF due to virtualization and the timing of UI element creation. `Dispatcher.BeginInvoke` with `DispatcherPriority.Background` (or even `Input`) is often necessary. You might need to subscribe to `LayoutUpdated` or `StatusChanged` on `ItemContainerGenerator` in some cases.  
* **`IsAnyCellInEditMode()`:** `MainViewModel` needs a reliable way to know if an edit is in progress to distinguish Browse Mode Tab from Editing Mode Tab. This could be a property like `ActivelyEditingItemVm` in `MainViewModel` that gets set when `IsInEditMode` on a `DataGridRowItemViewModel` becomes true and cleared when it becomes false.  
* **`CommitEdit()` Behavior for Tab:** When Tab confirms an edit, if the value is syntactically invalid (e.g., "abc" for a number), the spec says "Focus shall be returned back to the in-place edit field". Your `CommitEdit()` needs to handle this, and if it returns `false` due to such an error, the Tab navigation logic in `Editor_PreviewKeyDown` should probably *not* move to the next cell.  
* **MVVM Purity vs. Code-Behind:** Extensive focus management and `PreviewKeyDown` handling can lead to more code in the View's code-behind. Attached Behaviors or custom controls can help move some of this logic out if strict MVVM is desired, but for complex UI interactions like this, some code-behind is often pragmatic.  
* **Clarity of "Next row on the same level":** In a flat list representing a hierarchy, "same level" means same depth. The provided `FindNextTabTarget` attempts this. If it can't find an *editable* one on the same level, it then looks for the *next sequential visible and editable row*. If the next sequential row isn't editable, it's just selected.

This implementation requires careful coordination between the View (XAML and code-behind for event handling), the `DataGridRowItemViewModel` (for its state), and the `MainViewModel` (for orchestrating navigation and commands). Start simple and test each part of the Tab behavior thoroughly.

# Multi-select

Here's how you would implement multi-select functionality in your WPF `DataGrid` for the JSON Configuration Editor:

**1\. Enable Multi-Select in `DataGrid` (XAML)**

This is the foundational step. WPF's `DataGrid` has built-in support for multi-selection modes.

XML  
\<DataGrid ItemsSource="{Binding FlatItemsSource}"  
          SelectedItem="{Binding SelectedRowItem, Mode=TwoWay}"  
          SelectionMode="Extended" VirtualizingStackPanel.IsVirtualizing="True"  
          VirtualizingStackPanel.VirtualizationMode="Recycling"  
          EnableRowVirtualization="True"  
          EnableColumnVirtualization="True"  
          AutoGenerateColumns="False"  
          CanUserAddRows="False"  
          HeadersVisibility="Column"  
          x:Name="MainDataGrid"\>  
    \</DataGrid\>

* **`SelectionMode="Extended"`**: This allows users to select multiple rows using:  
  * `Ctrl + Mouse Left Click`: To toggle selection of individual rows.  
  * `Shift + Mouse Left Click`: To select a range of rows.  
  * `Shift + Up/Down arrow keys`: To extend selection from the currently focused row.

**2\. `MainViewModel` \- Tracking and Using Selected Items**

Your `MainViewModel` needs to be aware of the multiple selected items to act upon them. The `DataGrid.SelectedItems` property (which is an `IList`) provides this information.

You have two main approaches to get this into your ViewModel:

* **Option A: Bind `SelectedItems` (More Complex Synchronization):** Directly binding `DataGrid.SelectedItems` to an `ObservableCollection` in your ViewModel is tricky because `SelectedItems` is not a dependency property you can easily bind to in two-way mode. Solutions often involve attached behaviors or custom controls. This can be complex to implement robustly.

* **Option B: Pass `SelectedItems` as a `CommandParameter` (Simpler and Recommended):** This is often the more straightforward approach for MVVM. When a command is executed (e.g., Delete, Copy), you pass the `DataGrid.SelectedItems` collection as the `CommandParameter`.

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
// No specific ObservableCollection\<DataGridRowItemViewModel\> for \_selectedItemsList is strictly needed  
// if using CommandParameters. SelectedRowItem is still useful for single-select context.

public class MainViewModel : ViewModelBase  
{  
    // ... (SelectedRowItem property) ...

    // Commands will be modified to accept an IList (from DataGrid.SelectedItems)  
    // or IEnumerable\<DataGridRowItemViewModel\>.

    public MainViewModel(/\*...services...\*/)  
    {  
        // ...  
        // Example for DeleteNodeCommand initialization  
        // DeleteNodeCommand \= new RelayCommand\<IList\>(ExecuteDeleteNodes, CanExecuteDeleteNodes);  
        // CopyCommand \= new RelayCommand\<IList\>(ExecuteCopyNodes, CanExecuteCopyNodes);  
        // ...  
    }

    // Example for DeleteNodeCommand  
    private bool CanExecuteDeleteNodes(IList? selectedItems)  
    {  
        if (selectedItems \== null || selectedItems.Count \== 0\) return false;  
        // For context menu: "All selected items must support the operation"  
        // This means all items must not be read-only for deletion.  
        foreach (var item in selectedItems.OfType\<DataGridRowItemViewModel\>())  
        {  
            if (item.IsNodeReadOnly) return false; // Can't delete read-only nodes  
        }  
        return true;  
    }

    private void ExecuteDeleteNodes(IList? selectedItems)  
    {  
        if (selectedItems \== null || selectedItems.Count \== 0\) return;

        var vmsToDelete \= selectedItems.OfType\<DataGridRowItemViewModel\>().ToList();  
        if (\!vmsToDelete.Any()) return;

        // Show general warning dialog (Spec Section 2.9)  
        if (\!\_dialogService.ShowConfirmationDialog($"Are you sure you want to delete {vmsToDelete.Count} item(s)?", "Confirm Delete"))  
        {  
            return;  
        }

        // Important: Process deletions carefully, especially if they affect the underlying collection  
        // that FlatItemsSource is built from (e.g., \_rootDomNode).  
        // It's often best to collect all DomNodes to delete first, then remove them from their parents.  
        // This should ideally be a single undoable operation.

        List\<Tuple\<DomNode, DomNode, int\>\> undoDeleteInfo \= new List\<Tuple\<DomNode, DomNode, int\>\>(); // Parent, Node, OriginalIndex

        foreach (var vm in vmsToDelete.OrderByDescending(item \=\> FlatItemsSource.IndexOf(item))) // Process from bottom up if removing from bound ObservableCollection directly  
        {  
            if (vm.DomNode \!= null && vm.DomNode.Parent \!= null)  
            {  
                int originalIndex \= \-1;  
                if (vm.DomNode.Parent is ArrayNode an)  
                {  
                    originalIndex \= an.Items.IndexOf(vm.DomNode);  
                    an.Items.Remove(vm.DomNode);  
                }  
                else if (vm.DomNode.Parent is ObjectNode on)  
                {  
                    on.Children.Remove(vm.DomNode.Name);  
                    // For objects, index isn't as relevant for re-insertion unless order is strictly maintained  
                }  
                \_domToSchemaMap.Remove(vm.DomNode); // Clean up mapping  
                undoDeleteInfo.Add(new Tuple\<DomNode, DomNode, int\>(vm.DomNode.Parent, vm.DomNode, originalIndex));  
                // FlatItemsSource.Remove(vm); // If not doing a full refresh  
            }  
        }

        if (undoDeleteInfo.Any())  
        {  
            // \_undoRedoService.RecordOperation(new CompositeDeleteOperation(undoDeleteInfo)); // Conceptual composite undo  
            MarkAsDirty();  
        }

        // After modifying the core \_rootDomNode structure, rebuild the display list  
        ApplyFiltersAndRefreshDisplayList(); // Or a more targeted removal from FlatItemsSource  
    }

    // Example for CopyCommand  
    private bool CanExecuteCopyNodes(IList? selectedItems)  
    {  
        return selectedItems \!= null && selectedItems.Count \> 0;  
    }

    private void ExecuteCopyNodes(IList? selectedItems)  
    {  
        if (selectedItems \== null || selectedItems.Count \== 0\) return;

        var vmsToCopy \= selectedItems.OfType\<DataGridRowItemViewModel\>().ToList();  
        if (\!vmsToCopy.Any()) return;

        // "If multiple items selected, a JSON array of items is stored." (Spec Section 2.11)  
        // "Currently selected items are copied to the clipboard as a JSON string." (Spec Section 2.11)  
        if (vmsToCopy.Count \== 1\)  
        {  
            var domNode \= vmsToCopy.First().DomNode;  
            if (domNode \!= null)  
            {  
                string jsonString \= \_jsonSerializer.Serialize(domNode); // Serialize single node (subtree for object/array)  
                // \_clipboardService.SetText(jsonString); // Using an abstracted clipboard service  
                System.Windows.Clipboard.SetText(jsonString);  
                UpdateStatusBar("Item copied to clipboard.", false);  
            }  
        }  
        else  
        {  
            // Create a temporary root ArrayNode to hold all selected items for serialization  
            var tempArrayRoot \= new ArrayNode("$temp\_copy\_root$", null);  
            foreach (var vm in vmsToCopy)  
            {  
                if (vm.DomNode \!= null)  
                {  
                    // Important: When copying multiple items, you are copying their current state/value.  
                    // The DomNode structure needs to be such that serializer can handle them.  
                    // If they are independent items from different parents, you might just serialize each  
                    // and then construct the JSON array string manually.  
                    // If \_jsonSerializer.Serialize can take a List\<DomNode\> and produce an array, that's ideal.  
                    // Otherwise, serialize each node and combine.  
                    tempArrayRoot.Items.Add(vm.DomNode); // This assumes serializer handles items of an array correctly  
                }  
            }  
            string jsonArrayString \= \_jsonSerializer.Serialize(tempArrayRoot); // This will create {"$temp\_copy\_root$": \[items...\]} or just \[items...\] depending on serializer.  
                                                                            // You might need to adjust serializer or manually build the JSON array string.  
            // A simpler manual approach for JSON array string:  
            // var jsonStrings \= vmsToCopy.Select(vm \=\> vm.DomNode \!= null ? \_jsonSerializer.Serialize(vm.DomNode) : null)  
            //                             .Where(s \=\> s \!= null).ToList();  
            // string jsonArrayString \= $"\[{string.Join(",", jsonStrings)}\]";

            // System.Windows.Clipboard.SetText(jsonArrayString);  
            // UpdateStatusBar($"{vmsToCopy.Count} items copied to clipboard as a JSON array.", false);

            // For robust multi-item copy as a JSON array, it's best to construct a JsonDocument  
            using (var ms \= new System.IO.MemoryStream())  
            using (var writer \= new Utf8JsonWriter(ms))  
            {  
                writer.WriteStartArray();  
                foreach (var vm in vmsToCopy)  
                {  
                    if (vm.DomNode is ValueNode vn) vn.Value.WriteTo(writer);  
                    else if (vm.DomNode is RefNode rn) rn.OriginalValue.WriteTo(writer); // Write the {"$ref":...} object  
                    else if (vm.DomNode \!= null)  
                    {  
                        // For ObjectNode/ArrayNode, serialize them individually and parse as raw JSON  
                        string nodeJson \= \_jsonSerializer.Serialize(vm.DomNode);  
                        using(JsonDocument doc \= JsonDocument.Parse(nodeJson))  
                        {  
                            doc.RootElement.WriteTo(writer);  
                        }  
                    }  
                }  
                writer.WriteEndArray();  
                writer.Flush();  
                string jsonText \= System.Text.Encoding.UTF8.GetString(ms.ToArray());  
                System.Windows.Clipboard.SetText(jsonText);  
                UpdateStatusBar($"{vmsToCopy.Count} items copied as JSON array.", false);  
            }  
        }  
    }

    // Paste command logic:  
    // "Pasting in Browse mode only defined for array nodes." (Spec Section 2.11)  
    // "Paste (Ctrl+V) while multiple rows are selected in an array shall fail with an error message in the status bar." (Clarification 7\)  
    // This implies that for Paste, SelectedItem (single) is the anchor, not SelectedItems.  
    // The command parameter for Paste should probably be the single SelectedRowItem.  
}

**3\. XAML `CommandParameter` Binding:**

When binding commands in XAML (e.g., for menu items or context menu items), pass the `DataGrid.SelectedItems`.

XML  
\<MenuItem Header="Delete"  
          Command="{Binding DeleteNodeCommand}"  
          CommandParameter="{Binding ElementName=MainDataGrid, Path=SelectedItems}"/\>

\<MenuItem Header="Copy"  
          Command="{Binding CopyCommand}"  
          CommandParameter="{Binding ElementName=MainDataGrid, Path=SelectedItems}"/\>

\<ContextMenu DataContext="{Binding PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}"\>  
    \<MenuItem Header="Delete"  
              Command="{Binding ParentViewModel.DeleteNodeCommand}"  
              CommandParameter="{Binding RelativeSource={RelativeSource AncestorType=DataGrid}, Path=SelectedItems}"/\>  
    \<MenuItem Header="Copy"  
              Command="{Binding ParentViewModel.CopyCommand}"  
              CommandParameter="{Binding RelativeSource={RelativeSource AncestorType=DataGrid}, Path=SelectedItems}"/\>  
    \</ContextMenu\>

**4\. Context Menu `CanExecute` Logic (Spec Section 2.15):**

The `CanExecute` methods for commands that can act on multiple items (like Delete, Copy, ResetToNull) need to iterate through all `SelectedItems`.

C\#  
// Example for a hypothetical ResetToNullCommand  
private bool CanExecuteResetToNull(IList? selectedItems)  
{  
    if (selectedItems \== null || selectedItems.Count \== 0\) return false;  
    foreach (var item in selectedItems.OfType\<DataGridRowItemViewModel\>())  
    {  
        if (item.IsNodeReadOnly) return false; // Cannot reset read-only nodes  
        if (item.DomNode is ObjectNode || item.DomNode is ArrayNode)  
        {  
            // Spec for "Reset to null" usually applies to ValueNode or RefNode values.  
            // Clarify if complex types can be "reset to null" (i.e., replaced by a JSON null).  
            // For now, assume only value-like nodes.  
            // return false; // Or, if allowed, this check isn't needed.  
        }  
    }  
    return true;  
}

private void ExecuteResetToNull(IList? selectedItems)  
{  
    // ... iterate and reset each applicable node's value to null ...  
    // ... record undo operations ...  
    // ... MarkAsDirty() and RefreshDisplayList() if necessary ...  
}

**5\. Undo/Redo for Multi-Item Operations:**

* When an operation like `ExecuteDeleteNodes` modifies multiple `DomNode`s, you should ideally group these changes into a single `IUndoableOperation`.  
* You can create a `CompositeUndoableOperation` that holds a list of individual operations (e.g., one `DeleteNodeOperation` for each deleted node). When this composite operation's `Undo()` is called, it calls `Undo()` on all its children.

**Summary of Steps:**

1. **Enable `SelectionMode="Extended"` on the `DataGrid` in XAML.** WPF will then handle the basic `Ctrl+Click` and `Shift+Click`/Arrow key multi-selection mechanisms.  
2. **Adapt `MainViewModel` commands** to accept `IList` (or `IEnumerable<DataGridRowItemViewModel>`) as their parameter, representing the `DataGrid.SelectedItems`.  
3. **Pass `DataGrid.SelectedItems`** as the `CommandParameter` from XAML.  
4. **Modify command logic (`Execute` and `CanExecute`)** to iterate over the collection of selected items.  
   * `CanExecute` should ensure the operation is valid for *all* selected items if the operation is atomic per item (e.g., all must be deletable).  
   * `Execute` should perform the action on all selected items.  
5. **Implement composite undo operations** for actions that affect multiple items simultaneously to ensure a single undo reverses the entire multi-item action.  
6. **Handle Paste carefully:** Remember that pasting to a multi-selected target should fail, so the `PasteCommand`'s `CanExecute` might check if `SelectedItems.Count == 1` (or that `SelectedRowItem` is the intended single target).

By following these steps, you'll effectively integrate multi-select capabilities into your editor as defined by the specification.

# Array editing, including the placeholder at the end, with keyboard handling

Okay, implementing array editing with the specified placeholder node and keyboard handling involves several interconnected parts, touching on the `DataGridRowItemViewModel`, `MainViewModel`, and the XAML for the `DataGrid`.

Here's a breakdown of how to approach this, based on the established specification:

**Core Requirements from Specification for Array Editing:**

* **Placeholder Node (Spec Section 2.4.3):**  
  * A placeholder (displaying "Add item" in gray) shown at the end of every array item list.  
  * If a filter is active, and an array is displayed, the "Add item" placeholder for that array should always be visible as long as the array itself is visible.  
  * Entering edit mode (Enter or double-clicking) on this placeholder:  
    * Adds a new array item to the DOM tree with its default value (as defined by `ItemSchema`).  
    * Starts in-place editing the newly added item (if in-place editing is supported for its type).  
  * If tab navigation moves to this placeholder node, it shall automatically add a new item and enter edit mode on it.  
* **Insertion (Insert Key \- Spec Section 2.9):**  
  * For Arrays: Inserts a new item with its default value above the current row. Item type according to schema; if no schema, same type as selected item or ask user. Default value from schema or C\# type default.  
  * If `Insert` on array's placeholder node (from schema-covered items): DOM node built with all mandatory sub-nodes set to their default value.  
* **Context Menu Insertion (Spec Section 2.15):**  
  * "Insert new item ABOVE": Inserts new array item above selected one (like Insert key).  
  * "Insert new item BELOW": Inserts new array item below selected one. When an array's placeholder node is selected, a new item is created at its place, and in-place edit mode starts.  
* **Deletion (Spec Section 2.9):** Delete key deletes selected array item(s).  
* **Clipboard Paste (Spec Section 2.11):** Pasting in browse mode only for array nodes, above the currently selected array item row.

**Implementation Strategy:**

**1\. Representing the "Add Item" Placeholder:**

You can achieve this in a few ways:

* **Option A: Special `DataGridRowItemViewModel` Type:**

  * Create a derived class, e.g., `AddItemPlaceholderViewModel : DataGridRowItemViewModel`.  
  * This ViewModel would have specific properties to indicate it's a placeholder (e.g., `IsPlaceholder = true`).  
  * Its `NodeName` would be fixed to "Add item".  
  * Its `ValueDisplay` would be empty or a specific instruction.  
  * It would always appear last for an expanded `ArrayNode`.  
* **Option B: A Flag in `DataGridRowItemViewModel`:**

  * Add `public bool IsAddItemPlaceholder { get; private set; }` to `DataGridRowItemViewModel`.  
  * When constructing VMs for an `ArrayNode`, if `ShowDomAndSchemaView` is on (or based on your logic for placeholder visibility), add one extra VM at the end with this flag set to true. This VM wouldn't wrap a real `DomNode` initially but would know its parent `ArrayNode` and the `ItemSchema`.

C\#  
// \--- In JsonConfigEditor.Wpf/ViewModels/DataGridRowItemViewModel.cs \---  
public class DataGridRowItemViewModel : ViewModelBase  
{  
    // ... existing properties ...  
    public bool IsAddItemPlaceholder { get; private set; }  
    public ArrayNode? ParentArrayNodeForPlaceholder { get; private set; } // If this is a placeholder, which array it belongs to  
    public SchemaNode? ItemSchemaForPlaceholder { get; private set; } // Item schema for new item

    // Existing constructor for DomNode-backed items  
    public DataGridRowItemViewModel(DomNode domNode, SchemaNode schemaContextNode, MainViewModel parentViewModel)  
    {  
        // ...  
        IsAddItemPlaceholder \= false;  
    }

    // New constructor for the "Add Item" placeholder  
    public DataGridRowItemViewModel(ArrayNode parentArray, SchemaNode? itemSchema, MainViewModel parentViewModel, int depth)  
        : base() // Assuming ViewModelBase has a parameterless constructor or handle it  
    {  
        // Initialize essential properties for a placeholder  
        // No actual DomNode initially. SchemaContextNode might be the itemSchema.  
        // Name can be set to "Add item" via a property.  
        // ValueDisplay can be empty or instructive.  
        this.SchemaContextNode \= itemSchema ?? new SchemaNode("\[PlaceholderItem\]", typeof(object), false,true,null,null,null,null,null,false,null,null,false,null); // Fallback if itemSchema is null  
        this.ParentViewModel \= parentViewModel;  
        // Store parentArray and itemSchema for when the item is actually added  
        this.ParentArrayNodeForPlaceholder \= parentArray;  
        this.ItemSchemaForPlaceholder \= itemSchema;  
        this.IsAddItemPlaceholder \= true;  
        // Set \_nameOverride, \_depthOverride as used in schema-only node constructor  
        this.\_nameOverride \= "(Add new item)"; // Or get from resources for localization  
        this.\_depthOverride \= depth;

        // A placeholder is not expandable, not a DOM node present initially  
        this.\_isExpanded \= false;  
        this.DomNode \= null;  
    }

    public new string NodeName // Override if base directly accessed DomNode.Name  
    {  
        get  
        {  
            if (IsAddItemPlaceholder) return \_nameOverride ?? "(Add new item)";  
            // ... existing NodeName logic ...  
            return DomNode?.Name ?? \_nameOverride ?? SchemaContextNode.Name;  
        }  
    }

    public new string ValueDisplay // Override  
    {  
        get  
        {  
            if (IsAddItemPlaceholder) return ""; // Or some placeholder text  
            // ... existing ValueDisplay logic ...  
            return "";  
        }  
    }

    public new bool IsEditable // Override  
    {  
        get  
        {  
            if (IsAddItemPlaceholder) return true; // Placeholder is "editable" to trigger add  
            // ... existing IsEditable logic ...  
            return false;  
        }  
    }  
}

* 

**2\. `MainViewModel` \- Managing Placeholders and Array Item Creation:**

* **Populating `FlatItemsSource`:** When `AddNodeAndPotentialSchemaChildrenToListRecursive` (or similar) processes an `ArrayNode` and its `IsExpanded` VM:

  1. After adding all existing child `DomNode` VMs, if the array is editable (not read-only based on its own schema or parent), add an `AddItemPlaceholderViewModel` linked to this `ArrayNode` and its `ItemSchema`.  
  2. The placeholder should always be visible if the array itself is visible and expanded (even with filters active, as per spec).

C\#  
// \--- In JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
private void AddNodeAndPotentialSchemaChildrenToListRecursive(DomNode domNode, SchemaNode? schemaForDomNode, int depth)  
{  
    // ... (existing logic for adding domNode's VM) ...  
    var vm \= GetOrCreateDataGridRowItemViewModel(domNode, schemaForDomNode); // Ensure this method can handle existing VMs  
    // ...

    if (vm.IsExpandedInternal) // Or vm.IsExpanded  
    {  
        // ... (logic for ObjectNode children) ...  
        if (domNode is ArrayNode arrayNode)  
        {  
            SchemaNode? itemSchemaContext \= schemaForDomNode?.ItemSchema;  
            foreach (var childDomNode in arrayNode.Items)  
            {  
                AddNodeAndPotentialSchemaChildrenToListRecursive(childDomNode, itemSchemaContext, depth \+ 1);  
            }

            // Add "Add Item" placeholder if the array is editable  
            // Check if arrayNode itself or its schema is read-only  
            bool isArrayEditable \= \!(schemaForDomNode?.IsReadOnly ?? false);  
            if (isArrayEditable) // And potentially other conditions like not being a schema-only array itself  
            {  
                var placeholderVm \= new DataGridRowItemViewModel(arrayNode, itemSchemaContext, this, depth \+ 1);  
                FlatItemsSource.Add(placeholderVm);  
            }  
        }  
    }  
}

*   
* **Handling Edit Activation on Placeholder (`ActivateEditModeCommand` or `CommitEdit`):** When `DataGridRowItemViewModel.IsInEditMode` is set to `true` for a placeholder (or when its `CommitEdit` is called because it was "edited"):

  1. The `DataGridRowItemViewModel.CommitEdit()` method should detect `IsAddItemPlaceholder == true`.  
  2. It should then call a method on `MainViewModel`, e.g., `AddNewItemToArrayFromPlaceholder(DataGridRowItemViewModel placeholderVm)`.

C\#  
// \--- In JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
public DataGridRowItemViewModel? AddNewItemToArrayFromPlaceholder(DataGridRowItemViewModel placeholderVm)  
{  
    if (\!placeholderVm.IsAddItemPlaceholder || placeholderVm.ParentArrayNodeForPlaceholder \== null)  
        return null;

    ArrayNode parentArray \= placeholderVm.ParentArrayNodeForPlaceholder;  
    SchemaNode? itemSchema \= placeholderVm.ItemSchemaForPlaceholder;

    // 1\. Create the new DomNode using DomFactory (Spec Section 2.4.3)  
    //    The name for an array item is its future index.  
    string newItemName \= parentArray.Items.Count.ToString();  
    DomNode newItemDomNode;

    if (itemSchema \!= null)  
    {  
        newItemDomNode \= DomFactory.CreateDefaultFromSchema(newItemName, itemSchema, parentArray);  
        // If itemSchema defines an object with mandatory properties, DomFactory should create them (Spec Section 2.9)  
    }  
    else  
    {  
        // No schema for item type: prompt user or use default (e.g., null ValueNode)  
        // For now, let's create a null ValueNode. The spec also allows asking user.  
        // This part matches "Insert key for Arrays with no schema" (Spec Section 2.9)  
        // For simplicity, we'll use a null string value initially.  
        // A dialog should ask for type: string, number, boolean, object, array (Spec Section 2.9)  
        // TODO: Implement dialog to ask for type if no itemSchema.  
        var nullElement \= JsonDocument.Parse("null").RootElement.Clone();  
        newItemDomNode \= new ValueNode(newItemName, parentArray, nullElement);  
    }

    // 2\. Add to the DOM  
    parentArray.Items.Add(newItemDomNode);  
    \_domToSchemaMap\[newItemDomNode\] \= itemSchema; // Map its schema

    // 3\. Record Undo Operation  
    \_undoRedoService.RecordOperation(new AddNodeOperation(parentArray, newItemDomNode, parentArray.Items.Count \-1));  
    MarkAsDirty();

    // 4\. Refresh the DataGrid display  
    //    This is complex: you need to replace the placeholder VM with the new item's VM  
    //    and then potentially add a \*new\* placeholder VM.  
    //    A full RefreshDisplayList() is simpler but less performant.  
    //    Targeted update:  
    int placeholderIndex \= FlatItemsSource.IndexOf(placeholderVm);  
    if (placeholderIndex \!= \-1)  
    {  
        FlatItemsSource.RemoveAt(placeholderIndex); // Remove old placeholder  
        var newItemVm \= GetOrCreateDataGridRowItemViewModel(newItemDomNode, itemSchema);  
        FlatItemsSource.Insert(placeholderIndex, newItemVm); // Insert new item VM

        // Add a new placeholder if the array is still editable  
         bool isArrayEditable \= \!(placeholderVm.ParentArrayNodeForPlaceholder\_Schema?.IsReadOnly ?? false); // Need schema of parent array  
        if (isArrayEditable) {  
            var newPlaceholderVm \= new DataGridRowItemViewModel(parentArray, itemSchema, this, newItemVm.Indentation.Left/15 /\*approx depth\*/ \+1);  
            FlatItemsSource.Insert(placeholderIndex \+ 1, newPlaceholderVm);  
        }

        SelectedRowItem \= newItemVm; // Select the new item  
        return newItemVm; // Return the VM for the new item  
    }  
    else {  
        ApplyFiltersAndRefreshDisplayList(); // Fallback to full refresh  
    }  
    return null;  
}  
 The `DataGridRowItemViewModel.CommitEdit` would then try to start editing this new item.

 C\#  
// \--- In JsonConfigEditor.Wpf/ViewModels/DataGridRowItemViewModel.cs \---  
public bool CommitEdit()  
{  
    if (IsAddItemPlaceholder)  
    {  
        IsInEditMode \= false; // Exit "edit" of placeholder itself  
        var newItemVm \= ParentViewModel.AddNewItemToArrayFromPlaceholder(this);  
        if (newItemVm \!= null && newItemVm.IsEditable)  
        {  
            // "Starts in-place editing the newly added item" (Spec Section 2.4.3)  
            newItemVm.IsInEditMode \= true;  
            ParentViewModel.RequestScrollToItem(newItemVm); // Ensure visible  
            // Focus needs to be set programmatically here on the editor within newItemVm  
            ParentViewModel.RequestFocusOnEditor(newItemVm); // Conceptual method  
        }  
        return true; // Placeholder "edit" is considered successful if item is added  
    }  
    // ... existing CommitEdit logic for actual DomNodes ...  
}

* 

**3\. Keyboard Handling for Arrays:**

* **`Insert` Key (Spec Section 2.9):**

  * In `MainViewModel.ExecuteInsertNewItemCommand(DataGridRowItemViewModel? currentItem)`:  
    * If `currentItem` is null or `currentItem.DomNode` is null, do nothing or handle based on context.  
    * If `currentItem.IsAddItemPlaceholder == true`: Call `AddNewItemToArrayFromPlaceholder(currentItem)` and then try to start editing the new item (as per spec for placeholder \+ Insert Key).  
    * If `currentItem.DomNode.Parent is ArrayNode parentArray`:  
      * Determine `itemSchema` (from `parentArray`'s schema or by prompting/inferring).  
      * Create `newItemDomNode` using `DomFactory` or user input.  
      * Find index of `currentItem.DomNode` in `parentArray.Items`.  
      * Insert `newItemDomNode` at that index. Update names (indices) of subsequent items.  
      * Record undo, mark dirty.  
      * Refresh `FlatItemsSource` (targeted or full). Select the new item. Start editing it.  
* **Tab Navigation to Placeholder (Spec Section 2.4.3):**

  * In `MainWindow.xaml.cs`, `Editor_PreviewKeyDown` (for Tab in edit mode):  
    * If `mainVm.FindNextTabTarget()` returns the placeholder VM:  
      * Call `placeholderVm.ParentViewModel.AddNewItemToArrayFromPlaceholder(placeholderVm)`.  
      * The returned `newItemVm` (if not null) should then have `IsInEditMode = true` set, and focus should be moved to its editor.  
  * In `MainDataGrid_PreviewKeyDown_BrowseMode` (for Tab in browse mode):  
    * If the `nextItem` determined by Tab logic is a placeholder:  
      * Call `nextItem.ParentViewModel.AddNewItemToArrayFromPlaceholder(nextItem)`.  
      * Select the new item VM and set `IsInEditMode = true`. Programmatically focus its editor.  
      * `e.Handled = true;`  
* **`Delete` Key:** `ExecuteDeleteNodes` in `MainViewModel` already handles multiple items. If an array item is deleted:

  * Remove from `parentArray.Items`.  
  * Update names (indices) of subsequent items.  
  * Record undo, mark dirty.  
  * Refresh `FlatItemsSource`.

**4\. Context Menu Actions (Spec Section 2.15):**

* **"Insert new item ABOVE"**: Similar to Insert key logic, but the insertion point is explicitly `currentIndex`.  
* **"Insert new item BELOW"**: Similar, but insertion point is `currentIndex + 1`.  
  * If `SelectedRowItem.IsAddItemPlaceholder == true`: "a new item is created at its place. In-place edit mode starts immediately". This is exactly what `AddNewItemToArrayFromPlaceholder` followed by setting `IsInEditMode = true` on the new item achieves.

**5\. `DataGridRowItemViewModel` needs `ParentArrayNodeForPlaceholder_Schema` to check IsReadOnly for the placeholder.**

C\#  
// \--- In DataGridRowItemViewModel constructor for placeholder \---  
// this.ParentArrayNodeForPlaceholder\_Schema \= parentArraySchema; (pass the schema of the parent array itself)

// \--- Then in IsEditable for placeholder \---  
// if (IsAddItemPlaceholder) return \!(ParentArrayNodeForPlaceholder\_Schema?.IsReadOnly ?? false);

**Key Challenges:**

* **Refreshing `FlatItemsSource` Efficiently:** Adding/removing items from an array (especially not at the end) requires careful updates to `FlatItemsSource` to maintain correct order and placeholder positions. Targeted list modifications are better than full rebuilds for performance.  
* **Managing Indices:** When items are inserted/deleted in an `ArrayNode`, the `Name` property of subsequent `DomNode`s (which hold their index) needs to be updated. This also means their corresponding `DataGridRowItemViewModel.NodeName` needs to refresh.  
* **Focus Management:** Programmatically setting focus to a newly added and editable item's editor control after UI updates requires care (using `Dispatcher` as shown in Tab navigation outline).  
* **Dialog for Type Selection (No Schema):** If an array has no `ItemSchema`, inserting an item requires a dialog to ask the user for the type of the new item (string, number, boolean, object, array, reference).

This detailed approach covers the specified array editing features. It's complex due to the dynamic nature of the placeholder and the need to keep the DOM, ViewModels, and UI display in sync.

# Insert key feature including the dialog for specifying new item name, type and value

Okay, let's detail how to implement the "Insert Key" feature, which has different behaviors based on the context (arrays, objects, placeholders) and includes a dialog for specifying new item details, as per **Spec Section 2.9**.

**Core Idea:**

The `Insert` key action is context-sensitive. `MainViewModel` will have an `InsertNewItemCommand` triggered by the key press. This command will analyze the `SelectedRowItem` (the currently selected item in the `DataGrid`) to determine the correct insertion behavior and, if necessary, launch a dialog to gather details for the new node.

**1\. `InsertNewItemDialog` (New UI \- XAML and ViewModel)**

You'll need a new dialog window for cases where the user must specify properties for the new item.

* **`InsertNewItemDialog.xaml`:**

  * `TextBox` for "Property Name" (visible only when inserting into an `ObjectNode` where the name isn't predefined by schema, or for no-schema objects).  
  * `ComboBox` for "JSON Data Type" (options: String, Number, Boolean, Object, Array, Reference \- as per Spec Section 2.9, 2.15).  
  * `TextBox` for "Property Value" (visible and enabled only if a primitive type is selected in the ComboBox above).  
  * "OK" and "Cancel" buttons.  
* **`InsertNewItemDialogViewModel.cs` (`JsonConfigEditor.Wpf.ViewModels`):**

  * Properties: `PropertyName` (string), `SelectedJsonDataType` (enum or string), `PropertyValue` (string).  
  * Collections for `JsonDataType` `ComboBox` items.  
  * `ICommand` for OK (which would validate input and close the dialog with a result).  
  * Logic to control visibility/enablement of `PropertyName` and `PropertyValue` fields.

**2\. `MainViewModel` \- `InsertNewItemCommand` Logic**

This command is the central dispatcher for the Insert key.

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
// ... (previous using statements and class structure) ...

public class MainViewModel : ViewModelBase  
{  
    // ... (SelectedRowItem, \_domToSchemaMap, \_schemaLoader, \_undoRedoService, \_dialogService properties) ...

    // public ICommand InsertNewItemCommand { get; } // Initialized in constructor  
    // For this command, the parameter will be the currently SelectedRowItem

    private void ExecuteInsertNewItem(DataGridRowItemViewModel? currentItemVm)  
    {  
        if (currentItemVm \== null && \!\_rootDomNode.Items.Any()) // Inserting into an empty root array  
        {  
            HandleInsertIntoEmptyRootArray();  
            return;  
        }  
        if (currentItemVm \== null) return; // No context to insert

        DomNode? contextNode \= currentItemVm.DomNode; // This is the node the selection is on  
        SchemaNode? contextSchema \= currentItemVm.AssociatedSchemaNode;

        // \--- Behavior as per Spec Section 2.9 \---

        // A. Inserting into an Array (above the current item or into placeholder)  
        if (currentItemVm.IsAddItemPlaceholder) // Insert key on array's "Add item" placeholder  
        {  
            // "If the Insert key on a "gray" placeholder node (schema-covered items):  
            //  The DOM node should be built with all mandatory subnodes set to their default value." (Spec Section 2.9)  
            DataGridRowItemViewModel? newItemVm \= AddNewItemToArrayFromPlaceholder(currentItemVm); // Existing method  
            if (newItemVm \!= null && newItemVm.IsEditable)  
            {  
                newItemVm.IsInEditMode \= true;  
                RequestScrollToItem(newItemVm);  
                RequestFocusOnEditor(newItemVm);  
            }  
        }  
        else if (contextNode?.Parent is ArrayNode parentArray) // Standard array item selected  
        {  
            SchemaNode? parentArraySchema \= GetSchemaForDomNode(parentArray);  
            SchemaNode? itemSchema \= parentArraySchema?.ItemSchema ?? contextSchema?.ItemSchema; // Prefer array's item schema  
            int insertAtIndex \= parentArray.Items.IndexOf(contextNode\!);

            InsertNewArrayItem(parentArray, itemSchema, insertAtIndex, "ABOVE");  
        }  
        // B. Inserting into an Object (as a new property) or when an ObjectNode itself is selected  
        else if (contextNode is ObjectNode selectedObject && string.IsNullOrEmpty(contextNode.Name) && contextNode.Parent \== null) // Selected the ROOT object node directly  
        {  
             // "if the selected ObjectNode is the root node (has no parent), it adds a new property to it." (Clarification 3\)  
             HandleInsertNewPropertyToObject(selectedObject, contextSchema, null, \-1); // null parent context for dialog  
        }  
        else if (contextNode?.Parent is ObjectNode parentObject) // A property within an object is selected  
        {  
            // "If standing on a property within an ObjectNode, a new property should be created \[above it\]."  
            // This means adding a new property to parentObject.  
            // The dialog is for defining this \*new\* sibling property.  
            // To insert "above", we'd need to reorder, which is complex.  
            // The spec for "Insert new sub-node" (context menu) is "Adds a new property to the selected object node".  
            // Let's align "Insert" key on an object's property to mean adding a \*new property to the parent object\*,  
            // typically at the end or prompting for name.  
            // "For no-schema non-array nodes (when using Insert key): If standing on a property within an ObjectNode,  
            // a new property should be created. The editor should show a modal dialog..." (Spec Section 2.9)  
            // This implies adding a \*new, different\* property, not inserting above the current one.  
             HandleInsertNewPropertyToObject(parentObject, GetSchemaForDomNode(parentObject), contextNode.Name, \-1);  
        }  
        else if (contextNode is ObjectNode selectedObjectNodeItself) // An ObjectNode (not its property value) is selected  
        {  
            // "if you press Insert when an ObjectNode itself (not one of its properties) is selected,  
            // a dialog for a new property on the same level in the parent dom node should be shown." (Clarification 3\)  
            // This means adding a property to selectedObjectNodeItself.Parent if it's an ObjectNode.  
            if(selectedObjectNodeItself.Parent is ObjectNode parentOfSelectedObject)  
            {  
                HandleInsertNewPropertyToObject(parentOfSelectedObject, GetSchemaForDomNode(parentOfSelectedObject), selectedObjectNodeItself.Name, \-1);  
            }  
            else if (selectedObjectNodeItself.Parent \== null) // It's the root object  
            {  
                HandleInsertNewPropertyToObject(selectedObjectNodeItself, contextSchema, null, \-1);  
            }  
            // If selectedObjectNodeItself.Parent is an ArrayNode, this Insert key behavior is not explicitly defined for "ObjectNode itself".  
            // The array insertion logic (above) would have already handled this if an array item was selected.  
            // For safety, do nothing or status bar message.  
        }  
        else if (contextNode \== null && currentItemVm.IsSchemaOnlyNode && currentItemVm.SchemaContextNode.NodeType \== SchemaNodeType.Object)  
        {  
            // Inserting while a schema-only object property placeholder is selected.  
            // This should materialize the placeholder object and then potentially allow adding a new sub-property to it.  
            // For simplicity now, let's say it behaves like "Insert new sub-node" for the object this placeholder represents.  
            DomNode? materializedObject \= MaterializeDomNodeForSchema(currentItemVm);  
            if (materializedObject is ObjectNode objNode)  
            {  
                HandleInsertNewPropertyToObject(objNode, currentItemVm.SchemaContextNode, null, \-1);  
            }  
        }  
         else if (\_rootDomNode is ArrayNode rootArray && currentItemVm \== null) // Inserting into an empty root array  
        {  
             InsertNewArrayItem(rootArray, rootArraySchema?.ItemSchema, 0, "INTO\_EMPTY");  
        }  
    }

    private void HandleInsertIntoEmptyRootArray()  
    {  
        if (\_rootDomNode is ArrayNode rootArray)  
        {  
            SchemaNode? rootArraySchema \= GetSchemaForDomNode(rootArray);  
            InsertNewArrayItem(rootArray, rootArraySchema?.ItemSchema, 0, "INTO\_EMPTY");  
        }  
    }

    /// \<summary\>  
    /// Handles the logic for inserting a new property into an ObjectNode.  
    /// This will typically involve showing a dialog to get the property name, type, and value.  
    /// (From specification document, Section 2.9 \- "For no-schema non-array nodes")  
    /// \</summary\>  
    private void HandleInsertNewPropertyToObject(ObjectNode targetObjectNode, SchemaNode? targetObjectSchema, string? siblingPropertyName, int insertIndex)  
    {  
        // 1\. Show Dialog:  
        //    var dialogVm \= new InsertNewItemDialogViewModel(isObjectProperty: true, existingNames: targetObjectNode.Children.Keys);  
        //    bool? dialogResult \= \_dialogService.ShowInsertNewItemDialog(dialogVm); // Conceptual dialog service method

        // For skeleton, assume we get these values:  
        // if (dialogResult \== true)  
        // {  
        //     string newPropertyName \= dialogVm.PropertyName;  
        //     JsonDataType selectedType \= dialogVm.SelectedJsonDataType; // Enum: String, Number, Boolean, Object, Array, Reference  
        //     string initialValueStr \= dialogVm.PropertyValue;

        // \--- Mocking dialog result for now \---  
        string newPropertyName \= $"newProperty{targetObjectNode.Children.Count \+ 1}"; // Ensure unique enough for example  
        if (targetObjectNode.Children.ContainsKey(newPropertyName)) {  
            UpdateStatusBar($"Property '{newPropertyName}' already exists.", true);  
            return;  
        }  
        // Let's default to inserting a string for simplicity in skeleton  
        JsonDataType selectedType \= JsonDataType.String; // Assume JsonDataType is an enum  
        string initialValueStr \= "default value";  
        // \--- End Mock \---

        // 2\. Create the new DomNode based on dialog input.  
        DomNode? newPropertyNode \= null;  
        SchemaNode? newPropertySchema \= null; // If targetObjectSchema defines this new property

        if (targetObjectSchema?.Properties?.TryGetValue(newPropertyName, out var propSchema) \== true)  
        {  
            newPropertySchema \= propSchema; // Schema found for this named property  
            newPropertyNode \= DomFactory.CreateDefaultFromSchema(newPropertyName, newPropertySchema, targetObjectNode);  
            // If user provided an initialValueStr for a primitive, try to apply it.  
            if (newPropertyNode is ValueNode vn && selectedType.IsPrimitive()) // Assume IsPrimitive() helper  
            {  
                // TryParseEditValue is a helper from previous milestone outlines  
                if(TryParseEditValue(initialValueStr, newPropertySchema.ClrType, out JsonElement parsedElement, out \_)) {  
                    vn.Value \= parsedElement;  
                }  
            }  
        }  
        else if (targetObjectSchema?.AllowAdditionalProperties \== true && targetObjectSchema.AdditionalPropertiesSchema \!= null)  
        {  
            // Adding an "additional property"  
            newPropertySchema \= targetObjectSchema.AdditionalPropertiesSchema;  
            newPropertyNode \= DomFactory.CreateDefaultFromSchema(newPropertyName, newPropertySchema, targetObjectNode);  
            // Apply initialValueStr if primitive  
            if (newPropertyNode is ValueNode vn && selectedType.IsPrimitive())  
            {  
                 if(TryParseEditValue(initialValueStr, newPropertySchema.ClrType, out JsonElement parsedElement, out \_)) {  
                    vn.Value \= parsedElement;  
                }  
            }  
        }  
        else // Unschematized property or schema doesn't allow it (though UI should prevent if closed object)  
        {  
            switch (selectedType)  
            {  
                case JsonDataType.String:  
                    newPropertyNode \= new ValueNode(newPropertyName, targetObjectNode, JsonDocument.Parse($"\\"{JsonEncodedText.Encode(initialValueStr)}\\"").RootElement.Clone());  
                    break;  
                case JsonDataType.Number: // Assume integer for simplicity here  
                    newPropertyNode \= new ValueNode(newPropertyName, targetObjectNode, JsonDocument.Parse(int.TryParse(initialValueStr, out int n) ? n.ToString() : "0").RootElement.Clone());  
                    break;  
                case JsonDataType.Boolean:  
                    newPropertyNode \= new ValueNode(newPropertyName, targetObjectNode, JsonDocument.Parse(bool.TryParse(initialValueStr, out bool b) && b ? "true" : "false").RootElement.Clone());  
                    break;  
                case JsonDataType.Object:  
                    newPropertyNode \= new ObjectNode(newPropertyName, targetObjectNode);  
                    break;  
                case JsonDataType.Array:  
                    newPropertyNode \= new ArrayNode(newPropertyName, targetObjectNode);  
                    break;  
                case JsonDataType.Reference:  
                    // Dialog should provide path for reference  
                    string refPath \= initialValueStr; // Assuming initialValueStr holds the path for $ref  
                    if (string.IsNullOrWhiteSpace(refPath) || \!refPath.StartsWith("/")) refPath \= "/example/path"; // Default invalid path  
                    var refObj \= JsonDocument.Parse($"{{\\"$ref\\":\\"{JsonEncodedText.Encode(refPath)}\\"}}").RootElement.Clone();  
                    newPropertyNode \= new RefNode(newPropertyName, targetObjectNode, refPath, refObj);  
                    break;  
                default:  
                    UpdateStatusBar("Invalid data type selected for new property.", true);  
                    return;  
            }  
        }

        if (newPropertyNode \!= null)  
        {  
            targetObjectNode.Children\[newPropertyName\] \= newPropertyNode;  
            \_domToSchemaMap\[newPropertyNode\] \= newPropertySchema; // May be null if unschematized

            \_undoRedoService.RecordOperation(new AddNodeOperation(targetObjectNode, newPropertyNode, \-1)); // \-1 for object property  
            MarkAsDirty();  
            ApplyFiltersAndRefreshDisplayList(); // Refresh to show new property  
            // TODO: Select and potentially start editing the new node/value  
            var newVm \= FlatItemsSource.FirstOrDefault(vm \=\> vm.DomNode \== newPropertyNode);  
            if (newVm \!= null)  
            {  
                SelectedRowItem \= newVm;  
                RequestScrollToItem(newVm);  
                if (newVm.IsEditable && selectedType.IsPrimitive()) // Start editing primitives  
                {  
                    newVm.IsInEditMode \= true;  
                    RequestFocusOnEditor(newVm);  
                }  
            }  
        }  
        // } // End of if (dialogResult \== true)  
    }

    /// \<summary\>  
    /// Inserts a new item into an ArrayNode.  
    /// \</summary\>  
    /// \<param name="parentArray"\>The ArrayNode to insert into.\</param\>  
    /// \<param name="itemSchema"\>The SchemaNode for items in this array (can be null).\</param\>  
    /// \<param name="index"\>The index at which to insert the new item.\</param\>  
    /// \<param name="mode"\>"ABOVE", "BELOW", "INTO\_EMPTY" \- for slightly different behaviors or logging\</param\>  
    private void InsertNewArrayItem(ArrayNode parentArray, SchemaNode? itemSchema, int index, string mode)  
    {  
        // 1\. Determine the type and default value for the new item. (Spec Section 2.9 \- For Arrays)  
        DomNode newItemDomNode;  
        string newItemName \= index.ToString(); // Initial name, will be updated if inserted mid-array

        if (itemSchema \!= null)  
        {  
            // "Default value according to the schema. If not defined, then a natural default for the CLR data type"  
            // "For Schema-covered items (when using Insert key on a "gray" placeholder node):  
            //  The DOM node should be built with all mandatory subnodes set to their default value."  
            newItemDomNode \= DomFactory.CreateDefaultFromSchema(newItemName, itemSchema, parentArray);  
        }  
        else // No schema for array item  
        {  
            // "Item type according to the schema. If no schema is defined, then the same type as the selected item.  
            //  If no items are in the array yet, the editor should ask about the item type (natural Json value types)."  
            // For simplicity in skeleton, let's default to a null ValueNode.  
            // TODO: Implement dialog to ask for type as per spec.  
            JsonDataType selectedType \= JsonDataType.String; // Assume from dialog or default  
            string initialValueStr \= "";       // Assume from dialog or default

            // Based on selectedType, create appropriate node (similar to HandleInsertNewPropertyToObject)  
             switch (selectedType)  
            {  
                case JsonDataType.String:  
                    newItemDomNode \= new ValueNode(newItemName, parentArray, JsonDocument.Parse($"\\"{JsonEncodedText.Encode(initialValueStr)}\\"").RootElement.Clone());  
                    break;  
                // ... other types ...  
                default:  
                    newItemDomNode \= new ValueNode(newItemName, parentArray, JsonDocument.Parse("null").RootElement.Clone());  
                    break;  
            }  
        }

        // 2\. Insert into DOM and update names (indices) of subsequent items  
        parentArray.Items.Insert(index, newItemDomNode);  
        for (int i \= index; i \< parentArray.Items.Count; i++) // Update names (indices)  
        {  
            parentArray.Items\[i\].Name \= i.ToString(); // Protected setter needed on DomNode.Name  
        }  
        \_domToSchemaMap\[newItemDomNode\] \= itemSchema;

        // 3\. Record Undo, Mark Dirty, Refresh UI  
        \_undoRedoService.RecordOperation(new AddNodeOperation(parentArray, newItemDomNode, index));  
        MarkAsDirty();  
        ApplyFiltersAndRefreshDisplayList(); // Refresh display

        // 4\. Select and start editing the new item's value if it's a primitive  
        var newVm \= FlatItemsSource.FirstOrDefault(vm \=\> vm.DomNode \== newItemDomNode);  
        if (newVm \!= null)  
        {  
            SelectedRowItem \= newVm;  
            RequestScrollToItem(newVm);  
            if (newVm.IsEditable && newItemDomNode is ValueNode) // Or RefNode  
            {  
                newVm.IsInEditMode \= true;  
                RequestFocusOnEditor(newVm);  
            }  
        }  
    }  
    // Mock enum for dialog  
    public enum JsonDataType { String, Number, Boolean, Object, Array, Reference; public bool IsPrimitive() \=\> this \== String || this \== Number || this \== Boolean; }  
}

**3\. `IDialogService` Enhancement (Conceptual):**

C\#  
// \--- File: JsonConfigEditor.Wpf/Services/IDialogService.cs \---  
namespace JsonConfigEditor.Wpf.Services  
{  
    // ... (existing dialog methods) ...

    // Define a result class for the dialog  
    public class InsertNewItemDialogResult  
    {  
        public bool Confirmed { get; set; }  
        public string? PropertyName { get; set; } // Null if not for an object property  
        public MainViewModel.JsonDataType SelectedJsonDataType { get; set; } // Use the enum from MainViewModel or a shared one  
        public string? InitialValue { get; set; } // For primitives or $ref path  
    }

    public interface IDialogService  
    {  
        // ...  
        /// \<summary\>  
        /// Shows a dialog to get details for a new JSON node.  
        /// \</summary\>  
        /// \<param name="isObjectProperty"\>True if inserting a property into an object (requires name), false for array item (name is index).\</param\>  
        /// \<param name="existingPropertyNames"\>Collection of existing names if inserting an object property, to suggest unique names or validate.\</param\>  
        /// \<returns\>The result from the dialog, or null if cancelled.\</returns\>  
        InsertNewItemDialogResult? ShowInsertNewItemDialog(bool isObjectProperty, IEnumerable\<string\>? existingPropertyNames \= null);  
    }  
}

**Explanation and Flow:**

1. **`InsertNewItemCommand` Trigger:** User presses `Insert`. The `ExecuteInsertNewItem` method in `MainViewModel` is called with the currently selected `DataGridRowItemViewModel`.  
2. **Context Determination:**  
   * **Array Context:** If the selection is an array item or an array's "Add Item" placeholder, the logic proceeds to insert an array item (`InsertNewArrayItem`).  
     * If schema exists (`itemSchema`), `DomFactory.CreateDefaultFromSchema` is used. This handles mandatory sub-nodes for objects within arrays (**Spec Section 2.9**).  
     * If no schema, the spec says "same type as selected item" or "ask about item type". The skeleton defaults to a simpler path but notes where a dialog would go.  
   * **Object Context / No-Schema Non-Array:** If the selection is a property in an object, or an `ObjectNode` itself, or a no-schema non-array node, `HandleInsertNewPropertyToObject` is called.  
     * This method **must show a modal dialog** (**Spec Section 2.9**) asking for:  
       * Property Name (if applicable).  
       * JSON Data Type (String, Number, Boolean, Object, Array, Reference).  
       * Initial Value (if primitive type selected, or path if Reference).  
     * The dialog's result is used to create the new `DomNode`.  
3. **Node Creation:**  
   * For schematized items, `DomFactory.CreateDefaultFromSchema` is preferred.  
   * For items created via dialog, construct the `DomNode` based on user's type selection and initial value.  
4. **DOM Update:** The new `DomNode` is added to its parent's `Children` (for `ObjectNode`) or `Items` (for `ArrayNode`). If it's an array insertion, indices of subsequent items are updated.  
5. **State Update:** The new node is mapped to its schema (`_domToSchemaMap`), an undo operation is recorded, `IsDirty` is set.  
6. **UI Refresh:** `ApplyFiltersAndRefreshDisplayList()` updates the `DataGrid`.  
7. **Focus & Edit:** The newly inserted item is selected, scrolled into view, and if it's a primitive or editable `RefNode`, in-place editing starts.

This covers the different scenarios for the Insert key as detailed in the specification. The dialog for new object properties or un-schematized array items is a key new UI component required.

# Schema extraction from assemblies

Okay, let's detail the implementation strategy for schema extraction from C\# assemblies. This is the core of your `SchemaLoaderService` and involves significant use of .NET Reflection.

**Core Goal:** To scan specified C\# assemblies, identify classes marked with `[ConfigSchemaAttribute]`, and recursively reflect over the C\# types (and their properties) to build an in-memory tree of `SchemaNode` objects.

**1\. `SchemaLoaderService` \- Orchestration (`JsonConfigEditor.Core.SchemaLoading`)**

This class orchestrates the loading process.

C\#  
using System.Reflection;  
using JsonConfigEditor.Contracts.Attributes; // Your custom attributes  
using JsonConfigEditor.Core.Schema;         // Your SchemaNode  
using System.ComponentModel;              // For ReadOnlyAttribute  
using System.ComponentModel.DataAnnotations; // For RangeAttribute  
// Add other necessary using statements

namespace JsonConfigEditor.Core.SchemaLoading  
{  
    public class SchemaLoaderService : ISchemaLoaderService  
    {  
        private readonly Dictionary\<string, SchemaNode\> \_loadedRootSchemas \= new Dictionary\<string, SchemaNode\>();  
        private readonly List\<string\> \_errorMessages \= new List\<string\>();

        public IReadOnlyDictionary\<string, SchemaNode\> RootSchemas \=\> \_loadedRootSchemas;  
        public IReadOnlyList\<string\> ErrorMessages \=\> \_errorMessages;

        /// \<summary\>  
        /// Asynchronously loads schema definitions from assemblies found at the specified paths.  
        /// This method clears any previously loaded schemas and error messages.  
        /// (From specification document, Section 2.2 & Best Practice for async)  
        /// \</summary\>  
        public async Task LoadSchemasFromAssembliesAsync(IEnumerable\<string\> assemblyPaths)  
        {  
            \_loadedRootSchemas.Clear();  
            \_errorMessages.Clear();

            // UI should indicate busy state here (conceptual, handled by MainViewModel)

            foreach (var path in assemblyPaths)  
            {  
                try  
                {  
                    // For .NET Core/5+, consider AssemblyLoadContext for more advanced scenarios,  
                    // but Assembly.LoadFrom is generally suitable for this kind of plugin loading.  
                    // Run assembly loading and processing in a background thread to keep UI responsive.  
                    await Task.Run(() \=\>  
                    {  
                        Assembly assembly \= Assembly.LoadFrom(path);  
                        ProcessAssembly(assembly);  
                    });  
                }  
                catch (Exception ex)  
                {  
                    // Record error specific to this assembly  
                    string errorMessage \= $"Error loading or processing assembly '{path}': {ex.Message}";  
                    \_errorMessages.Add(errorMessage);  
                    // Log detailed error using NLog (Spec Section 4 \- Logging)  
                    // Logger.Error(ex, errorMessage);  
                }  
            }  
            // UI should clear busy state here (conceptual)  
            // Notify subscribers (e.g., MainViewModel) that loading is complete (e.g., via an event or observable properties)  
        }

        /// \<summary\>  
        /// Processes a single loaded assembly to find and build root schema definitions.  
        /// \</summary\>  
        private void ProcessAssembly(Assembly assembly)  
        {  
            try  
            {  
                foreach (Type typeInAssembly in assembly.GetTypes())  
                {  
                    if (\!typeInAssembly.IsPublic && \!typeInAssembly.IsNestedPublic) continue; // Typically only consider public types

                    var configSchemaAttributes \= typeInAssembly.GetCustomAttributes\<ConfigSchemaAttribute\>(false);  
                    foreach (var attr in configSchemaAttributes)  
                    {  
                        if (string.IsNullOrWhiteSpace(attr.MountPath))  
                        {  
                            \_errorMessages.Add($"Schema Error: Type '{typeInAssembly.FullName}' in assembly '{assembly.GetName().Name}' has a ConfigSchemaAttribute with an empty or invalid MountPath.");  
                            continue;  
                        }

                        // The spec says the attribute points to the SchemaClassType.  
                        // If the attribute is ON the schema class itself, attr.SchemaClassType \== typeInAssembly  
                        Type schemaDefiningType \= attr.SchemaClassType;  
                        if (schemaDefiningType \== null) { // Fallback if SchemaClassType was not required by attribute constructor  
                             schemaDefiningType \= typeInAssembly;  
                        }

                        if (\_loadedRootSchemas.ContainsKey(attr.MountPath))  
                        {  
                            \_errorMessages.Add($"Schema Error: Duplicate MountPath '{attr.MountPath}' detected. Defined by '{schemaDefiningType.FullName}' and also by '{\_loadedRootSchemas\[attr.MountPath\].ClrType.FullName}'.");  
                            continue; // Skip this duplicate  
                        }

                        try  
                        {  
                            // Build the root SchemaNode. The 'name' for a root schema is its MountPath.  
                            // A new HashSet is created for each root schema to track circular refs within that schema branch.  
                            SchemaNode rootNode \= BuildSchemaRecursive(schemaDefiningType, attr.MountPath, null, new HashSet\<Type\>());  
                            \_loadedRootSchemas.Add(attr.MountPath, rootNode);  
                        }  
                        catch (Exception ex)  
                        {  
                            \_errorMessages.Add($"Error building schema for MountPath '{attr.MountPath}' (Type: '{schemaDefiningType.FullName}'): {ex.Message}");  
                            // Logger.Error(ex, ...);  
                        }  
                    }  
                }  
            }  
            catch (ReflectionTypeLoadException rtlex) // Handles issues if some types in assembly can't be loaded  
            {  
                \_errorMessages.Add($"Error reflecting types in assembly '{assembly.GetName().Name}': {rtlex.Message}");  
                foreach(var loaderEx in rtlex.LoaderExceptions)  
                {  
                    if(loaderEx \!= null) \_errorMessages.Add($"  LoaderException: {loaderEx.Message}");  
                }  
                // Logger.Error(rtlex, ...);  
            }  
             catch (Exception ex)  
            {  
                \_errorMessages.Add($"Unexpected error processing assembly '{assembly.GetName().Name}': {ex.Message}");  
                // Logger.Error(ex, ...);  
            }  
        }  
    }  
}

**2\. `SchemaLoaderService` \- Recursive Schema Building Logic:**

This is the core recursive method. It needs to be inside `SchemaLoaderService`.

C\#  
// \--- Still within JsonConfigEditor.Core/SchemaLoading/SchemaLoaderService.cs \---  
public partial class SchemaLoaderService // Using partial to split the class for readability  
{  
    /// \<summary\>  
    /// Recursively builds a SchemaNode for a given C\# type.  
    /// This is the heart of the schema extraction process.  
    /// \</summary\>  
    /// \<param name="currentType"\>The C\# System.Type to build the schema for.\</param\>  
    /// \<param name="nodeName"\>The name this schema node will have (e.g., property name, MountPath, or "\*").\</param\>  
    /// \<param name="sourcePropertyInfo"\>If this schema is for a property, this is its PropertyInfo. Null otherwise (e.g., for root types, array items).\</param\>  
    /// \<param name="processedTypesInPath"\>A HashSet to detect and break circular references within the current processing path.\</param\>  
    /// \<returns\>The constructed SchemaNode.\</returns\>  
    private SchemaNode BuildSchemaRecursive(Type currentType, string nodeName, PropertyInfo? sourcePropertyInfo, HashSet\<Type\> processedTypesInPath)  
    {  
        // \--- 1\. Handle Circular References \---  
        if (\!currentType.IsValueType && currentType \!= typeof(string) && processedTypesInPath.Contains(currentType))  
        {  
            \_errorMessages.Add($"Schema Warning: Circular reference detected for type '{currentType.FullName}' while processing '{nodeName}'. Returning minimal schema for this occurrence.");  
            // Return a basic, non-recursive schema node to break the cycle.  
            // It's important this placeholder doesn't attempt to define Properties/ItemSchema again for this type.  
            return new SchemaNode(nodeName, currentType, false, false, null, null, null, null, null, false, null, null, false, null, currentType.IsEnum ? null : nodeName);  
        }  
        // Add to processed path if it's a type that can cause cycles  
        if (\!currentType.IsValueType && currentType \!= typeof(string))  
        {  
            processedTypesInPath.Add(currentType);  
        }

        // \--- 2\. Extract Basic Info & Constraints \---  
        // Prioritize attributes on sourcePropertyInfo, then fall back to currentType if applicable.  
        MemberInfo attributeSource \= sourcePropertyInfo ?? (MemberInfo)currentType;

        Type effectiveClrType \= currentType;  
        Type? underlyingNullableType \= Nullable.GetUnderlyingType(currentType);  
        if (underlyingNullableType \!= null)  
        {  
            effectiveClrType \= underlyingNullableType; // Work with the non-nullable version for most type checks  
        }

        bool isRequired \= DetermineIsRequired(effectiveClrType, sourcePropertyInfo);  
        bool isReadOnly \= (attributeSource.GetCustomAttribute\<ReadOnlyAttribute\>(true)?.IsReadOnly ?? false) || (sourcePropertyInfo?.CanWrite \== false);  
        string? regexPattern \= attributeSource.GetCustomAttribute\<SchemaRegexPatternAttribute\>(true)?.Pattern;  
        List\<string\>? allowedValues \= attributeSource.GetCustomAttribute\<SchemaAllowedValuesAttribute\>(true)?.AllowedValues;  
        bool isEnumFlags \= false;

        double? min \= null;  
        double? max \= null;  
        var rangeAttr \= attributeSource.GetCustomAttribute\<RangeAttribute\>(true);  
        if (rangeAttr \!= null)  
        {  
            if (rangeAttr.Minimum is IConvertible minConv) min \= minConv.ToDouble(System.Globalization.CultureInfo.InvariantCulture);  
            if (rangeAttr.Maximum is IConvertible maxConv) max \= maxConv.ToDouble(System.Globalization.CultureInfo.InvariantCulture);  
        }

        if (effectiveClrType.IsEnum)  
        {  
            allowedValues \= Enum.GetNames(effectiveClrType).ToList();  
            isEnumFlags \= effectiveClrType.IsDefined(typeof(FlagsAttribute), false);  
        }

        // Default Value (complex part)  
        object? defaultValue \= GetEffectiveDefaultValue(currentType, sourcePropertyInfo);

        // \--- 3\. Determine SchemaNodeType and Populate Complex Type Properties \---  
        Dictionary\<string, SchemaNode\>? properties \= null;  
        SchemaNode? additionalPropertiesSchema \= null;  
        bool allowAdditionalProperties \= false;  
        SchemaNode? itemSchema \= null;

        if (IsDictionaryStringKeyType(effectiveClrType, out Type? dictionaryValueType))  
        {  
            additionalPropertiesSchema \= BuildSchemaRecursive(dictionaryValueType\!, "\*", null, new HashSet\<Type\>(processedTypesInPath));  
            allowAdditionalProperties \= true;  
        }  
        else if (IsCollectionType(effectiveClrType, out Type? elementType) && effectiveClrType \!= typeof(string))  
        {  
            itemSchema \= BuildSchemaRecursive(elementType\!, "\*", null, new HashSet\<Type\>(processedTypesInPath));  
        }  
        else if (ShouldBeTreatedAsObject(effectiveClrType)) // Custom helper: IsClass, not string, not enum, not primitive, not collection/dictionary  
        {  
            properties \= new Dictionary\<string, SchemaNode\>();  
            foreach (PropertyInfo propInfo in effectiveClrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))  
            {  
                if (propInfo.GetCustomAttribute\<System.Text.Json.Serialization.JsonIgnoreAttribute\>(true) \!= null ||  
                    propInfo.GetIndexParameters().Any()) // Skip indexers  
                {  
                    continue;  
                }  
                properties\[propInfo.Name\] \= BuildSchemaRecursive(propInfo.PropertyType, propInfo.Name, propInfo, new HashSet\<Type\>(processedTypesInPath));  
            }  
            // AllowAdditionalProperties could be based on whether it implements IDictionary or has a specific attribute.  
            // For now, if it has defined properties, we assume it's closed unless it's also a dictionary type.  
            allowAdditionalProperties \= properties.Count \== 0; // Or more sophisticated logic  
        }

        // \--- 4\. Construct SchemaNode \---  
        // MountPath is only set for the very first call from ProcessAssembly for a root ConfigSchemaAttribute.  
        // For all nested calls (properties, item types), MountPath is null.  
        string? mountPathForThisNode \= (sourcePropertyInfo \== null && nodeName.Contains("/")) ? nodeName : null;

        var schemaNode \= new SchemaNode(  
            nodeName, currentType, // Store the original type (e.g., Nullable\<int\>), not just effectiveClrType for ClrType  
            isRequired, isReadOnly, defaultValue,  
            min, max, regexPattern, allowedValues, isEnumFlags,  
            properties, additionalPropertiesSchema, allowAdditionalProperties,  
            itemSchema, mountPathForThisNode  
        );

        // Remove from processed path before returning  
        if (\!currentType.IsValueType && currentType \!= typeof(string))  
        {  
            processedTypesInPath.Remove(currentType);  
        }

        return schemaNode;  
    }

    // \--- Helper Methods for Reflection (to be implemented within SchemaLoaderService) \---

    private bool DetermineIsRequired(Type type, PropertyInfo? propInfo)  
    {  
        // (From specification document, Section 2.2 \- IsRequired from C\# non-nullables, RequiredMemberAttribute)  
        // 1\. Check \[RequiredMemberAttribute\] on propInfo if available.  
        // 2\. If value type and not Nullable\<T\>, it's required.  
        // 3\. NRT analysis (simplified):  
        //    \- Check NullableAttribute/NullableContextAttribute on propInfo or its declaring type.  
        // This is complex to get 100% correct. Start with value types and RequiredMemberAttribute.  
        if (propInfo?.GetCustomAttribute\<System.Runtime.CompilerServices.RequiredMemberAttribute\>() \!= null) return true;  
        if (type.IsValueType && Nullable.GetUnderlyingType(type) \== null) return true;  
        // Basic NRT check (heuristic):  
        // var nullableAttrib \= propInfo?.GetCustomAttributes\<System.Runtime.CompilerServices.NullableAttribute\>(false).FirstOrDefault();  
        // if (nullableAttrib \!= null && nullableAttrib.NullableFlags.Length \> 0 && nullableAttrib.NullableFlags\[0\] \== 1\) return false; // Explicitly nullable  
        // if (nullableAttrib \!= null && nullableAttrib.NullableFlags.Length \> 0 && nullableAttrib.NullableFlags\[0\] \== 2\) return true; // Explicitly not nullable  
        return false; // Default to not required for reference types if NRT unsure.  
    }

    private object? GetEffectiveDefaultValue(Type type, PropertyInfo? propInfo)  
    {  
        // (From specification document, Section 2.2 \- DefaultValue from C\# initializers or parameterless constructors)  
        // 1\. If propInfo exists and has an initializer: (VERY HARD to get via reflection directly)  
        //    \- One trick: if propInfo.DeclaringType has a param-less ctor, create instance, read value.  
        //      \`var instance \= Activator.CreateInstance(propInfo.DeclaringType); return propInfo.GetValue(instance);\`  
        //      This has side-effects and assumes a simple public parameterless constructor.  
        // 2\. If type has a parameterless constructor:  
        //    \`if (type.GetConstructor(Type.EmptyTypes) \!= null) return Activator.CreateInstance(type);\`  
        // 3\. Else, C\# type defaults:  
        //    \`if (type.IsValueType) return Activator.CreateInstance(type); else return null;\`  
        try  
        {  
            if (propInfo?.DeclaringType \!= null && propInfo.CanRead)  
            {  
                 // Attempt to get default from instance (only if simple and parameterless constructor exists for declaring type)  
                 if (propInfo.DeclaringType.GetConstructor(Type.EmptyTypes) \!= null) {  
                    object? instance \= null;  
                    try { instance \= Activator.CreateInstance(propInfo.DeclaringType); } catch {} // May fail  
                    if (instance \!= null) return propInfo.GetValue(instance);  
                 }  
            }  
             if (type.GetConstructor(Type.EmptyTypes) \!= null && \!type.IsAbstract) { // Check IsAbstract  
                return Activator.CreateInstance(type);  
             }  
             if (type.IsValueType) return Activator.CreateInstance(type);  
        }  
        catch (Exception ex)  
        {  
            \_errorMessages.Add($"Schema Info: Could not determine default value for '{propInfo?.Name ?? type.Name}': {ex.Message}");  
        }  
        return null; // Default for reference types or if Activator fails  
    }

    private bool IsCollectionType(Type type, out Type? elementType)  
    {  
        elementType \= null;  
        if (type \== typeof(string)) return false; // String is IEnumerable\<char\> but not a "collection" in this context

        if (type.IsArray)  
        {  
            elementType \= type.GetElementType();  
            return elementType \!= null;  
        }  
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type))  
        {  
            // For generic collections like List\<T\>, ICollection\<T\>, IEnumerable\<T\>  
            if (type.IsGenericType)  
            {  
                elementType \= type.GetGenericArguments().FirstOrDefault();  
                return elementType \!= null;  
            }  
            // For non-generic collections (e.g., ArrayList), element type is object.  
            // This might be too broad or require special handling if you want to support them.  
            // For now, let's primarily focus on generic collections.  
            // elementType \= typeof(object);  
            // return true;  
        }  
        return false;  
    }

    private bool IsDictionaryStringKeyType(Type type, out Type? valueType)  
    {  
        valueType \= null;  
        if (type.IsGenericType &&  
            (type.GetGenericTypeDefinition() \== typeof(IDictionary\<,\>) ||  
             type.GetGenericTypeDefinition() \== typeof(Dictionary\<,\>) ||  
             type.GetGenericTypeDefinition() \== typeof(IReadOnlyDictionary\<,\>))) // Add other common dictionary types  
        {  
            var genericArgs \= type.GetGenericArguments();  
            if (genericArgs\[0\] \== typeof(string))  
            {  
                valueType \= genericArgs\[1\];  
                return true;  
            }  
        }  
        return false;  
    }  
      
    private bool ShouldBeTreatedAsObject(Type type) {  
        if (type.IsPrimitive || type \== typeof(string) || type.IsEnum || type \== typeof(decimal) || type \== typeof(DateTime) || type \== typeof(DateTimeOffset) || type \== typeof(TimeSpan) || type \== typeof(Guid))  
            return false;  
        if (IsCollectionType(type, out \_) || IsDictionaryStringKeyType(type, out \_))  
            return false;  
        return type.IsClass || (type.IsValueType && \!type.IsEnum && \!type.IsPrimitive); // Structs are objects too  
    }  
}

**Key Considerations During Implementation:**

* **Thorough Reflection:** This process is heavily reliant on accurate .NET reflection. Test with various C\# class structures (nested classes, generics, inheritance though schema attributes are not inherited by default for `ConfigSchemaAttribute`, various property types, different attributes).  
* **Error Handling and Logging:** Add extensive logging (using NLog as specified) within `BuildSchemaRecursive` and `ProcessAssembly` to capture issues encountered during reflection (e.g., type load errors, inaccessible members, attribute parsing issues). Add these to `_errorMessages`.  
* **Default Value Complexity:** As noted, getting default values from property initializers (`public string Name { get; set; } = "MyDefault";`) is the hardest part to do perfectly with reflection alone without instantiating the parent object. The provided `GetEffectiveDefaultValue` gives a strategy.  
* **NRT Interpretation:** Accurately determining `IsRequired` from NRT annotations via reflection is complex. The `System.Runtime.CompilerServices.NullableAttribute` and `NullableContextAttribute` provide some information, but it's not straightforward. Start with simpler rules (value types, `RequiredMemberAttribute`) and refine if needed.  
* **Performance:** Reflection can be slow. The `LoadSchemasFromAssembliesAsync` uses `Task.Run` for assembly loading, which helps. If schema construction itself becomes a bottleneck for very large/many schemas, you might consider caching strategies later (as a future consideration).  
* **Testing:** Create a separate test project with various C\# classes using all supported attributes and structures to thoroughly test the `SchemaLoaderService`.

This provides a more detailed skeleton for the schema extraction logic. Remember to implement the helper methods (`IsCollectionType`, `IsDictionaryStringKeyType`, `ShouldBeTreatedAsObject`, etc.) robustly.

# Mapping schema to dom

Okay, implementing the mapping between your `DomNode` tree and your `SchemaNode` definitions is a critical step that enables all schema-aware features like validation, context-sensitive editing, and displaying schema-derived information. This process primarily happens after a DOM tree is built (from parsing a file or creating a new one) and after schemas are loaded.

Here's how you would implement this, mainly within the `MainViewModel` or a dedicated service it uses, following the principles in your specification:

**Core Idea:**

The goal is to populate a dictionary, `_domToSchemaMap: Dictionary<DomNode, SchemaNode?>`, where each `DomNode` in your currently loaded document is associated with its corresponding `SchemaNode`. A `DomNode` might have a `null` `SchemaNode` if it doesn't fall under any defined schema (i.e., it's an "unschematized" part of the JSON).

The mapping logic relies on:

1. **`MountPath`s:** Defined in `[ConfigSchemaAttribute]` to anchor parts of your schema tree to specific paths in the DOM.  
2. **Recursive Traversal:** Both the DOM tree and the schema tree (implicitly via `SchemaNode.Properties` and `SchemaNode.ItemSchema`) are traversed.

**1\. `MainViewModel` \- Orchestrating the Mapping Process:**

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
// ... (previous using statements and class structure) ...  
using JsonConfigEditor.Core.Dom;  
using JsonConfigEditor.Core.Schema;  
using JsonConfigEditor.Core.SchemaLoading; // For ISchemaLoaderService

public class MainViewModel : ViewModelBase  
{  
    // ... (existing properties: \_rootDomNode, FlatItemsSource, \_schemaLoader, \_undoRedoService, \_dialogService) ...

    /// \<summary\>  
    /// Stores the mapping between each DomNode in the current document and its corresponding SchemaNode.  
    /// A DomNode might map to null if it's unschematized.  
    /// (From specification document, Section 2.2)  
    /// \</summary\>  
    private readonly Dictionary\<DomNode, SchemaNode?\> \_domToSchemaMap \= new Dictionary\<DomNode, SchemaNode?\>();

    // This method is called:  
    // 1\. After a JSON file is successfully parsed into \_rootDomNode.  
    // 2\. After schemas are loaded/reloaded by \_schemaLoader.  
    // 3\. After a "New File" operation where \_rootDomNode is initialized.  
    // (From specification document, Section 2.2, 2.8)  
    public void RebuildDomToSchemaMapping()  
    {  
        \_domToSchemaMap.Clear();  
        if (\_rootDomNode \== null || \_schemaLoader.RootSchemas.Count \== 0\)  
        {  
            // No DOM or no schemas loaded, so nothing to map.  
            // Ensure all existing VMs are updated to reflect no schema.  
            foreach(var vm in FlatItemsSource) { vm.UpdateSchemaInfo(null); }  
            return;  
        }

        // Start recursive mapping from the root of the DOM tree.  
        // The initial parentSchemaContext is null; the method will try to find a mounted schema.  
        MapNodeAndChildrenRecursive(\_rootDomNode, null, ""); // "" is the path for the root

        // After mapping, refresh the ViewModels in FlatItemsSource so they get their SchemaNode.  
        RefreshAllViewModelsWithSchemaInfo();  
    }

    /// \<summary\>  
    /// Recursively traverses the DomNode tree, determining and assigning the appropriate  
    /// SchemaNode for each DomNode.  
    /// \</summary\>  
    /// \<param name="domNode"\>The current DomNode to map.\</param\>  
    /// \<param name="parentSchemaContext"\>The SchemaNode of the direct parent DomNode (if any, and if schematized).  
    /// This is used to find schemas for properties of an object or items of an array if no more specific MountPath applies.\</param\>  
    /// \<param name="currentPath"\>The full path to the current domNode.\</param\>  
    private void MapNodeAndChildrenRecursive(DomNode domNode, SchemaNode? parentSchemaContext, string currentPath)  
    {  
        SchemaNode? effectiveSchemaNode \= null;

        // Step 1: Try to find a schema based on a direct MountPath match for the currentPath.  
        // "find the most specific (longest matching) MountPath that is an ancestor of or equal to the DomNode's path"  
        // (From specification document, Section 2.2, Clarification 4\)  
        string longestMatchingMountPath \= string.Empty;  
        SchemaNode? mountedRootSchema \= null;

        foreach (var rootSchemaPair in \_schemaLoader.RootSchemas)  
        {  
            if (currentPath.StartsWith(rootSchemaPair.Key) && rootSchemaPair.Key.Length \> longestMatchingMountPath.Length)  
            {  
                longestMatchingMountPath \= rootSchemaPair.Key;  
                mountedRootSchema \= rootSchemaPair.Value;  
            }  
        }

        if (mountedRootSchema \!= null)  
        {  
            // A MountPath applies. Navigate from this mountedRootSchema to the specific schema for domNode.  
            effectiveSchemaNode \= FindSchemaInHierarchy(mountedRootSchema, currentPath, longestMatchingMountPath);  
        }  
        else if (parentSchemaContext \!= null) // Step 2: If no MountPath, try to derive from parent's schema context.  
        {  
            if (domNode.Parent is ObjectNode && parentSchemaContext.NodeType \== SchemaNodeType.Object)  
            {  
                effectiveSchemaNode \= parentSchemaContext.Properties?.TryGetValue(domNode.Name, out var propSchema) \== true  
                                        ? propSchema  
                                        : parentSchemaContext.AdditionalPropertiesSchema; // Fallback to additionalProperties if allowed  
            }  
            else if (domNode.Parent is ArrayNode && parentSchemaContext.NodeType \== SchemaNodeType.Array)  
            {  
                effectiveSchemaNode \= parentSchemaContext.ItemSchema;  
            }  
        }  
        // If effectiveSchemaNode is still null here, the domNode is unschematized for this path.

        \_domToSchemaMap\[domNode\] \= effectiveSchemaNode;

        // Step 3: Recurse for children  
        if (domNode is ObjectNode objectNode)  
        {  
            foreach (var childPair in objectNode.Children)  
            {  
                string childPath \= $"{currentPath}{(string.IsNullOrEmpty(currentPath) || currentPath \== "/" ? "" : "/")}{childPair.Key}";  
                MapNodeAndChildrenRecursive(childPair.Value, effectiveSchemaNode, childPath); // Pass current node's schema as parent context  
            }  
        }  
        else if (domNode is ArrayNode arrayNode)  
        {  
            for (int i \= 0; i \< arrayNode.Items.Count; i++)  
            {  
                string childPath \= $"{currentPath}/{i}"; // Array items use index in path  
                MapNodeAndChildrenRecursive(arrayNode.Items\[i\], effectiveSchemaNode, childPath); // Pass current node's schema as parent context  
            }  
        }  
    }

    /// \<summary\>  
    /// Given a root SchemaNode (that was matched by a MountPath) and a full DOM path,  
    /// navigates within the SchemaNode structure to find the specific SchemaNode corresponding  
    /// to the tail end of the fullDomPath.  
    /// \</summary\>  
    /// \<param name="currentSchemaScope"\>The SchemaNode that serves as the root for the current scope (e.g., from a MountPath).\</param\>  
    /// \<param name="fullDomPath"\>The complete path of the DomNode we are trying to find a schema for.\</param\>  
    /// \<param name="basePathCoveredByScope"\>The portion of fullDomPath that currentSchemaScope already covers (its MountPath).\</param\>  
    /// \<returns\>The specific SchemaNode for the DomNode, or null if not found within this scope.\</returns\>  
    private SchemaNode? FindSchemaInHierarchy(SchemaNode currentSchemaScope, string fullDomPath, string basePathCoveredByScope)  
    {  
        if (\!fullDomPath.StartsWith(basePathCoveredByScope))  
        {  
            return null; // Should not happen if called correctly  
        }

        if (fullDomPath.Length \== basePathCoveredByScope.Length || // Paths are identical  
            (fullDomPath.Length \== basePathCoveredByScope.Length \+1 && fullDomPath.EndsWith("/"))) // or identical ending in slash  
        {  
            return currentSchemaScope; // The domNode corresponds directly to this mounted schema  
        }

        // Get the remaining path segments to navigate within the currentSchemaScope  
        string relativePath \= fullDomPath.Substring(basePathCoveredByScope.Length).TrimStart('/');  
        if (string.IsNullOrEmpty(relativePath)) return currentSchemaScope; // Should have been caught above

        string\[\] segments \= relativePath.Split('/');  
        SchemaNode? currentSegmentSchema \= currentSchemaScope;

        foreach (string segment in segments)  
        {  
            if (currentSegmentSchema \== null) return null; // Path goes deeper than schema defines

            if (currentSegmentSchema.NodeType \== SchemaNodeType.Object)  
            {  
                currentSegmentSchema \= currentSegmentSchema.Properties?.TryGetValue(segment, out var propSchema) \== true  
                                        ? propSchema  
                                        : currentSegmentSchema.AdditionalPropertiesSchema;  
            }  
            else if (currentSegmentSchema.NodeType \== SchemaNodeType.Array)  
            {  
                // If segment is numeric, it's an array index. All items share ItemSchema.  
                if (int.TryParse(segment, out \_))  
                {  
                    currentSegmentSchema \= currentSegmentSchema.ItemSchema;  
                }  
                else  
                {  
                    return null; // Path segment in an array context is not an index  
                }  
            }  
            else // Value node, cannot go deeper  
            {  
                return null;  
            }  
        }  
        return currentSegmentSchema;  
    }

    /// \<summary\>  
    /// Constructs the canonical path for a given DomNode.  
    /// Example: "config/users/0/name"  
    /// \</summary\>  
    public string GetDomNodePath(DomNode node) // Made public if other services need it  
    {  
        if (node.Parent \== null) // Root node  
        {  
            // Root node might have a special name like "$root" or be the mount path itself.  
            // For path construction, often the "root" itself implies an empty path or "/".  
            // Let's use empty string for root, segments will be added.  
            return node.Name \== "$root" ? "" : node.Name; // If root has a meaningful name (like a top-level mount)  
        }

        var segments \= new LinkedList\<string\>();  
        DomNode? current \= node;  
        while (current \!= null && current.Parent \!= null) // Stop before adding the root's own name if it's a placeholder  
        {  
            segments.AddFirst(current.Name);  
            current \= current.Parent;  
        }  
        // If the ultimate root was named "$root", we don't include it.  
        // Otherwise, if it had a meaningful name (e.g. from a MountPath like "config"), include it.  
        if (current \!= null && current.Name \!= "$root") {  
             segments.AddFirst(current.Name);  
        }

        return string.Join("/", segments);  
    }

    /// \<summary\>  
    /// Iterates through the FlatItemsSource and updates each ViewModel's AssociatedSchemaNode.  
    /// \</summary\>  
    private void RefreshAllViewModelsWithSchemaInfo()  
    {  
        foreach (var vm in FlatItemsSource)  
        {  
            if (vm.DomNode \!= null && \_domToSchemaMap.TryGetValue(vm.DomNode, out var schemaNode))  
            {  
                vm.UpdateSchemaInfo(schemaNode);  
            }  
            else if (vm.IsSchemaOnlyNode) // Schema-only VMs already have their SchemaContextNode  
            {  
                vm.UpdateSchemaInfo(vm.SchemaContextNode); // Or ensure it's correctly set  
            }  
            else  
            {  
                vm.UpdateSchemaInfo(null); // Unschematized  
            }  
        }  
    }

    /// \<summary\>  
    /// Gets the mapped SchemaNode for a given DomNode.  
    /// Public accessor if needed by other parts of the UI or services.  
    /// \</summary\>  
    public SchemaNode? GetSchemaForDomNode(DomNode domNode)  
    {  
        \_domToSchemaMap.TryGetValue(domNode, out var schemaNode);  
        return schemaNode;  
    }

    /// \<summary\>  
    /// Maps a single, newly added DomNode to its schema.  
    /// Useful after operations like "Add Item" or "Materialize Schema Node".  
    /// (From specification document, Section 2.2)  
    /// \</summary\>  
    public void MapSingleNewDomNode(DomNode newDomNode, DomNode parentDomNode)  
    {  
        if (newDomNode \== null || parentDomNode \== null) return;

        SchemaNode? parentSchema \= GetSchemaForDomNode(parentDomNode);  
        string domNodePath \= GetDomNodePath(newDomNode); // Calculate its full path

        // This logic is similar to the recursive one but targeted.  
        SchemaNode? effectiveSchemaNode \= null;  
        string longestMatchingMountPath \= string.Empty;  
        SchemaNode? mountedRootSchema \= null;

        foreach (var rootSchemaPair in \_schemaLoader.RootSchemas) {  
            if (domNodePath.StartsWith(rootSchemaPair.Key) && rootSchemaPair.Key.Length \> longestMatchingMountPath.Length) {  
                longestMatchingMountPath \= rootSchemaPair.Key;  
                mountedRootSchema \= rootSchemaPair.Value;  
            }  
        }  
        if (mountedRootSchema \!= null) {  
            effectiveSchemaNode \= FindSchemaInHierarchy(mountedRootSchema, domNodePath, longestMatchingMountPath);  
        } else if (parentSchema \!= null) {  
             if (parentDomNode is ObjectNode && parentSchema.NodeType \== SchemaNodeType.Object) {  
                effectiveSchemaNode \= parentSchema.Properties?.TryGetValue(newDomNode.Name, out var ps) \== true ? ps : parentSchema.AdditionalPropertiesSchema;  
            } else if (parentDomNode is ArrayNode && parentSchema.NodeType \== SchemaNodeType.Array) {  
                effectiveSchemaNode \= parentSchema.ItemSchema;  
            }  
        }  
        \_domToSchemaMap\[newDomNode\] \= effectiveSchemaNode;

        // If this newDomNode has children (e.g. created from DomFactory with defaults), map them too.  
        if (newDomNode is ObjectNode on) {  
            foreach(var child in on.Children.Values) MapSingleNewDomNode(child, newDomNode); // Recursive call for children of newly added node  
        } else if (newDomNode is ArrayNode an) {  
            foreach(var item in an.Items) MapSingleNewDomNode(item, newDomNode);  
        }

        // Update the VM if it exists in FlatItemsSource  
        var vm \= FlatItemsSource.FirstOrDefault(x \=\> x.DomNode \== newDomNode);  
        vm?.UpdateSchemaInfo(effectiveSchemaNode);  
    }

    /// \<summary\>  
    /// Removes a DomNode and its descendants from the schema map.  
    /// Called when a DomNode is deleted.  
    /// (From specification document, Section 2.2)  
    /// \</summary\>  
    public void UnmapDomNodeAndDescendants(DomNode domNode)  
    {  
        if (domNode \== null) return;  
        \_domToSchemaMap.Remove(domNode);  
        if (domNode is ObjectNode on)  
        {  
            foreach (var child in on.Children.Values.ToList()) UnmapDomNodeAndDescendants(child); // ToList due to potential modification  
        }  
        else if (domNode is ArrayNode an)  
        {  
            foreach (var item in an.Items.ToList()) UnmapDomNodeAndDescendants(item);  
        }  
    }  
}

**2\. `DataGridRowItemViewModel` Update:**

The `UpdateSchemaInfo` method (previously `ApplySchemaInfo`) is used by `MainViewModel` to push the resolved schema to the VM.

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/DataGridRowItemViewModel.cs \---  
public class DataGridRowItemViewModel : ViewModelBase  
{  
    public SchemaNode? AssociatedSchemaNode { get; private set; }

    /// \<summary\>  
    /// Updates the ViewModel with its associated SchemaNode.  
    /// Called by MainViewModel after schema mapping or when schema context changes.  
    /// \</summary\>  
    public void UpdateSchemaInfo(SchemaNode? schemaNode)  
    {  
        bool changed \= AssociatedSchemaNode \!= schemaNode;  
        AssociatedSchemaNode \= schemaNode;  
        if (changed)  
        {  
            // Raise PropertyChanged for any properties that depend on schema info  
            OnPropertyChanged(nameof(AssociatedSchemaNode));  
            OnPropertyChanged(nameof(IsNodeReadOnly));  
            OnPropertyChanged(nameof(NodeTooltip)); // Example dependent property  
            OnPropertyChanged(nameof(IsEditable));  
            OnPropertyChanged(nameof(ValueDisplay)); // ValueDisplay might change based on schema (e.g. for enums)  
        }  
    }  
    // ... other properties and methods ...  
}

**Integration Points:**

**After JSON Load:** In `MainViewModel.LoadJsonAsync()`, after `_jsonParser.Parse()` successfully sets `_rootDomNode`:  
 C\#  
\_rootDomNode \= \_jsonParser.Parse(jsonContent);  
if (\_rootDomNode \!= null)  
{  
    RebuildDomToSchemaMapping(); // This will populate \_domToSchemaMap and update VMs  
    // ValidateFullDocument(); // Then run validation  
    RefreshDisplayList(); // Then build the FlatItemsSource for the UI  
}

1. 

**After "New File":** When `_rootDomNode` is initialized to an empty object `{}`:  
 C\#  
// In ExecuteNewFile()  
\_rootDomNode \= new ObjectNode("$root", null); // Or from DomFactory with root schema  
RebuildDomToSchemaMapping();  
// ValidateFullDocument();  
RefreshDisplayList();

2.   
3. **After Schemas Load/Reload:** If you implement a schema refresh mechanism independent of file loading, `RebuildDomToSchemaMapping()` must be called.  
4. **After Adding a `DomNode`:** When a node is added programmatically (e.g., materializing a schema-only node, user adds array item/object property via UI):  
   * Call `MainViewModel.MapSingleNewDomNode(newNode, parentNode)` for the newly added node and its children if it's complex.  
5. **After Deleting a `DomNode`:**  
   * Call `MainViewModel.UnmapDomNodeAndDescendants(deletedNode)` to clean up the `_domToSchemaMap`.

This structure provides a robust way to map your DOM to your schemas, handling mounted schemas and deriving context from parents. The path generation and navigation within schema hierarchies are the most complex parts to get right.

# Undo, Redo

Okay, implementing a robust Undo/Redo system is essential for a good user experience in an editor. This involves tracking changes to your DOM tree and providing a mechanism to revert and reapply those changes. We'll base this on **Spec Section 2.14** and related interactions.

**Core Components:**

1. **`IUndoableOperation` Interface:** Defines the contract for any action that can be undone and redone.  
2. **Concrete Operation Classes:** Implementations of `IUndoableOperation` for specific actions (e.g., changing a value, adding a node, deleting a node).  
3. **`UndoRedoService`:** Manages the undo and redo stacks and orchestrates the operations.  
4. **`MainViewModel` Integration:** Uses the `UndoRedoService` to record operations and provide Undo/Redo commands to the UI.

---

**1\. `IUndoableOperation` Interface (`JsonConfigEditor.Core.UndoRedo`)**

This was defined in a previous skeleton, and it's central.

C\#  
// \--- File: JsonConfigEditor.Core/UndoRedo/IUndoableOperation.cs \---  
namespace JsonConfigEditor.Core.UndoRedo  
{  
    /// \<summary\>  
    /// Defines the contract for an operation that can be undone and redone within the application.  
    /// Each discrete, user-initiated change that modifies the DOM should be encapsulated by an  
    /// implementation of this interface.  
    /// (From specification document, Section 2.14)  
    /// \</summary\>  
    public interface IUndoableOperation  
    {  
        /// \<summary\>  
        /// Reverts the changes made by this operation.  
        /// This method should restore the DOM to its state before the operation was performed.  
        /// It also needs to trigger UI updates (typically via the MainViewModel).  
        /// \</summary\>  
        void Undo();

        /// \<summary\>  
        /// Re-applies the changes made by this operation after it has been undone.  
        /// This method should restore the DOM to its state after the operation was initially performed.  
        /// It also needs to trigger UI updates.  
        /// \</summary\>  
        void Redo();

        /// \<summary\>  
        /// Gets a human-readable description of the operation (e.g., "Change value of 'port' to 8080").  
        /// This can be used for debugging or potentially in the UI (e.g., "Undo Change Port Value").  
        /// \</summary\>  
        string Description { get; }  
    }  
}

---

**2\. Concrete Operation Classes (`JsonConfigEditor.Core.UndoRedo`)**

You'll need several of these.

**`ChangeValueOperation`:** For `ValueNode` value changes and `RefNode` path changes.

 C\#  
// \--- File: JsonConfigEditor.Core/UndoRedo/ChangeValueOperation.cs \---  
using JsonConfigEditor.Core.Dom;  
using System.Text.Json; // For JsonElement  
using JsonConfigEditor.Wpf.ViewModels; // For MainViewModel (or an action to refresh UI)

namespace JsonConfigEditor.Core.UndoRedo  
{  
    public class ChangeValueOperation : IUndoableOperation  
    {  
        private readonly DomNode \_targetNode; // Can be ValueNode or RefNode  
        private readonly JsonElement \_oldValueJson; // For ValueNode  
        private readonly JsonElement \_newValueJson; // For ValueNode  
        private readonly string? \_oldRefPath;     // For RefNode  
        private readonly string? \_newRefPath;     // For RefNode  
        private readonly MainViewModel \_mainViewModel; // To trigger UI refresh and selection

        public string Description { get; }

        // Constructor for ValueNode changes  
        public ChangeValueOperation(ValueNode targetNode, JsonElement oldValue, JsonElement newValue, MainViewModel mainViewModel)  
        {  
            \_targetNode \= targetNode;  
            \_oldValueJson \= oldValue.Clone(); // Clone to ensure lifetime  
            \_newValueJson \= newValue.Clone();  
            \_mainViewModel \= mainViewModel;  
            Description \= $"Change value of '{targetNode.Name}' from '{\_oldValueJson}' to '{\_newValueJson}'";  
        }

        // Constructor for RefNode path changes  
        public ChangeValueOperation(RefNode targetNode, string oldPath, string newPath, MainViewModel mainViewModel)  
        {  
            \_targetNode \= targetNode;  
            \_oldRefPath \= oldPath;  
            \_newRefPath \= newPath;  
            \_mainViewModel \= mainViewModel;  
            Description \= $"Change $ref path of '{targetNode.Name}' from '{oldPath}' to '{newPath}'";  
        }

        public void Undo()  
        {  
            if (\_targetNode is ValueNode vn) vn.Value \= \_oldValueJson.Clone();  
            else if (\_targetNode is RefNode rn) rn.ReferencePath \= \_oldRefPath\!;  
            \_mainViewModel.NotifyDomChanged(\_targetNode, ChangeType.Modified); // Notify UI to refresh this node  
        }

        public void Redo()  
        {  
            if (\_targetNode is ValueNode vn) vn.Value \= \_newValueJson.Clone();  
            else if (\_targetNode is RefNode rn) rn.ReferencePath \= \_newRefPath\!;  
            \_mainViewModel.NotifyDomChanged(\_targetNode, ChangeType.Modified); // Notify UI to refresh this node  
        }  
    }  
}

* 

**`AddNodeOperation`:** For adding a new node.

 C\#  
// \--- File: JsonConfigEditor.Core/UndoRedo/AddNodeOperation.cs \---  
using JsonConfigEditor.Core.Dom;  
using JsonConfigEditor.Wpf.ViewModels;

namespace JsonConfigEditor.Core.UndoRedo  
{  
    public class AddNodeOperation : IUndoableOperation  
    {  
        private readonly DomNode \_parentNode;  
        private readonly DomNode \_addedNode;  
        private readonly int \_indexInArray; // \-1 if added to ObjectNode, or if order doesn't matter / added at end

        public string Description \=\> $"Add node '{\_addedNode.Name}' to '{\_parentNode.Name}'";  
        private readonly MainViewModel \_mainViewModel;

        public AddNodeOperation(DomNode parentNode, DomNode addedNode, int indexInArray, MainViewModel mainViewModel)  
        {  
            \_parentNode \= parentNode;  
            \_addedNode \= addedNode;  
            \_indexInArray \= indexInArray;  
            \_mainViewModel \= mainViewModel;  
        }

        public void Undo() // Effectively deletes the node  
        {  
            if (\_parentNode is ObjectNode on) on.Children.Remove(\_addedNode.Name);  
            else if (\_parentNode is ArrayNode an) an.Items.Remove(\_addedNode);  
            // Important: If ArrayNode, re-index subsequent items if necessary  
            \_mainViewModel.UnmapDomNodeAndDescendants(\_addedNode);  
            \_mainViewModel.NotifyDomChanged(\_parentNode, ChangeType.StructureChanged); // Parent's structure changed  
        }

        public void Redo() // Re-adds the node  
        {  
            \_addedNode.Parent \= \_parentNode; // Re-assign parent  
            if (\_parentNode is ObjectNode on) on.Children\[\_addedNode.Name\] \= \_addedNode;  
            else if (\_parentNode is ArrayNode an)  
            {  
                if (\_indexInArray \>= 0 && \_indexInArray \<= an.Items.Count) an.Items.Insert(\_indexInArray, \_addedNode);  
                else an.Items.Add(\_addedNode); // Add to end if index is invalid  
            }  
            // Important: If ArrayNode, re-index subsequent items if necessary  
            \_mainViewModel.MapSingleNewDomNode(\_addedNode, \_parentNode);  
            \_mainViewModel.NotifyDomChanged(\_parentNode, ChangeType.StructureChanged);  
        }  
    }  
}

* 

**`DeleteNodeOperation`:** For deleting a node.

 C\#  
// \--- File: JsonConfigEditor.Core/UndoRedo/DeleteNodeOperation.cs \---  
using JsonConfigEditor.Core.Dom;  
using JsonConfigEditor.Wpf.ViewModels;

namespace JsonConfigEditor.Core.UndoRedo  
{  
    public class DeleteNodeOperation : IUndoableOperation  
    {  
        private readonly DomNode \_parentNode;  
        private readonly DomNode \_deletedNode; // Keep a full copy/snapshot if needed, or just essentials  
        private readonly string \_deletedNodeName;  
        private readonly JsonElement? \_deletedValueNodeValue; // If it was a ValueNode  
        private readonly string? \_deletedRefNodePath;    // If it was a RefNode  
        // If \_deletedNode was Object/Array, need to store its children to reconstruct fully for redo.  
        // For simplicity here, assume \_deletedNode can be re-added.  
        private readonly int \_originalIndexInArray;  
        private readonly MainViewModel \_mainViewModel;

        public string Description \=\> $"Delete node '{\_deletedNodeName}' from '{\_parentNode.Name}'";

        public DeleteNodeOperation(DomNode parentNode, DomNode deletedNode, int originalIndexInArray, MainViewModel mainViewModel)  
        {  
            \_parentNode \= parentNode;  
            \_deletedNode \= deletedNode; // This should be a clone or enough info to rebuild  
            \_deletedNodeName \= deletedNode.Name;  
            if (deletedNode is ValueNode vn) \_deletedValueNodeValue \= vn.Value.Clone();  
            if (deletedNode is RefNode rn) \_deletedRefNodePath \= rn.ReferencePath;  
            \_originalIndexInArray \= originalIndexInArray;  
            \_mainViewModel \= mainViewModel;  
        }

        public void Undo() // Re-adds the node  
        {  
            \_deletedNode.Parent \= \_parentNode; // Restore parent link  
            if (\_parentNode is ObjectNode on) on.Children\[\_deletedNodeName\] \= \_deletedNode;  
            else if (\_parentNode is ArrayNode an)  
            {  
                if (\_originalIndexInArray \>= 0 && \_originalIndexInArray \<= an.Items.Count) an.Items.Insert(\_originalIndexInArray, \_deletedNode);  
                else an.Items.Add(\_deletedNode);  
            }  
            // Re-map the node and its children if it's complex  
            \_mainViewModel.MapSingleNewDomNode(\_deletedNode, \_parentNode);  
            \_mainViewModel.NotifyDomChanged(\_parentNode, ChangeType.StructureChanged);  
        }

        public void Redo() // Actually performs the delete  
        {  
            if (\_parentNode is ObjectNode on) on.Children.Remove(\_deletedNodeName);  
            else if (\_parentNode is ArrayNode an) an.Items.Remove(\_deletedNode);  
            \_mainViewModel.UnmapDomNodeAndDescendants(\_deletedNode);  
            \_mainViewModel.NotifyDomChanged(\_parentNode, ChangeType.StructureChanged);  
        }  
    }  
}

* 

**`CompositeOperation` (Optional but good for multi-part changes):**

 C\#  
// \--- File: JsonConfigEditor.Core/UndoRedo/CompositeOperation.cs \---  
namespace JsonConfigEditor.Core.UndoRedo  
{  
    public class CompositeOperation : IUndoableOperation  
    {  
        private readonly List\<IUndoableOperation\> \_operations \= new List\<IUndoableOperation\>();  
        public string Description { get; }

        public CompositeOperation(string description, IEnumerable\<IUndoableOperation\>? initialOperations \= null)  
        {  
            Description \= description;  
            if (initialOperations \!= null) \_operations.AddRange(initialOperations);  
        }

        public void AddOperation(IUndoableOperation operation) \=\> \_operations.Add(operation);

        public void Undo()  
        {  
            // Undo operations in reverse order of how they were added/performed  
            for (int i \= \_operations.Count \- 1; i \>= 0; i--)  
            {  
                \_operations\[i\].Undo();  
            }  
        }

        public void Redo()  
        {  
            // Redo operations in the original order  
            foreach (var operation in \_operations)  
            {  
                operation.Redo();  
            }  
        }  
    }  
}

* 

---

**3\. `UndoRedoService` Implementation (`JsonConfigEditor.Core.UndoRedo`)**

C\#  
// \--- File: JsonConfigEditor.Core/UndoRedo/UndoRedoService.cs \---  
namespace JsonConfigEditor.Core.UndoRedo  
{  
    public class UndoRedoService  
    {  
        private readonly Stack\<IUndoableOperation\> \_undoStack \= new Stack\<IUndoableOperation\>();  
        private readonly Stack\<IUndoableOperation\> \_redoStack \= new Stack\<IUndoableOperation\>();  
          
        // To manage IsDirty flag based on save state (Spec Section 2.8, 2.14)  
        private int \_undoCountAtLastSave \= 0;  
        public Action\<bool\>? IsDirtyChangedCallback { get; set; } // MainViewModel can subscribe

        public bool CanUndo \=\> \_undoStack.Any();  
        public bool CanRedo \=\> \_redoStack.Any();

        /// \<summary\>  
        /// Records an operation that can be undone.  
        /// Clears the redo stack whenever a new operation is added.  
        /// (From specification document, Section 2.14)  
        /// \</summary\>  
        public void RecordOperation(IUndoableOperation operation)  
        {  
            \_undoStack.Push(operation);  
            \_redoStack.Clear(); // Any new action clears the redo stack  
            IsDirtyChangedCallback?.Invoke(IsCurrentlyDirty());  
            // Notify CanExecuteChanged for UndoCommand/RedoCommand  
        }

        public void Undo()  
        {  
            if (\!CanUndo) return;  
            var operation \= \_undoStack.Pop();  
            operation.Undo();  
            \_redoStack.Push(operation);  
            IsDirtyChangedCallback?.Invoke(IsCurrentlyDirty());  
        }

        public void Redo()  
        {  
            if (\!CanRedo) return;  
            var operation \= \_redoStack.Pop();  
            operation.Redo();  
            \_undoStack.Push(operation);  
            IsDirtyChangedCallback?.Invoke(IsCurrentlyDirty());  
        }

        public void ClearStacks()  
        {  
            \_undoStack.Clear();  
            \_redoStack.Clear();  
            \_undoCountAtLastSave \= 0; // Reset save point  
            IsDirtyChangedCallback?.Invoke(IsCurrentlyDirty());  
        }

        /// \<summary\>  
        /// Call this after a successful save operation.  
        /// (From specification document, Section 2.8)  
        /// \</summary\>  
        public void MarkCurrentStateAsSaved()  
        {  
            \_undoCountAtLastSave \= \_undoStack.Count;  
            IsDirtyChangedCallback?.Invoke(IsCurrentlyDirty());  
        }

        /// \<summary\>  
        /// Checks if the current state is different from the last saved state.  
        /// (From specification document, Section 2.8, 2.14)  
        /// \</summary\>  
        private bool IsCurrentlyDirty()  
        {  
            return \_undoStack.Count \!= \_undoCountAtLastSave;  
        }  
    }  
}

---

**4\. `MainViewModel` Integration (`JsonConfigEditor.Wpf.ViewModels`)**

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
using JsonConfigEditor.Core.UndoRedo;  
using JsonConfigEditor.Core.Dom; // For ChangeType enum

public enum ChangeType { Modified, StructureChanged, SelectionChanged } // Define this or similar

public class MainViewModel : ViewModelBase  
{  
    public UndoRedoService UndoRedoService { get; } // Initialized in constructor

    // ... (IsDirty property) ...  
    // IsDirty will now be primarily controlled by UndoRedoService's callback

    public MainViewModel(UndoRedoService undoRedoService, /\* other services \*/)  
    {  
        UndoRedoService \= undoRedoService;  
        UndoRedoService.IsDirtyChangedCallback \= (isDirty) \=\> IsDirty \= isDirty; // Link IsDirty

        // Initialize UndoCommand and RedoCommand  
        // UndoCommand \= new RelayCommand(() \=\> UndoRedoService.Undo(), () \=\> UndoRedoService.CanUndo);  
        // RedoCommand \= new RelayCommand(() \=\> UndoRedoService.Redo(), () \=\> UndoRedoService.CanRedo);  
        // Commands need to raise CanExecuteChanged when CanUndo/CanRedo changes.  
        // The UndoRedoService can have events for CanUndoChanged/CanRedoChanged.  
    }

    public void MarkAsDirty() // This might not be needed if UndoRedoService handles it  
    {  
        // IsDirty \= true; // Now managed by UndoRedoService.IsDirtyChangedCallback  
    }  
      
    public void NotifyDomChanged(DomNode changedNode, ChangeType changeType)  
    {  
        // This method is crucial for refreshing the UI after Undo/Redo operations.  
        // 1\. Find the DataGridRowItemViewModel for changedNode.  
        // 2\. If ChangeType.Modified, update its ValueDisplay and other relevant properties.  
        // 3\. If ChangeType.StructureChanged (node added/deleted from parent):  
        //    \- Call ApplyFiltersAndRefreshDisplayList() to rebuild the view.  
        //    \- Ensure the appropriate node (e.g., the one acted upon or its parent) is selected and scrolled into view.  
        //  
        // Example:  
        var vm \= FlatItemsSource.FirstOrDefault(r \=\> r.DomNode \== changedNode);  
        if (vm \!= null)  
        {  
            if (changeType \== ChangeType.Modified)  
            {  
                vm.RefreshDisplayProperties(); // Conceptual method to re-fetch ValueDisplay, etc.  
            }  
            SelectedRowItem \= vm; // Select the affected node  
            RequestScrollToItem(vm);  
        }

        if (changeType \== ChangeType.StructureChanged)  
        {  
            // For structural changes, a full refresh of the flat list is often safest  
            // to ensure correct order, indentation, and placeholders.  
            ApplyFiltersAndRefreshDisplayList();  
            // You might need to re-select the appropriate item after refresh.  
            // The Undo/Redo operation itself could return the DomNode to select.  
        }  
    }

    // \--- In methods that change the DOM \---  
    // Example: After successfully committing an edit in DataGridRowItemViewModel,  
    //          it calls a method in MainViewModel which then records the operation.  
    public void RecordChangeValue(ValueNode targetNode, JsonElement oldValue, JsonElement newValue, DataGridRowItemViewModel vmContext)  
    {  
        UndoRedoService.RecordOperation(new ChangeValueOperation(targetNode, oldValue, newValue, this));  
        // IsDirty is now handled by UndoRedoService callback  
        // The VM (vmContext) should update its ValueDisplay.  
        vmContext.RefreshDisplayProperties(); // Conceptual  
    }

    // Similarly for AddNode, DeleteNode, etc. The operation classes will call MainViewModel.NotifyDomChanged  
    // during their Undo/Redo methods.  
}

// \--- In DataGridRowItemViewModel.cs \---  
public class DataGridRowItemViewModel : ViewModelBase  
{  
    // ...  
    public void RefreshDisplayProperties()  
    {  
        OnPropertyChanged(nameof(ValueDisplay));  
        // OnPropertyChanged for any other display property that might change.  
    }  
    // ...  
}

**Integration Steps:**

1. **Instantiate `UndoRedoService`** in `MainViewModel` (or inject it).  
2. **Hook up `IsDirtyChangedCallback`** from `UndoRedoService` to `MainViewModel.IsDirty`.  
3. **Modify DOM-Changing Methods:**  
   * `DataGridRowItemViewModel.CommitEdit()`: Instead of directly changing `DomNode.Value`, it should create the appropriate `ChangeValueOperation` and pass it to `UndoRedoService.RecordOperation()`. Then, it should *call* `operation.Redo()` once to apply the change for the first time (this is a common pattern: execute by redoing). Or, the operation's constructor could apply the change.  
   * Commands for "Delete Node", "Insert Item/Property", "Reset to Null", "Materialize Schema Node": These should construct and record their respective `IUndoableOperation`s. The operation itself will modify the DOM.  
4. **UI Refresh after Undo/Redo:** The `Undo()` and `Redo()` methods in the concrete operation classes need to call back to `MainViewModel` (e.g., via `MainViewModel.NotifyDomChanged`) so the UI can be updated. This might involve:  
   * Targeted refresh of a `DataGridRowItemViewModel`'s properties.  
   * Full refresh of `FlatItemsSource` via `ApplyFiltersAndRefreshDisplayList()` if the structure changed.  
   * Ensuring the selection and scroll position are appropriate.  
5. **File Operations:**  
   * `SaveFileCommand`: Call `UndoRedoService.MarkCurrentStateAsSaved()`.  
   * `NewFileCommand`, `OpenFileCommand`, `RevertFileCommand`: Call `UndoRedoService.ClearStacks()`.

This structure creates a robust undo/redo system. The key is that every change to the `DomNode` tree goes through an `IUndoableOperation` that knows how to reverse and reapply itself, and these operations are managed by the `UndoRedoService`. The `MainViewModel` then ensures the UI reflects these changes.

# Tree Picker

Okay, let's detail the implementation for the "Tree Picker" modal dialog. This picker is crucial for user-friendly `$ref` path selection and needs to display the combined DOM \+ schema tree structure.

**Core Goal:** Provide a modal dialog with a `TreeView` that shows the combined DOM and schema-only nodes. When a user selects a node in this tree and clicks "OK", the dialog should return the full, canonical path to that selected node.

---

**1\. `PickerTreeNodeViewModel.cs` (New ViewModel for the Tree Picker)**

This ViewModel will represent each node in the `TreeView` within the picker dialog.

C\#  
// \--- File: JsonConfigEditor.Wpf.ViewModels.PickerDialogs.PickerTreeNodeViewModel.cs \---  
using JsonConfigEditor.Core.Dom;  
using JsonConfigEditor.Core.Schema;  
using System.Collections.ObjectModel; // For ObservableCollection  
using System.ComponentModel;         // For INotifyPropertyChanged from ViewModelBase

namespace JsonConfigEditor.Wpf.ViewModels.PickerDialogs  
{  
    /// \<summary\>  
    /// ViewModel for a single node in the TreeView of the RefNodePathPickerDialog.  
    /// Represents either an actual DomNode or a schema-only placeholder.  
    /// \</summary\>  
    public class PickerTreeNodeViewModel : ViewModelBase // Assuming ViewModelBase for INotifyPropertyChanged  
    {  
        private readonly DomNode? \_domNode; // Null if this is a schema-only placeholder  
        private readonly SchemaNode \_schemaContextNode; // Schema for this node (definition if schema-only)  
        private readonly string \_nodeNameInParent; // The actual property name or array index string  
        private readonly RefNodePathPickerViewModel \_parentPickerViewModel; // To communicate selection

        private bool \_isExpanded;

        /// \<summary\>  
        /// Initializes a new instance for an actual DomNode.  
        /// \</summary\>  
        public PickerTreeNodeViewModel(DomNode domNode, SchemaNode schemaContextNode, string fullPath, RefNodePathPickerViewModel parentPickerViewModel)  
        {  
            \_domNode \= domNode;  
            \_schemaContextNode \= schemaContextNode;  
            \_nodeNameInParent \= domNode.Name; // Name from DomNode  
            FullPath \= fullPath;  
            \_parentPickerViewModel \= parentPickerViewModel;  
            Children \= new ObservableCollection\<PickerTreeNodeViewModel\>();  
            // Default expansion: expand if it's an object/array and not too deep, or has children.  
             \_isExpanded \= (\_domNode is ObjectNode || \_domNode is ArrayNode) && (domNode.Depth \< 2);  
        }

        /// \<summary\>  
        /// Initializes a new instance for a schema-only (addable) node.  
        /// \</summary\>  
        public PickerTreeNodeViewModel(SchemaNode schemaNode, string nodeName, string fullPath, RefNodePathPickerViewModel parentPickerViewModel, int depth)  
        {  
            \_domNode \= null; // Mark as schema-only  
            \_schemaContextNode \= schemaNode;  
            \_nodeNameInParent \= nodeName; // Name from schema property  
            FullPath \= fullPath;  
            \_parentPickerViewModel \= parentPickerViewModel;  
            Children \= new ObservableCollection\<PickerTreeNodeViewModel\>();  
            \_isExpanded \= (\_schemaContextNode.NodeType \== SchemaNodeType.Object || \_schemaContextNode.NodeType \== SchemaNodeType.Array) && (depth \< 2);  
        }

        /// \<summary\>  
        /// The underlying DomNode, if this represents an existing node in the document.  
        /// Null for schema-only nodes.  
        /// \</summary\>  
        public DomNode? DomNode \=\> \_domNode;

        /// \<summary\>  
        /// The SchemaNode defining this node's structure and type.  
        /// \</summary\>  
        public SchemaNode SchemaContextNode \=\> \_schemaContextNode;

        /// \<summary\>  
        /// The name of this node as it appears as a property or array index.  
        /// \</summary\>  
        public string Name \=\> \_nodeNameInParent;

        /// \<summary\>  
        /// The full canonical path to this node from the root.  
        /// E.g., "/sectionA/myArray/0/propertyName"  
        /// \</summary\>  
        public string FullPath { get; }

        /// \<summary\>  
        /// Indicates if this node is a placeholder defined by schema but not present in the DOM.  
        /// (From specification document, Section 2.12)  
        /// \</summary\>  
        public bool IsSchemaOnlyNode \=\> \_domNode \== null;

        /// \<summary\>  
        /// Display name for the TreeView item. Could include type hints or default values for schema-only nodes.  
        /// \</summary\>  
        public string DisplayName  
        {  
            get  
            {  
                string namePrefix \= string.Empty;  
                // Example: Add prefix for array items to show index visually, though FullPath is canonical  
                // This depends on how TreeView item templates are structured.  
                // if (Parent is ArrayNode or parent SchemaNode is Array type) namePrefix \= $"\[{Name}\] ";

                string typeSuffix \= IsSchemaOnlyNode ? $" ({\_schemaContextNode.ClrType.Name}, default)" : $" ({\_schemaContextNode.ClrType.Name})";  
                if (\_schemaContextNode.NodeType \== SchemaNodeType.Object && \!IsSchemaOnlyNode && \_domNode is ObjectNode on)  
                    typeSuffix \= $" (Object, {on.Children.Count} props)";  
                else if (\_schemaContextNode.NodeType \== SchemaNodeType.Array && \!IsSchemaOnlyNode && \_domNode is ArrayNode an)  
                    typeSuffix \= $" (Array, {an.Items.Count} items)";  
                else if (\_schemaContextNode.NodeType \== SchemaNodeType.Object && IsSchemaOnlyNode)  
                     typeSuffix \= " (Object, default)";  
                else if (\_schemaContextNode.NodeType \== SchemaNodeType.Array && IsSchemaOnlyNode)  
                     typeSuffix \= " (Array, default)";

                return $"{namePrefix}{Name}{typeSuffix}";  
            }  
        }

        /// \<summary\>  
        /// Child nodes for the TreeView. Populated on demand or during initial tree build.  
        /// \</summary\>  
        public ObservableCollection\<PickerTreeNodeViewModel\> Children { get; }

        /// \<summary\>  
        /// Gets or sets whether this tree node is currently expanded.  
        /// \</summary\>  
        public bool IsExpanded  
        {  
            get \=\> \_isExpanded;  
            set  
            {  
                if (SetProperty(ref \_isExpanded, value) && value) // SetProperty from ViewModelBase  
                {  
                    // Optional: Load children on demand if not already loaded  
                    \_parentPickerViewModel.EnsureChildrenLoaded(this);  
                }  
            }  
        }

        private bool \_isSelected;  
        /// \<summary\>  
        /// Gets or sets whether this tree node is currently selected.  
        /// When set, it should update the PathPickerViewModel's SelectedPath.  
        /// \</summary\>  
        public bool IsSelected  
        {  
            get \=\> \_isSelected;  
            set  
            {  
                if (SetProperty(ref \_isSelected, value) && value)  
                {  
                    \_parentPickerViewModel.SelectedNode \= this;  
                }  
            }  
        }  
    }  
}

---

**2\. `RefNodePathPickerViewModel.cs` (ViewModel for the Dialog Window)**

This ViewModel manages the overall state of the picker dialog.

C\#  
// \--- File: JsonConfigEditor.Wpf.ViewModels.PickerDialogs.RefNodePathPickerViewModel.cs \---  
using JsonConfigEditor.Core.Dom;  
using JsonConfigEditor.Core.Schema;  
using JsonConfigEditor.Core.SchemaLoading; // For ISchemaLoaderService (to access RootSchemas)  
using System.Collections.ObjectModel;  
using System.Windows.Input; // For ICommand

namespace JsonConfigEditor.Wpf.ViewModels.PickerDialogs  
{  
    /// \<summary\>  
    /// ViewModel for the RefNodePathPickerDialog window.  
    /// Manages the tree of selectable nodes and the resulting path.  
    /// \</summary\>  
    public class RefNodePathPickerViewModel : ViewModelBase  
    {  
        private readonly DomNode? \_rootDomNode; // The application's current root DOM  
        private readonly ISchemaLoaderService \_schemaLoader;  
        private readonly Func\<DomNode, SchemaNode?\> \_schemaMapper; // Function to get schema for a DomNode

        private PickerTreeNodeViewModel? \_selectedNode;  
        private string? \_currentPathPreview;

        /// \<summary\>  
        /// The collection of root nodes for the TreeView.  
        /// (From specification document \- Tree Picker should show combined DOM+Schema)  
        /// \</summary\>  
        public ObservableCollection\<PickerTreeNodeViewModel\> RootNodes { get; }

        /// \<summary\>  
        /// Gets or sets the currently selected PickerTreeNodeViewModel in the TreeView.  
        /// \</summary\>  
        public PickerTreeNodeViewModel? SelectedNode  
        {  
            get \=\> \_selectedNode;  
            set  
            {  
                if (SetProperty(ref \_selectedNode, value))  
                {  
                    CurrentPathPreview \= \_selectedNode?.FullPath;  
                    // Optionally, update CanExecute for OKCommand  
                    (OkCommand as RelayCommand)?.RaiseCanExecuteChanged();  
                }  
            }  
        }

        /// \<summary\>  
        /// Gets the preview of the path for the currently selected node.  
        /// \</summary\>  
        public string? CurrentPathPreview  
        {  
            get \=\> \_currentPathPreview;  
            private set \=\> SetProperty(ref \_currentPathPreview, value);  
        }

        /// \<summary\>  
        /// The final selected path to be returned by the dialog.  
        /// \</summary\>  
        public string? ResultPath { get; private set; }

        public ICommand OkCommand { get; }  
        public ICommand CancelCommand { get; } // Typically handled by DialogResult on the Window

        public RefNodePathPickerViewModel(DomNode? rootDomNode, ISchemaLoaderService schemaLoader, Func\<DomNode, SchemaNode?\> schemaMapper)  
        {  
            \_rootDomNode \= rootDomNode;  
            \_schemaLoader \= schemaLoader;  
            \_schemaMapper \= schemaMapper; // Pass the MainViewModel's GetSchemaForDomNode or similar  
            RootNodes \= new ObservableCollection\<PickerTreeNodeViewModel\>();

            OkCommand \= new RelayCommand(AcceptSelection, CanAcceptSelection);  
            // CancelCommand usually just closes the dialog with DialogResult \= false

            PopulateTree();  
        }

        private bool CanAcceptSelection() \=\> SelectedNode \!= null;

        private void AcceptSelection()  
        {  
            ResultPath \= SelectedNode?.FullPath;  
            // Dialog's DialogResult will be set to true by the View's button click  
        }

        /// \<summary\>  
        /// Populates the RootNodes collection for the TreeView.  
        /// This replicates the logic of building the combined DOM \+ Schema view.  
        /// (From specification document \- Tree Picker should show combined DOM+Schema)  
        /// \</summary\>  
        private void PopulateTree()  
        {  
            RootNodes.Clear();  
            // If \_rootDomNode is null, try to build from a root schema ("" MountPath) if available  
            if (\_rootDomNode \!= null)  
            {  
                SchemaNode? rootSchema \= \_schemaMapper(\_rootDomNode); // Get schema for the actual DOM root  
                if (rootSchema \== null && \_schemaLoader.RootSchemas.TryGetValue("", out var defaultRootSchema)) {  
                    rootSchema \= defaultRootSchema; // Fallback to schema mounted at "" if DOM root has no specific schema  
                }

                if (rootSchema \!= null) {  
                    var rootVm \= new PickerTreeNodeViewModel(\_rootDomNode, rootSchema, GetPathForPickerNode(\_rootDomNode.Name, null), this);  
                    RootNodes.Add(rootVm);  
                    EnsureChildrenLoaded(rootVm); // Load first level  
                }  
                // else: What if root DOM node has no schema at all, and no "" mount path schema?  
                // For path picker, we probably still want to show it if it exists.  
                // This requires a "dummy" schema or handling null schema in PickerTreeNodeViewModel.  
            }  
            else if (\_schemaLoader.RootSchemas.TryGetValue("", out var rootSchemaDefinition))  
            {  
                // No DOM, but a root schema definition exists. Show it.  
                var rootVm \= new PickerTreeNodeViewModel(rootSchemaDefinition, rootSchemaDefinition.Name ?? "$schema\_root", "/", this, 0);  
                RootNodes.Add(rootVm);  
                EnsureChildrenLoaded(rootVm);  
            }  
        }

        /// \<summary\>  
        /// Ensures children of a given PickerTreeNodeViewModel are loaded.  
        /// Called when a node is expanded or during initial population.  
        /// \</summary\>  
        public void EnsureChildrenLoaded(PickerTreeNodeViewModel parentPickerVm)  
        {  
            if (parentPickerVm.Children.Any() || \!parentPickerVm.IsExpanded) // Already loaded or not expanded  
            {  
                 // If not expanded but children are requested (e.g. initial load of few levels), proceed if no children yet.  
                if (parentPickerVm.IsExpanded && parentPickerVm.Children.Any()) return;  
            }  
            parentPickerVm.Children.Clear();

            SchemaNode parentSchemaContext \= parentPickerVm.SchemaContextNode;  
            string parentPath \= parentPickerVm.FullPath;  
            int childDepth \= (parentPickerVm.DomNode?.Depth ?? (parentPath.Count(c \=\> c \== '/') \-1)) \+ 1;

            if (parentPickerVm.DomNode is ObjectNode objectNode) // DOM Node exists and is an object  
            {  
                var existingDomPropertyNames \= new HashSet\<string\>(objectNode.Children.Keys);  
                foreach (var childDomNode in objectNode.Children.Values.OrderBy(c \=\> c.Name))  
                {  
                    SchemaNode? childSchema \= parentSchemaContext.Properties?.TryGetValue(childDomNode.Name, out var cs) \== true ? cs  
                                            : parentSchemaContext.AdditionalPropertiesSchema;  
                    if (childSchema \== null) childSchema \= \_schemaMapper(childDomNode); // Try direct map if context is insufficient  
                    if (childSchema \== null) childSchema \= new SchemaNode(childDomNode.Name, childDomNode.GetType(), false, false, null,null,null,null,null,false,null,null,true,null); // Fallback

                    var childVm \= new PickerTreeNodeViewModel(childDomNode, childSchema, GetPathForPickerNode(childDomNode.Name, parentPath), this);  
                    parentPickerVm.Children.Add(childVm);  
                }  
                // Add schema-only properties  
                if (parentSchemaContext.Properties \!= null)  
                {  
                    foreach (var schemaPropertyPair in parentSchemaContext.Properties.OrderBy(p \=\> p.Key))  
                    {  
                        if (\!existingDomPropertyNames.Contains(schemaPropertyPair.Key))  
                        {  
                            var schemaOnlyChildVm \= new PickerTreeNodeViewModel(schemaPropertyPair.Value, schemaPropertyPair.Key, GetPathForPickerNode(schemaPropertyPair.Key, parentPath), this, childDepth);  
                            parentPickerVm.Children.Add(schemaOnlyChildVm);  
                        }  
                    }  
                }  
            }  
            else if (parentPickerVm.DomNode is ArrayNode arrayNode) // DOM Node exists and is an array  
            {  
                SchemaNode? itemSchema \= parentSchemaContext.ItemSchema;  
                if (itemSchema \!= null) {  
                    for (int i \= 0; i \< arrayNode.Items.Count; i++)  
                    {  
                        var childDomNode \= arrayNode.Items\[i\];  
                        var childVm \= new PickerTreeNodeViewModel(childDomNode, itemSchema, GetPathForPickerNode(i.ToString(), parentPath), this);  
                        parentPickerVm.Children.Add(childVm);  
                    }  
                }  
            }  
            else if (parentPickerVm.IsSchemaOnlyNode) // Parent is schema-only, so children are also schema-only  
            {  
                if (parentSchemaContext.NodeType \== SchemaNodeType.Object && parentSchemaContext.Properties \!= null)  
                {  
                    foreach (var schemaPropertyPair in parentSchemaContext.Properties.OrderBy(p \=\> p.Key))  
                    {  
                        var schemaOnlyChildVm \= new PickerTreeNodeViewModel(schemaPropertyPair.Value, schemaPropertyPair.Key, GetPathForPickerNode(schemaPropertyPair.Key, parentPath), this, childDepth);  
                        parentPickerVm.Children.Add(schemaOnlyChildVm);  
                    }  
                }  
                // Schema-only arrays typically don't auto-populate items in a picker unless defaults exist.  
            }  
        }

        /// \<summary\>  
        /// Helper to construct the full path for a picker node.  
        /// Ensures paths start with "/" and handles root.  
        /// \</summary\>  
        private string GetPathForPickerNode(string nodeName, string? parentPath)  
        {  
            if (string.IsNullOrEmpty(parentPath) || parentPath \== "/") // Parent is root  
            {  
                return "/" \+ nodeName;  
            }  
            return parentPath \+ "/" \+ nodeName;  
        }  
    }  
}

**3\. `RefNodePathPickerDialog.xaml` (View for the Dialog):**

XML  
\<Window x:Class="JsonConfigEditor.Wpf.Views.PickerDialogs.RefNodePathPickerDialog"  
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"  
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"  
        xmlns:vm="clr-namespace:JsonConfigEditor.Wpf.ViewModels.PickerDialogs"  
        Title="Select Node Path ($ref)" Height="450" Width="400"  
        WindowStartupLocation="CenterOwner" ShowInTaskbar="False" ResizeMode="CanResizeWithGrip"  
        FocusManager.FocusedElement="{Binding ElementName=NodeTreeView}"\> \<Grid Margin="10"\>  
        \<Grid.RowDefinitions\>  
            \<RowDefinition Height="Auto"/\> \<RowDefinition Height="\*"/\>    \<RowDefinition Height="Auto"/\> \</Grid.RowDefinitions\>

        \<TextBlock Grid.Row="0" Text="Selected Path:" FontWeight="SemiBold"/\>  
        \<TextBox Grid.Row="0" Margin="0,0,0,5" Text="{Binding CurrentPathPreview, Mode=OneWay}" IsReadOnly="True" MinHeight="24" VerticalContentAlignment="Center"/\>  
          
        \<TreeView Grid.Row="1" ItemsSource="{Binding RootNodes}" Name="NodeTreeView" BorderThickness="1" BorderBrush="LightGray"\>  
            \<TreeView.ItemContainerStyle\>  
                \<Style TargetType="{x:Type TreeViewItem}"\>  
                    \<Setter Property="IsExpanded" Value="{Binding IsExpanded, Mode=TwoWay}"/\>  
                    \<Setter Property="IsSelected" Value="{Binding IsSelected, Mode=TwoWay}"/\>  
                    \<Setter Property="Padding" Value="2"/\>  
                    \<Style.Triggers\>  
                        \<DataTrigger Binding="{Binding IsSchemaOnlyNode}" Value="True"\>  
                            \<Setter Property="Foreground" Value="Gray"/\>  
                            \<Setter Property="FontStyle" Value="Italic"/\>  
                        \</DataTrigger\>  
                    \</Style.Triggers\>  
                \</Style\>  
            \</TreeView.ItemContainerStyle\>  
            \<TreeView.ItemTemplate\>  
                \<HierarchicalDataTemplate DataType="{x:Type vm:PickerTreeNodeViewModel}" ItemsSource="{Binding Children}"\>  
                    \<TextBlock Text="{Binding DisplayName}"/\>  
                \</HierarchicalDataTemplate\>  
            \</TreeView.ItemTemplate\>  
        \</TreeView\>

        \<StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,10,0,0"\>  
            \<Button Content="OK" Width="75" Margin="5" IsDefault="True"  
                    Command="{Binding OkCommand}" Click="OkButton\_Click"/\>  
            \<Button Content="Cancel" Width="75" Margin="5" IsCancel="True"/\> \</StackPanel\>  
    \</Grid\>  
\</Window\>

**Code-behind for `RefNodePathPickerDialog.xaml.cs`:**

C\#  
using System.Windows;  
using JsonConfigEditor.Wpf.ViewModels.PickerDialogs;

namespace JsonConfigEditor.Wpf.Views.PickerDialogs  
{  
    public partial class RefNodePathPickerDialog : Window  
    {  
        public RefNodePathPickerDialog(RefNodePathPickerViewModel viewModel)  
        {  
            InitializeComponent();  
            DataContext \= viewModel;  
        }

        private void OkButton\_Click(object sender, RoutedEventArgs e)  
        {  
            // The command will set the ResultPath.  
            // If command can execute and sets a valid path, then DialogResult can be true.  
            if ((DataContext as RefNodePathPickerViewModel)?.OkCommand.CanExecute(null) ?? false)  
            {  
                DialogResult \= true;  
            }  
            else  
            {  
                // Optionally show a message if no valid path is selected but OK is clicked.  
                // Or disable OK button until a path is selected via CanExecute.  
            }  
        }

        public string? SelectedPath \=\> (DataContext as RefNodePathPickerViewModel)?.ResultPath;  
    }  
}

**4\. Showing the Dialog (e.g., from `MainViewModel`):**

C\#  
// \--- In MainViewModel.cs \---  
// Assume OpenRefNodePathEditorModalCommand exists, bound to a button in RefNodePathEditorTemplate for DataGrid cells.  
// Its CommandParameter would be the DataGridRowItemViewModel of the RefNode being edited.

private void ExecuteOpenRefNodePathEditorModal(DataGridRowItemViewModel? refNodeVm)  
{  
    if (refNodeVm \== null || \!(refNodeVm.DomNode is RefNode existingRefNode)) return;

    // Pass current \_rootDomNode, schema loader, and the mapping function to the picker's VM  
    var pickerViewModel \= new RefNodePathPickerViewModel(  
        \_rootDomNode,  
        \_schemaLoader,  
        (domNode) \=\> GetSchemaForDomNode(domNode) // Lambda to pass the mapping lookup  
    );

    var dialog \= new RefNodePathPickerDialog(pickerViewModel)  
    {  
        Owner \= Application.Current.MainWindow // Set owner for proper modal behavior  
    };

    if (dialog.ShowDialog() \== true && \!string.IsNullOrEmpty(dialog.SelectedPath))  
    {  
        // User clicked OK and a path was selected. Update the RefNode.  
        // This is an edit, so it should go through the standard edit/commit/undo process.  
        refNodeVm.IsInEditMode \= true; // Enter edit mode if not already  
        refNodeVm.EditValue \= dialog.SelectedPath; // Set the new path  
        refNodeVm.CommitEdit(); // Commit the change (this will validate path syntax, update DOM, create undo)  
    }  
}

**Key Implementation Details:**

* **Hierarchical Data Binding:** The `TreeView` uses `HierarchicalDataTemplate` with `ItemsSource="{Binding Children}"` on `PickerTreeNodeViewModel`.  
* **Path Construction:** `PickerTreeNodeViewModel.FullPath` is crucial. It's built recursively by concatenating the current node's name with its parent's `FullPath`.  
* **Combined View Logic:** `RefNodePathPickerViewModel.EnsureChildrenLoaded` mirrors the logic of `MainViewModel.AddNodeAndPotentialSchemaChildrenToListRecursive` by adding VMs for both existing `DomNode`s and schema-only placeholders.  
* **On-Demand Loading (Optional):** For very large trees, children in `PickerTreeNodeViewModel` could be populated only when `IsExpanded` becomes true. `EnsureChildrenLoaded` facilitates this.  
* **Selection Handling:** `PickerTreeNodeViewModel.IsSelected` updates `RefNodePathPickerViewModel.SelectedNode`, which in turn updates `CurrentPathPreview`.  
* **Dialog Result:** The dialog returns `true` and the `ResultPath` when the user confirms.

This structure creates a self-contained and reusable tree picker dialog that accurately reflects the combined DOM and schema structure for path selection.

# Focus Management after UI updates

Okay, let's detail the focus management after various UI updates, as this is crucial for a smooth user experience, especially with keyboard navigation and operations that modify the UI structure. The goal is to place the keyboard focus on the most relevant element after an action.

We'll refer to the concrete focus management suggestions from **"config editor specs clarifications 3.md"** (which were accepted and expanded upon in our previous discussions) and integrate them into the actions of the `MainViewModel` and how the View (e.g., `MainWindow.xaml.cs`) might assist.

**Core Principle (from "config editor specs clarifications 3.md"):** "After an action, focus should be placed on the most relevant element, typically the element acted upon or the next logical element for interaction."

**Mechanism for Requesting Focus from ViewModel:**

Since ViewModels shouldn't directly manipulate UI elements like `Focus()`, we'll use an event-based mechanism or a dedicated focus service. For simplicity, let's assume `MainViewModel` can raise an event that the View (e.g., `MainWindow.xaml.cs`) subscribes to.

C\#  
// \--- In MainViewModel.cs \---  
public class MainViewModel : ViewModelBase  
{  
    // ...  
    /// \<summary\>  
    /// Event raised to request that the View set focus to the editor control  
    /// within the DataGridRow for the specified ViewModel.  
    /// \</summary\>  
    public event Action\<DataGridRowItemViewModel?\>? FocusEditorRequested;

    /// \<summary\>  
    /// Event raised to request that the View set focus to a specific DataGridRow.  
    /// \</summary\>  
    public event Action\<DataGridRowItemViewModel?\>? FocusRowRequested;

    /// \<summary\>  
    /// Call this from MainViewModel to ask the View to focus an editor.  
    /// \</summary\>  
    protected void RequestFocusOnEditor(DataGridRowItemViewModel? vm)  
    {  
        FocusEditorRequested?.Invoke(vm);  
    }

    /// \<summary\>  
    /// Call this from MainViewModel to ask the View to focus a row.  
    /// \</summary\>  
    protected void RequestFocusOnRow(DataGridRowItemViewModel? vm)  
    {  
        FocusRowRequested?.Invoke(vm);  
    }  
    // ...  
}

// \--- In MainWindow.xaml.cs (View's code-behind) \---  
public partial class MainWindow : Window  
{  
    public MainWindow()  
    {  
        InitializeComponent();  
        // Assuming DataContext is set to MainViewModel instance  
        var mainViewModel \= DataContext as MainViewModel;  
        if (mainViewModel \!= null)  
        {  
            mainViewModel.FocusEditorRequested \+= OnFocusEditorRequested;  
            mainViewModel.FocusRowRequested \+= OnFocusRowRequested;  
            // mainViewModel.ScrollToItemRequested \+= OnScrollToItemRequested; // Already defined  
        }  
    }

    private void OnFocusEditorRequested(DataGridRowItemViewModel? vmToFocus)  
    {  
        if (vmToFocus \== null) return;

        // Ensure the item is scrolled into view first  
        MainDataGrid.ScrollIntoView(vmToFocus);

        // Dispatcher is often needed because the element might not be immediately available  
        // after a data change or IsInEditMode change.  
        Dispatcher.BeginInvoke(new Action(() \=\>  
        {  
            var row \= MainDataGrid.ItemContainerGenerator.ContainerFromItem(vmToFocus) as DataGridRow;  
            if (row \!= null)  
            {  
                // Find the "Value" column cell (assuming it's the second column, index 1\)  
                DataGridCellsPresenter? presenter \= GetVisualChild\<DataGridCellsPresenter\>(row);  
                if (presenter \== null) return;

                DataGridCell? cell \= presenter.ItemContainerGenerator.ContainerFromIndex(1) as DataGridCell; // Value column  
                if (cell \!= null)  
                {  
                    // The editor control is within the cell's template.  
                    // We need to find it. This assumes a ContentPresenter is used by the DataTemplateSelector.  
                    ContentPresenter contentPresenter \= GetVisualChild\<ContentPresenter\>(cell);  
                    if (contentPresenter \!= null)  
                    {  
                        contentPresenter.ApplyTemplate(); // Ensure template is applied  
                        DependencyObject? editorControlCandidate \= (contentPresenter.ContentTemplate \!= null && VisualTreeHelper.GetChildrenCount(contentPresenter) \> 0\)  
                                                                 ? VisualTreeHelper.GetChild(contentPresenter, 0\)  
                                                                 : null;  
                          
                        if (editorControlCandidate is Control editorControl && editorControl.Focusable)  
                        {  
                            editorControl.Focus();  
                            if (editorControl is TextBox tb)  
                            {  
                                tb.SelectAll();  
                            }  
                        }  
                        else // Fallback if the direct child isn't the editor (e.g., it's wrapped in another panel)  
                        {  
                            FocusFirstFocusableChild(contentPresenter);  
                        }  
                    }  
                }  
            }  
        }), System.Windows.Threading.DispatcherPriority.Background); // Background or Input priority  
    }

    private void OnFocusRowRequested(DataGridRowItemViewModel? vmToFocus)  
    {  
        if (vmToFocus \== null) return;  
        MainDataGrid.ScrollIntoView(vmToFocus);  
        Dispatcher.BeginInvoke(new Action(() \=\>  
        {  
            var row \= MainDataGrid.ItemContainerGenerator.ContainerFromItem(vmToFocus) as DataGridRow;  
            row?.Focus();  
        }), System.Windows.Threading.DispatcherPriority.Background);  
    }

    private void FocusFirstFocusableChild(DependencyObject parent)  
    {  
        // Helper to find the first focusable child in the visual tree if the direct child isn't it.  
        for (int i \= 0; i \< VisualTreeHelper.GetChildrenCount(parent); i++)  
        {  
            var child \= VisualTreeHelper.GetChild(parent, i);  
            if (child is Control control && control.Focusable && control.IsEnabled)  
            {  
                control.Focus();  
                if (control is TextBox tb) tb.SelectAll();  
                return;  
            }  
            FocusFirstFocusableChild(child); // Recurse  
        }  
    }

    // GetVisualChild\<T\> helper method (as defined in previous Tab navigation implementation)  
    // public static T? GetVisualChild\<T\>(DependencyObject parent) where T : Visual { /\* ... \*/ }  
}

**Focus Management Scenarios (based on Spec Section 3 \- UI Design, Clarification 3):**

1. **After In-Place Edit Confirmation (`Enter`/`Tab`/Focus Loss):**

   * **`Enter` / Focus Loss (that confirms):** `DataGridRowItemViewModel.CommitEdit()` is called. If successful, `IsInEditMode` becomes `false`.

**Action in `MainViewModel` (or `CommitEdit`):** Focus should move to the `DataGrid` row containing the edited node, in browse mode.  
 C\#  
// Inside DataGridRowItemViewModel.CommitEdit() or method called by ConfirmEditCommand  
// after successful commit and IsInEditMode \= false:  
ParentViewModel.RequestFocusOnRow(this);

*   
  * **`Tab`:** (As detailed in Tab navigation implementation) Confirms edit, jumps to the next editable cell/row, and starts editing if applicable.  
    * **Action:** The Tab handling logic in `MainWindow.xaml.cs` (`Editor_PreviewKeyDown`) will call `FocusEditorInCell(nextTargetVm)` or `RequestFocusOnRow(nextTargetVm)` if the next item is not editable.  
2. **After In-Place Edit Cancellation (`Esc`):**

   * `DataGridRowItemViewModel.CancelEdit()` is called. `IsInEditMode` becomes `false`.

**Action in `MainViewModel` (or `CancelEdit`):** Focus moves to the `DataGrid` row containing the (unmodified) node, in browse mode.  
 C\#  
// Inside DataGridRowItemViewModel.CancelEdit() or method called by CancelEditCommand:  
ParentViewModel.RequestFocusOnRow(this);

*   
3. **After Node Deletion:**

   * `MainViewModel.ExecuteDeleteNodes()` is called.  
     * **Action in `ExecuteDeleteNodes()`:**  
       1. Determine the index of the first deleted item (or the primary selected item if multiple).  
       2. After nodes are removed from `FlatItemsSource`:  
          * If items remain: Select the node that was immediately after the deleted node(s), or if the last node(s) were deleted, select the new last node.  
          * If the tree becomes empty: Focus can go to the `DataGrid` itself or a primary control like the filter box.

C\#  
// Inside MainViewModel.ExecuteDeleteNodes(), after removing items from DOM and before/during FlatItemsSource refresh:  
int selectionIndexAfterDelete \= /\* calculate based on original selection \*/;  
// ... after FlatItemsSource is updated ...  
if (FlatItemsSource.Any())  
{  
    SelectedRowItem \= FlatItemsSource\[Math.Min(selectionIndexAfterDelete, FlatItemsSource.Count \- 1)\];  
    RequestFocusOnRow(SelectedRowItem);  
}  
else  
{  
    // MainDataGrid.Focus(); // Or another control  
}

*   
4. **After Node Insertion (Insert Key, Context Menu "Insert Above/Below", Materializing Schema Node):**

   * A new `DataGridRowItemViewModel` (`newVm`) is created and added to `FlatItemsSource`.

**Action in `MainViewModel` methods like `ExecuteInsertNewItem`, `MaterializeDomNodeForSchema`, `AddNewItemToArrayFromPlaceholder`:**  
 C\#  
// After newVm is added and selected:  
SelectedRowItem \= newVm;  
RequestScrollToItem(newVm);  
if (newVm.IsEditable && (newVm.DomNode is ValueNode || newVm.DomNode is RefNode || newVm.IsSchemaOnlyNode /\* and is primitive-like \*/))  
{  
    newVm.IsInEditMode \= true; // This should trigger InitializeEditValue  
    RequestFocusOnEditor(newVm); // Request focus for the new editor  
}  
else  
{  
    RequestFocusOnRow(newVm); // Focus the row if not directly editable  
}

*   
5. **After Adding Array Item (via placeholder edit \- Spec Section 2.4.3):**

   * This is a specific case of node insertion.

**Action in `MainViewModel.AddNewItemToArrayFromPlaceholder` (called from `placeholderVm.CommitEdit()`):** The new item VM is created and `IsInEditMode` is set.  
 C\#  
// Inside AddNewItemToArrayFromPlaceholder, after new item VM \`newItemVm\` is ready:  
if (newItemVm \!= null && newItemVm.IsEditable)  
{  
    newItemVm.IsInEditMode \= true;  
    RequestScrollToItem(newItemVm);  
    RequestFocusOnEditor(newItemVm);  
}

*   
6. **After Expanding/Collapsing Node:**

   * `DataGridRowItemViewModel.IsExpanded` setter calls `MainViewModel.OnExpansionChanged()`, which rebuilds/updates `FlatItemsSource`.

**Action in `MainViewModel.OnExpansionChanged()`:** Keep focus on the same `DataGridRowItemViewModel` that was expanded/collapsed.  
 C\#  
// Inside MainViewModel.OnExpansionChanged(DataGridRowItemViewModel changedItem)  
// After FlatItemsSource is refreshed:  
var reFetchedVm \= FlatItemsSource.FirstOrDefault(vm \=\> vm.DomNode \== changedItem.DomNode || (vm.IsSchemaOnlyNode && vm.SchemaContextNode \== changedItem.SchemaContextNode && vm.NodeName \== changedItem.NodeName));  
SelectedRowItem \= reFetchedVm ?? (FlatItemsSource.Any() ? FlatItemsSource.First() : null); // Re-select or select first  
if (SelectedRowItem \!= null) RequestFocusOnRow(SelectedRowItem);

*   
7. **After Search (Find Next/Previous):**

   * `MainViewModel.ExecuteFind()` identifies `targetVm`.

**Action in `ExecuteFind()`:**  
 C\#  
// After targetVm is found and highlighted:  
SelectedRowItem \= targetVm;  
RequestScrollToItem(targetVm);  
RequestFocusOnRow(targetVm); // Focus the row in browse mode

*   
8. **After Applying/Clearing Filter:**

   * `MainViewModel.ApplyFiltersAndRefreshDisplayList()` is called.

**Action at the end of `ApplyFiltersAndRefreshDisplayList()`:**  
 C\#  
// After FlatItemsSource is rebuilt:  
if (FlatItemsSource.Any())  
{  
    SelectedRowItem \= FlatItemsSource.First(); // Select the first visible item  
    RequestFocusOnRow(SelectedRowItem);  
}  
else  
{  
    // Focus the filter input box or the DataGrid itself  
    // This requires a way for ViewModel to request focus on a specific named control in the View.  
    // For simplicity: MainDataGrid.Focus(); (if accessible)  
}

*   
9. **Modal Dialogs (Property Editor, New Sub-Node, `RefNode` path editor):**

   * **On Open:** The dialog itself should set focus to its first interactive element (standard dialog behavior).  
   * **On Close (OK/Confirm/Cancel):** The control that launched the modal (e.g., a button in a cell, a context menu item which was invoked on a row) should ideally regain focus, or the relevant `DataGridRowItemViewModel` row should be focused.

**Action in the command/event handler that showed the modal, after `dialog.ShowDialog()` returns:**  
 C\#  
// E.g., after ExecuteOpenRefNodePathEditorModal  
if (refNodeVm \!= null) // refNodeVm was the context for opening the dialog  
{  
    SelectedRowItem \= refNodeVm;  
    RequestFocusOnRow(refNodeVm); // Or RequestFocusOnEditor if an edit was made and it should re-enter edit mode  
}

* 

**Key Considerations for Implementation:**

* **Dispatcher:** Always use `Dispatcher.BeginInvoke` for focus-setting calls that occur after a data change or UI reconstruction, as the elements might not be immediately available in the visual tree.  
* **Finding Controls:** The `FocusEditorInCell` logic relies on traversing the visual tree (`GetVisualChild<T>`). This can be fragile if your `DataTemplate` structure changes significantly. Ensure your `DataTemplateSelector` produces a somewhat predictable structure within the cell.  
* **`Focusable="False"` on Cell ContentPresenter:** As shown in a previous XAML outline, setting `Focusable="False"` on the `ContentPresenter` within the `DataGridCell` template can sometimes help ensure that keyboard focus goes directly to the editor control inside when `IsInEditMode` is true, rather than the cell itself.  
* **Testing:** Focus management is notoriously tricky to get perfect. Test thoroughly with both mouse and keyboard across all scenarios.

This detailed approach should help you implement robust focus management according to the specification.

# FindOrMaterializeParentDomFor and CalculatePathToParentForSchemaOnlyNode

Okay, detailing `MainViewModel.FindOrMaterializeParentDomFor` and `CalculatePathToParentForSchemaOnlyNode` is crucial because these methods bridge the conceptual schema-only nodes in your UI with the actual `DomNode` tree. They are at the heart of the "materialization" process.

Let's break them down.

**Assumptions:**

* `DataGridRowItemViewModel` for a schema-only node has:  
  * `SchemaContextNode`: The `SchemaNode` defining it.  
  * `_nameOverrideForSchemaOnly`: The property name it represents if it's a child of an object.  
  * `_depthForSchemaOnly`: Its conceptual depth in the tree.  
  * `ParentViewModel`: Reference to `MainViewModel`.  
* `MainViewModel` has `FlatItemsSource` which is the ordered list of VMs currently in the `DataGrid`.  
* `DomFactory.CreateDefaultFromSchema` exists and works as specified.  
* `_domToSchemaMap` and `_undoRedoService` are available.

---

**1\. `CalculatePathToParentForSchemaOnlyNode(DataGridRowItemViewModel vm)`**

**Goal:** Given a `DataGridRowItemViewModel` (`vm`) representing a schema-only node, determine the sequence of *schema properties* that form the path from an existing `DomNode` ancestor (or the conceptual root if no DOM exists yet) up to (but not including) `vm` itself. This path describes the intermediate schema-only nodes that also need to be materialized.

C\#  
// \--- File: JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
// ...

public class MainViewModel : ViewModelBase  
{  
    // Represents a segment in the conceptual path from an existing DOM anchor  
    // to the node being materialized. Each segment is a schema-only property.  
    private class PathSegmentToMaterialize  
    {  
        /// \<summary\>  
        /// The name of the property this segment represents.  
        /// \</summary\>  
        public string Name { get; }  
        /// \<summary\>  
        /// The SchemaNode defining this property.  
        /// \</summary\>  
        public SchemaNode Schema { get; }  
        /// \<summary\>  
        /// The conceptual depth of this segment in the tree.  
        /// \</summary\>  
        public int Depth {get; }

        public PathSegmentToMaterialize(string name, SchemaNode schema, int depth)  
        {  
            Name \= name;  
            Schema \= schema;  
            Depth \= depth;  
        }  
    }

    /// \<summary\>  
    /// Calculates the hierarchical path of schema-only parent segments that need to be  
    /// materialized above the given schema-only ViewModel.  
    /// The path starts from the closest existing DomNode ancestor or the root.  
    /// \</summary\>  
    /// \<param name="targetSchemaOnlyVm"\>The schema-only ViewModel for which the parent path is needed.\</param\>  
    /// \<param name="anchorDomNodeParent"\>Output: The closest existing DomNode that will serve as the anchor parent for the first segment.\</param\>  
    /// \<param name="anchorParentSchema"\>Output: The SchemaNode for the anchorDomNodeParent.\</param\>  
    /// \<returns\>A list of PathSegmentToMaterialize, ordered from the highest (closest to root/anchor)  
    /// down to the immediate parent of the node represented by targetSchemaOnlyVm.  
    /// Returns null if the path cannot be determined.\</returns\>  
    private List\<PathSegmentToMaterialize\>? CalculatePathToMaterializeAbove(  
        DataGridRowItemViewModel targetSchemaOnlyVm,  
        out DomNode? anchorDomNodeParent,  
        out SchemaNode? anchorParentSchema)  
    {  
        anchorDomNodeParent \= null;  
        anchorParentSchema \= null;

        if (targetSchemaOnlyVm.DomNode \!= null || \!targetSchemaOnlyVm.IsSchemaOnlyNode)  
        {  
            // This method is only for schema-only nodes that need their path materialized.  
            // If it has a DomNode, its parent is already real.  
            anchorDomNodeParent \= targetSchemaOnlyVm.DomNode?.Parent;  
            if (anchorDomNodeParent \!= null)  
                \_domToSchemaMap.TryGetValue(anchorDomNodeParent, out anchorParentSchema);  
            return new List\<PathSegmentToMaterialize\>(); // Empty path, parent is real  
        }

        var pathSegments \= new LinkedList\<PathSegmentToMaterialize\>();  
        DataGridRowItemViewModel? currentVm \= targetSchemaOnlyVm;

        int targetVmIndexInFlatList \= FlatItemsSource.IndexOf(currentVm);  
        if (targetVmIndexInFlatList \< 0\) return null; // Should not happen if vm is from the list

        // Traverse upwards in the FlatItemsSource to find the hierarchy of schema-only parents  
        // until we hit a VM that represents an actual DomNode or the top of the list.  
        DataGridRowItemViewModel? hierarchicalParentVm \= null;  
        for (int i \= targetVmIndexInFlatList \- 1; i \>= 0; i--)  
        {  
            var precedingVm \= FlatItemsSource\[i\];  
            // A hierarchical parent will have a shallower depth.  
            // The immediate parent has depth one less than the current VM.  
            if (precedingVm.Indentation.Left \< currentVm.Indentation.Left &&  
                precedingVm.Indentation.Left \== currentVm.Indentation.Left \- 15\) // Assuming 15 is indent unit  
            {  
                hierarchicalParentVm \= precedingVm;  
                break;  
            }  
        }

        while (currentVm \!= null && currentVm.IsSchemaOnlyNode)  
        {  
            // The currentVm's SchemaContextNode IS the schema for the node it represents.  
            // Its Name (from \_nameOverrideForSchemaOnly) is the property name.  
            pathSegments.AddFirst(new PathSegmentToMaterialize(currentVm.NodeName, currentVm.SchemaContextNode, currentVm.Indentation.Left / 15));

            // Find the hierarchical parent of currentVm in the FlatItemsSource  
            // This logic needs to correctly identify the logical parent in the flat list.  
            int currentIndex \= FlatItemsSource.IndexOf(currentVm);  
            if (currentIndex \<= 0 && currentVm.IsSchemaOnlyNode) // Reached top of list, still schema-only  
            {  
                 // This means the root itself (or a top-level mounted schema) is the starting point.  
                anchorDomNodeParent \= \_rootDomNode; // Could be null if document is completely empty  
                if (anchorDomNodeParent \!= null)  
                {  
                    \_domToSchemaMap.TryGetValue(anchorDomNodeParent, out anchorParentSchema);  
                }  
                else // No DOM root, so the anchor is conceptual root with its schema  
                {  
                     // Try to get schema for absolute root ""  
                    \_schemaLoader.RootSchemas.TryGetValue("", out anchorParentSchema);  
                }  
                currentVm \= null; // Stop iteration  
                break;  
            }  
              
            hierarchicalParentVm \= null;  
            for (int i \= currentIndex \- 1; i \>= 0; i--)  
            {  
                var precedingVm \= FlatItemsSource\[i\];  
                if (precedingVm.Indentation.Left \< currentVm.Indentation.Left &&  
                    precedingVm.Indentation.Left \== currentVm.Indentation.Left \- (15/\*indent unit\*/)) // Immediate parent  
                {  
                    hierarchicalParentVm \= precedingVm;  
                    break;  
                }  
            }

            currentVm \= hierarchicalParentVm;

            if (currentVm \!= null && currentVm.DomNode \!= null) // Found an anchor DomNode  
            {  
                anchorDomNodeParent \= currentVm.DomNode;  
                anchorParentSchema \= currentVm.AssociatedSchemaNode; // Or \_domToSchemaMap\[currentVm.DomNode\]  
                break; // Stop, the rest of the path to this anchor is real  
            }  
        }  
          
        // If we exited the loop and currentVm is null, it means the entire path up to the conceptual root  
        // was schema-only. anchorDomNodeParent will be \_rootDomNode (which could be null initially)  
        // and anchorParentSchema would be the schema for the root.

        if (anchorDomNodeParent \== null && \_rootDomNode \== null && anchorParentSchema \== null)  
        {  
            // If there's no DOM at all and no root schema, we can't really anchor.  
            // However, MaterializeDomNodeForSchema will try to create a root if needed.  
            // For now, let's assume \_rootDomNode will be created if it's null by the caller.  
        }

        return new List\<PathSegmentToMaterialize\>(pathSegments);  
    }  
}

**2\. `MainViewModel.FindOrMaterializeParentDomFor(DataGridRowItemViewModel childVmToMaterialize)` (Conceptual Refinement of `MaterializeDomNodeForSchema`)**

This method uses `CalculatePathToMaterializeAbove` and then creates the DOM nodes. The original `MaterializeDomNodeForSchema(vmToMaterialize)` will call this.

C\#  
// \--- Still within JsonConfigEditor.Wpf/ViewModels/MainViewModel.cs \---  
public partial class MainViewModel : ViewModelBase  
{  
    /// \<summary\>  
    /// Ensures the entire parent path for the given schema-only ViewModel exists in the DOM tree,  
    /// creating any missing DomNodes along the way using their default schema values.  
    /// Returns the immediate parent DomNode under which childVmToMaterialize's node should be created.  
    /// \</summary\>  
    /// \<param name="childVmToMaterialize"\>The schema-only ViewModel whose parent path needs to be ensured.\</param\>  
    /// \<param name="createdNodesForUndo"\>Output list to track all DomNodes materialized in this process for a composite undo.\</param\>  
    /// \<returns\>The materialized parent DomNode, or null if materialization fails.\</returns\>  
    private DomNode? FindOrMaterializeParentDomFor(  
        DataGridRowItemViewModel childVmToMaterialize,  
        List\<DomNode\> createdNodesForUndo)  
    {  
        if (childVmToMaterialize.DomNode \!= null) return childVmToMaterialize.DomNode.Parent; // Parent is already real

        List\<PathSegmentToMaterialize\>? pathSegmentsToCreate \=  
            CalculatePathToMaterializeAbove(childVmToMaterialize, out DomNode? currentDomAnchor, out SchemaNode? currentSchemaForAnchor);

        if (pathSegmentsToCreate \== null)  
        {  
            UpdateStatusBar("Error: Could not determine path for materializing node.", true);  
            return null;  
        }

        // Ensure root DomNode exists if we are materializing from the very top  
        if (currentDomAnchor \== null && \_rootDomNode \== null)  
        {  
            SchemaNode? rootSchema \= currentSchemaForAnchor ?? \_schemaLoader.RootSchemas.GetValueOrDefault("");  
            if (rootSchema \== null) // Absolute last resort: a generic object root  
            {  
                // This fallback should be rare if schemas are well-defined or a default root schema exists  
                rootSchema \= new SchemaNode("$root\_fallback", typeof(object), false, false, null, null, null, null, null, false,  
                                            new Dictionary\<string, SchemaNode\>(), null, true, null, "");  
                \_errorMessages.Add("Warning: No root schema found, creating generic root object for materialization.");  
            }  
            \_rootDomNode \= DomFactory.CreateDefaultFromSchema(rootSchema.Name ?? "$root", rootSchema, null);  
            \_domToSchemaMap\[\_rootDomNode\] \= rootSchema;  
            createdNodesForUndo.Add(\_rootDomNode); // Track root creation  
            currentDomAnchor \= \_rootDomNode;  
            // currentSchemaForAnchor remains the schema for this newly created root.  
        }  
        else if (currentDomAnchor \== null && \_rootDomNode \!= null) {  
            // This implies the path is directly off the existing root  
            currentDomAnchor \= \_rootDomNode;  
            \_domToSchemaMap.TryGetValue(currentDomAnchor, out currentSchemaForAnchor);  
        }

        DomNode currentMaterializedParent \= currentDomAnchor\!; // Asserting not null after above logic  
        SchemaNode? currentMaterializedParentSchema \= currentSchemaForAnchor;

        foreach (var segment in pathSegmentsToCreate)  
        {  
            if (\!(currentMaterializedParent is ObjectNode currentObjectParent))  
            {  
                UpdateStatusBar($"Error: Expected object parent for '{segment.Name}' but found {currentMaterializedParent?.GetType().Name}.", true);  
                return null;  
            }

            // The segment.Schema is the schema for the node to be created (segment.Name)  
            // The currentMaterializedParentSchema is the schema for currentObjectParent.  
            // We need to ensure segment.Schema is indeed a property of currentMaterializedParentSchema  
            // This check is implicitly handled if CalculatePathToMaterializeAbove correctly uses schema hierarchy.

            if (\!currentObjectParent.Children.TryGetValue(segment.Name, out DomNode? nextMaterializedNode))  
            {  
                // This segment needs to be materialized  
                nextMaterializedNode \= DomFactory.CreateDefaultFromSchema(segment.Name, segment.Schema, currentObjectParent);  
                currentObjectParent.Children\[segment.Name\] \= nextMaterializedNode;  
                \_domToSchemaMap\[nextMaterializedNode\] \= segment.Schema; // Map the new node  
                createdNodesForUndo.Add(nextMaterializedNode);  
            }  
            currentMaterializedParent \= nextMaterializedNode;  
            currentMaterializedParentSchema \= segment.Schema; // The schema for the node just processed becomes context for next segment  
        }

        return currentMaterializedParent; // This is the direct parent where childVmToMaterialize's node will be added  
    }

    /// \<summary\>  
    /// Top-level method called from DataGridRowItemViewModel.CommitEdit() when a schema-only node is being edited.  
    /// Orchestrates finding/materializing the parent path, creating the target node, and recording undo.  
    /// \</summary\>  
    public DomNode? MaterializeDomNodeForSchema(DataGridRowItemViewModel vmToMaterialize)  
    {  
        if (vmToMaterialize.DomNode \!= null) return vmToMaterialize.DomNode; // Already materialized

        List\<DomNode\> allCreatedNodesThisOperation \= new List\<DomNode\>();  
        DomNode? directParentDomNode \= FindOrMaterializeParentDomFor(vmToMaterialize, allCreatedNodesThisOperation);

        if (directParentDomNode \== null)  
        {  
            UpdateStatusBar($"Failed to materialize parent path for '{vmToMaterialize.NodeName}'.", true);  
            return null;  
        }

        // Now create the actual target node itself  
        string targetNodeName \= vmToMaterialize.NodeName; // This should be the correct property name  
        SchemaNode targetSchema \= vmToMaterialize.SchemaContextNode;

        if (directParentDomNode is ObjectNode parentObject)  
        {  
            if (parentObject.Children.ContainsKey(targetNodeName))  
            {  
                // Should not happen if vmToMaterialize was truly schema-only for this parent  
                UpdateStatusBar($"Conflict: Node '{targetNodeName}' already exists in parent '{parentObject.Name}'.", true);  
                return parentObject.Children\[targetNodeName\]; // Return existing  
            }  
            var newTargetDomNode \= DomFactory.CreateDefaultFromSchema(targetNodeName, targetSchema, parentObject);  
            parentObject.Children\[targetNodeName\] \= newTargetDomNode;  
            \_domToSchemaMap\[newTargetDomNode\] \= targetSchema;  
            allCreatedNodesThisOperation.Add(newTargetDomNode);

            // Record a composite undo operation for all created nodes  
            if (allCreatedNodesThisOperation.Any())  
            {  
                // Create a composite operation that knows how to undo the creation of all these nodes.  
                // For simplicity, the AddNodeOperation for the \*target\* node can be recorded,  
                // and its undo would remove it. If parent creations need separate undo, it's more complex.  
                // The spec implies "Any change... triggers an undoable operation."  
                // Materializing can be one such operation.  
                // For now, the last AddNodeOperation in the chain handles the immediate target.  
                // A truly robust undo for multi-level materialization needs a composite operation.  
                // For this skeleton, we'll assume AddNodeOperation is recorded for the final node.  
                \_undoRedoService.RecordOperation(new AddNodeOperation(parentObject, newTargetDomNode, \-1, this));  
            }

            MarkAsDirty();  
            UpdateStatusBar($"Node '{newTargetDomNode.Name}' materialized from schema.", false);  
            return newTargetDomNode;  
        }  
        else  
        {  
            UpdateStatusBar($"Error: Final parent for '{targetNodeName}' is not an object node. Cannot add property.", true);  
            return null;  
        }  
    }  
}

**Explanation and Refinements:**

1. **`CalculatePathToMaterializeAbove`:**

   * This method is responsible for introspection of the `FlatItemsSource` to understand the *conceptual* hierarchy of schema-only nodes leading up to `targetSchemaOnlyVm`.  
   * It navigates "upwards" in the flat list by looking for items with a shallower indentation that are the immediate hierarchical parent.  
   * It collects `PathSegmentToMaterialize` objects, each containing the `Name` (property name) and `SchemaNode` for an intermediate schema-only parent that needs creation.  
   * `anchorDomNodeParent` and `anchorParentSchema` are crucial `out` parameters: they identify the *first real `DomNode`* (or the conceptual root if the whole tree is being materialized from schema) under which the chain of schema-only nodes begins.  
2. **`FindOrMaterializeParentDomFor`:**

   * Takes the list of `PathSegmentToMaterialize` from the previous step.  
   * Starts from `anchorDomNodeParent` (or creates `_rootDomNode` if it's null and the path starts from the absolute root).  
   * Iteratively walks down the `pathSegmentsToCreate`. For each segment:  
     * If the corresponding child `DomNode` (with `segment.Name`) doesn't exist under the current `currentMaterializedParent`, it creates it using `DomFactory.CreateDefaultFromSchema(segment.Name, segment.Schema, currentMaterializedParent)`.  
     * The newly created (or found existing) node becomes the `currentMaterializedParent` for the next segment.  
     * All newly created nodes are added to `createdNodesForUndo`.  
   * Returns the final `currentMaterializedParent`, which is the direct DOM parent for the node `childVmToMaterialize` represents.  
3. **`MainViewModel.MaterializeDomNodeForSchema` (Top-Level Orchestrator):**

   * Calls `FindOrMaterializeParentDomFor` to ensure the parent path exists.  
   * Then creates the *actual target `DomNode`* specified by `vmToMaterialize.SchemaContextNode` and `vmToMaterialize.NodeName` under the now-materialized `directParentDomNode`.  
   * Records the necessary undo operations (ideally a composite one for all nodes created in `allCreatedNodesThisOperation`).  
   * Updates `_domToSchemaMap`.

**Key Challenges Addressed by This Detail:**

* **Path Determination for Schema-Only Nodes:** `CalculatePathToMaterializeAbove` directly addresses how to find the path of *what needs to be created*.  
* **Recursive Materialization:** `FindOrMaterializeParentDomFor` handles creating a chain of parent nodes if multiple levels are schema-only.  
* **Undo for Materialization:** The `createdNodesForUndo` list allows for a composite undo operation to remove all nodes that were materialized as part of a single user action.  
* **Root Handling:** Logic to create `_rootDomNode` if it doesn't exist and materialization starts from the very top based on a root schema.

This detailed approach provides a more concrete plan for the complex materialization process. The helper methods for path calculation and recursive DOM creation are non-trivial and will require careful implementation and testing.


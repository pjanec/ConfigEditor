using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.Core.Schema;
using System;
using System.Windows;
using System.Text.Json;      // For JsonElement
using JsonConfigEditor.Contracts.Editors;

namespace JsonConfigEditor.ViewModels
{
    /// <summary>
    /// ViewModel for a single row in the DataGrid. It wraps either an actual DomNode
    /// or represents a schema-only placeholder for an addable node.
    /// (From specification document, Section 2.3, 2.12, and various editing sections)
    /// </summary>
    public class DataGridRowItemViewModel : ViewModelBase
    {
        // --- Private Fields ---
        private DomNode? _domNode; // Null if this is a schema-only placeholder
        private readonly SchemaNode? _schemaContextNode; // Schema context for this node
        private readonly MainViewModel _parentViewModel; // To interact with main logic

        private bool _isExpandedInternal; // Actual expansion state
        private string _editValue = string.Empty;
        private bool _isInEditMode;
        private bool _isValid = true; // Reflects validation status against schema
        private string _validationErrorMessage = string.Empty;
        private bool _isHighlightedInSearch;

        // Fields specific to placeholders
        private readonly bool _isAddItemPlaceholder; // True if this is an "Add item" placeholder for an array
        private readonly ArrayNode? _parentArrayNodeForPlaceholder; // Parent array if _isAddItemPlaceholder
        private readonly SchemaNode? _itemSchemaForPlaceholder; // Item schema if _isAddItemPlaceholder
        private readonly string? _nameOverrideForSchemaOnly; // Name for schema-only property placeholders
        private readonly int _depthForSchemaOnly; // Depth for schema-only/placeholder nodes

        // Added private fields for new properties
        private IValueEditor? _modalEditorInstance;

        // --- Constructor for actual DomNodes ---
        public DataGridRowItemViewModel(DomNode domNode, SchemaNode? schemaContextNode, MainViewModel parentViewModel)
        {
            _domNode = domNode ?? throw new ArgumentNullException(nameof(domNode));
            _schemaContextNode = schemaContextNode;
            _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
            _isAddItemPlaceholder = false;
            _isExpandedInternal = (_domNode is ObjectNode || _domNode is ArrayNode) && (_domNode.Depth < 2); // Default expand first few levels
            // Initialize IsValid based on current validation status if available from MainViewModel
            // Initialize _editValue based on DomNode
            if (_domNode is ValueNode vn) { _editValue = vn.Value.ToString(); }
            else if (_domNode is RefNode rn) { _editValue = rn.ReferencePath ?? string.Empty; }
        }

        // --- Constructor for Schema-Only Property Placeholders ---
        public DataGridRowItemViewModel(SchemaNode schemaPropertyNode, string propertyName, MainViewModel parentViewModel, int depth)
        {
            _domNode = null; // Marks as schema-only
            _schemaContextNode = schemaPropertyNode ?? throw new ArgumentNullException(nameof(schemaPropertyNode));
            _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
            _nameOverrideForSchemaOnly = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            _depthForSchemaOnly = depth;
            _isAddItemPlaceholder = false;
            _isExpandedInternal = (schemaPropertyNode.NodeType == SchemaNodeType.Object || schemaPropertyNode.NodeType == SchemaNodeType.Array) && (depth < 2);
            // Initialize _editValue based on DefaultValue for schema-only node
            _editValue = _schemaContextNode.DefaultValue?.ToString() ?? string.Empty;
        }

        // --- Constructor for "Add Item" Array Placeholders ---
        public DataGridRowItemViewModel(ArrayNode parentArrayNode, SchemaNode? itemSchema, MainViewModel parentViewModel, int depth)
        {
            _domNode = null; // Marks as placeholder
            _schemaContextNode = itemSchema;
            _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
            _parentArrayNodeForPlaceholder = parentArrayNode ?? throw new ArgumentNullException(nameof(parentArrayNode));
            _itemSchemaForPlaceholder = itemSchema;
            _nameOverrideForSchemaOnly = "(Add new item)"; // Display text for placeholder
            _depthForSchemaOnly = depth;
            _isAddItemPlaceholder = true;
            _isExpandedInternal = false; // Placeholders are not expandable
            _editValue = string.Empty; // No edit value for add item placeholder initially
        }

        // --- Public Properties for DataBinding and Logic ---

        /// <summary>
        /// Gets the underlying DomNode this ViewModel wraps.
        /// Null if this represents a schema-only placeholder or an "Add Item" placeholder.
        /// </summary>
        public DomNode? DomNode => _domNode;

        /// <summary>
        /// Gets the SchemaNode providing context for this item.
        /// For DOM-present nodes, it's their mapped schema. For schema-only/placeholders, it's their definition.
        /// </summary>
        public SchemaNode? SchemaContextNode => _schemaContextNode;

        /// <summary>
        /// Reference to the MainViewModel for callbacks and accessing shared services.
        /// </summary>
        public MainViewModel ParentViewModel => _parentViewModel;

        /// <summary>
        /// True if this ViewModel wraps an actual DomNode present in the JSON document.
        /// False for schema-only property placeholders and "Add Item" array placeholders.
        /// </summary>
        public bool IsDomNodePresent => _domNode != null;

        /// <summary>
        /// True if this ViewModel represents a property defined in the schema but not yet in the DOM.
        /// Excludes the "Add Item" array placeholder.
        /// (From specification document, Section 2.12)
        /// </summary>
        public bool IsSchemaOnlyNode => _domNode == null && !_isAddItemPlaceholder;

        /// <summary>
        /// True if this is the special "Add Item" placeholder for an array.
        /// (From specification document, Section 2.4.3)
        /// </summary>
        public bool IsAddItemPlaceholder => _isAddItemPlaceholder;

        /// <summary>
        /// Gets the display name of the node. Handles placeholders, root, and array indices.
        /// (From specification document, Section 2.3.1)
        /// </summary>
        public string NodeName
        {
            get
            {
                if (_isAddItemPlaceholder) return _nameOverrideForSchemaOnly!;
                if (IsSchemaOnlyNode) return _nameOverrideForSchemaOnly!;
                if (_domNode == null) return "(Error: DomNode is null)"; // Should not happen if not placeholder

                string name = _domNode.Name;

                // Determine if parent context is an array
                bool isParentArray = (_domNode.Parent is ArrayNode) ||
                                     (_parentArrayNodeForPlaceholder != null && _isAddItemPlaceholder); 
                                     // TODO: (IsSchemaOnlyNode && ParentViewModel.GetParentSchemaForSchemaOnlyNode(this)?.NodeType == SchemaNodeType.Array); // Conceptual parent schema lookup

                if (isParentArray && int.TryParse(name, out _)) // If name is numeric index
                {
                    return $"[{name}]";
                }
                return name == "$root" ? "(Root)" : name;
            }
        }

        /// <summary>
        /// Gets a string representation of the node's value for display.
        /// For schema-only nodes, shows the default value. For placeholders, shows placeholder text.
        /// (From specification document, Section 2.3.2, 2.12)
        /// </summary>
        public string ValueDisplay
        {
            get
            {
                if (_isAddItemPlaceholder) return ""; // Placeholder text handled by NodeName or specific template
                if (IsSchemaOnlyNode)
                {
                    // Display default value or type hint for schema-only nodes
                    if (_schemaContextNode.DefaultValue != null)
                        return _schemaContextNode.DefaultValue.ToString() ?? "(null default)";
                    return $"(Add {_schemaContextNode.ClrType.Name})";
                }
                if (_domNode is ValueNode vn)
                {
                    if (vn.Value.ValueKind == JsonValueKind.Null) return "(null)";
                    return vn.Value.ToString();
                }
                if (_domNode is RefNode rn)
                {
                    return rn.ReferencePath ?? "(empty ref)";
                }
                if (_domNode is ObjectNode on)
                {
                    return "[Object]"; // Or count of items: on.Children.Count
                }
                if (_domNode is ArrayNode an)
                {
                    return $"[{an.Items.Count} items]";
                }
                return "(unknown)";
            }
        }

        /// <summary>
        /// Gets the indentation margin based on the node's depth.
        /// (From specification document, Section 2.3.1)
        /// </summary>
        public Thickness Indentation
        {
            get
            {
                var depth = _domNode?.Depth ?? _depthForSchemaOnly;
                return new Thickness(depth * 20, 0, 0, 0); // 20 pixels per level
            }
        }

        /// <summary>
        /// Gets a value indicating whether this node can be expanded (ObjectNode, ArrayNode, or schema-only object/array).
        /// </summary>
        public bool IsExpandable => !_isAddItemPlaceholder &&
                                    ((_domNode is ObjectNode || _domNode is ArrayNode) ||
                                     (IsSchemaOnlyNode && (_schemaContextNode?.NodeType == SchemaNodeType.Object || _schemaContextNode?.NodeType == SchemaNodeType.Array)));

        /// <summary>
        /// Gets or sets whether this node is currently expanded in the UI.
        /// Setter notifies MainViewModel to refresh the flat list.
        /// (From specification document, Section 2.3.3)
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpandedInternal;
            set
            {
                if (_isExpandedInternal != value && IsExpandable)
                {
                    bool oldValue = _isExpandedInternal;
                    _isExpandedInternal = value;
                    System.Diagnostics.Debug.WriteLine($"VM '{NodeName}' ({GetHashCode()}): IsExpanded changed from {oldValue} to {_isExpandedInternal}. Firing OnPropertyChanged.");
                    OnPropertyChanged(); 
                    System.Diagnostics.Debug.WriteLine($"VM '{NodeName}' ({GetHashCode()}): Calling ParentViewModel.OnExpansionChanged.");
                    ParentViewModel.OnExpansionChanged(this); 
                }
            }
        }

        /// <summary>
        /// Allows MainViewModel to set expansion state internally (e.g., due to filtering) without triggering the full OnExpansionChanged logic.
        /// </summary>
        internal void SetExpansionStateInternal(bool expanded)
        {
            if (_isExpandedInternal != expanded && IsExpandable)
            {
                _isExpandedInternal = expanded;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        /// <summary>
        /// Gets a value indicating whether the node's value can be edited.
        /// Considers IsReadOnly from schema, node type, and if it's a placeholder.
        /// (From specification document, Section 2.4, 2.10)
        /// </summary>
        public bool IsEditable
        {
            get
            {
                // Placeholders can be "edited" to trigger materialization
                if (_isAddItemPlaceholder || IsSchemaOnlyNode)
                    return true;

                // Check if DOM node is present and not read-only
                if (IsDomNodePresent)
                {
                    // Check schema read-only flag
                    if (_schemaContextNode?.IsReadOnly == true)
                        return false;

                    // Only value nodes and ref nodes are directly editable
                    return _domNode is ValueNode || _domNode is RefNode;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets or sets the string value currently being edited in an editor control.
        /// (From specification document, Section 2.4)
        /// </summary>
        public string EditValue
        {
            get => _editValue;
            set => SetProperty(ref _editValue, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this row is in edit mode.
        /// (From specification document, Section 2.4)
        /// </summary>
        public bool IsInEditMode
        {
            get => _isInEditMode;
            set
            {
                if (IsEditable || (IsAddItemPlaceholder && value)) // Allow entering edit mode for add item placeholder
                {
                    if (SetProperty(ref _isInEditMode, value) && _isInEditMode)
                    {
                        // Initialize EditValue when entering edit mode
                        InitializeEditValue();
                        ParentViewModel.SetCurrentlyEditedItem(this);
                    }
                    else if (!_isInEditMode)
                    {
                        ParentViewModel.SetCurrentlyEditedItem(null);
                    }
                }
                else if (_isInEditMode && !value) // Allow exiting edit mode even if not editable
                {
                    SetProperty(ref _isInEditMode, false);
                    ParentViewModel.SetCurrentlyEditedItem(null);
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current node's data is valid.
        /// (From specification document, Section 2.4.1)
        /// </summary>
        public bool IsValid
        {
            get => _isValid;
            private set => SetProperty(ref _isValid, value);
        }

        /// <summary>
        /// Gets the validation error message if IsValid is false.
        /// (From specification document, Section 2.4.1)
        /// </summary>
        public string ValidationErrorMessage
        {
            get => _validationErrorMessage;
            private set => SetProperty(ref _validationErrorMessage, value);
        }

        /// <summary>
        /// Gets or sets a value indicating if this item should be highlighted as a search result.
        /// (From specification document, Section 2.6)
        /// </summary>
        public bool IsHighlightedInSearch
        {
            get => _isHighlightedInSearch;
            set => SetProperty(ref _isHighlightedInSearch, value);
        }

        // --- Schema Derived Read-Only Properties for UI ---
        /// <summary>True if the schema marks this node as read-only and it's a DOM-present node.</summary>
        public bool IsNodeReadOnly => IsDomNodePresent && (_schemaContextNode?.IsReadOnly ?? false);

        /// <summary>True if this is a DOM-present node but has no associated schema.</summary>
        public bool IsUnschematized => IsDomNodePresent && _schemaContextNode == null && !(_domNode is RefNode); // RefNodes have special handling

        /// <summary>True if this is a RefNode pointing to an external or unresolvable path.</summary>
        public bool IsRefLinkToExternalOrMissing
        {
            get
            {
                if (_domNode is RefNode refNode)
                {
                    return refNode.IsExternalReference() || !ParentViewModel.IsRefPathResolvable(refNode.ReferencePath);
                }
                return false;
            }
        }

        // --- Public Methods ---

        /// <summary>
        /// Initializes the edit value when entering edit mode.
        /// </summary>
        private void InitializeEditValue()
        {
            if (_isAddItemPlaceholder)
            {
                EditValue = "";
            }
            else if (IsSchemaOnlyNode)
            {
                EditValue = _schemaContextNode?.DefaultValue?.ToString() ?? "";
            }
            else if (_domNode is ValueNode valueNode)
            {
                EditValue = valueNode.GetDisplayValue();
            }
            else if (_domNode is RefNode refNode)
            {
                EditValue = refNode.ReferencePath;
            }
            else
            {
                EditValue = "";
            }
        }

        /// <summary>
        /// Attempts to commit the edited value (_editValue) back to the DomNode.
        /// If this VM represents a schema-only node, it first triggers its materialization.
        /// Performs validation and updates IsValid, ValidationErrorMessage.
        /// (From specification document, Section 2.4, 2.12)
        /// </summary>
        public bool CommitEdit()
        {
            try
            {
                if (_isAddItemPlaceholder)
                {
                    // TODO: Implement Undo/Redo for AddArrayItem
                    return ParentViewModel.AddArrayItem(_parentArrayNodeForPlaceholder!, EditValue, _itemSchemaForPlaceholder);
                }
                else if (IsSchemaOnlyNode)
                {
                    // TODO: Implement Undo/Redo for MaterializeSchemaOnlyNode
                    return ParentViewModel.MaterializeSchemaOnlyNode(this, EditValue);
                }
                else if (_domNode is ValueNode valueNode)
                {
                    JsonElement oldValue = valueNode.Value; // Clone if necessary, ValueNode.Value should return a clone or be safe
                    
                    if (valueNode.TryUpdateFromString(EditValue))
                    {
                        JsonElement newValue = valueNode.Value; // Get the new value
                        ParentViewModel.RecordValueEdit(valueNode, oldValue, newValue); // For Undo/Redo
                        // OnNodeValueChanged is now implicitly handled by RecordValueEdit's effect on IsDirty & validation
                        SetValidationState(true, "");
                        return true;
                    }
                    else
                    {
                        SetValidationState(false, "Invalid value format");
                        return false;
                    }
                }
                else if (_domNode is RefNode refNode)
                {
                    string oldPath = refNode.ReferencePath;
                    if (oldPath != EditValue) // Check if value actually changed
                    {
                        refNode.ReferencePath = EditValue;
                        // TODO: Implement Undo/Redo for RefNode path changes
                        // ParentViewModel.RecordRefPathEdit(refNode, oldPath, EditValue); 
                        ParentViewModel.OnNodeValueChanged(this); // General notification
                    }
                    SetValidationState(true, ""); // Assume ref path string is always valid for now
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                SetValidationState(false, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Cancels the current edit operation and reverts EditValue if necessary.
        /// (From specification document, Section 2.4)
        /// </summary>
        public void CancelEdit()
        {
            IsInEditMode = false;
            InitializeEditValue(); // Reset to original value
        }

        /// <summary>
        /// Sets the validation state and message for this item.
        /// Called by MainViewModel after full document validation or by CommitEdit.
        /// </summary>
        public void SetValidationState(bool isValid, string message)
        {
            IsValid = isValid;
            ValidationErrorMessage = message;
        }

        /// <summary>
        /// Updates the ViewModel with its associated SchemaNode (e.g., after a global remap).
        /// </summary>
        public void UpdateSchemaInfo()
        {
            OnPropertyChanged(nameof(IsNodeReadOnly));
            OnPropertyChanged(nameof(NodeName));
            OnPropertyChanged(nameof(ValueDisplay));
            OnPropertyChanged(nameof(IsUnschematized));
            OnPropertyChanged(nameof(IsRefLinkToExternalOrMissing));
        }

        /// <summary>
        /// Called by MainViewModel after an Undo/Redo operation modifies the underlying DomNode.
        /// Refreshes display properties that depend directly on DomNode's state.
        /// </summary>
        public void RefreshDisplayProperties()
        {
            OnPropertyChanged(nameof(NodeName));
            OnPropertyChanged(nameof(ValueDisplay));
            OnPropertyChanged(nameof(IsDomNodePresent));
            OnPropertyChanged(nameof(IsEditable));
        }

        // Added public properties
        public IValueEditor? ModalEditorInstance
        {
            get => _modalEditorInstance;
            set
            {
                _modalEditorInstance = value;
                OnPropertyChanged();
            }
        }
    }
} 
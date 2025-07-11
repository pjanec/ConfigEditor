using RuntimeConfig.Core.Dom;
using RuntimeConfig.Core.Schema;
using JsonConfigEditor.Core.History;
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
        private readonly string _pathKeyForSchemaOnlyNode = string.Empty; // Unique key for schema-only nodes

        // Added private fields for new properties
        private IValueEditor? _modalEditorInstance;
        private string _originalValueBeforeEdit = string.Empty;

        // --- Constructor for actual DomNodes ---
        public DataGridRowItemViewModel(DomNode domNode, SchemaNode? schemaContextNode, MainViewModel parentViewModel, int originLayerIndex)
        {
            _domNode = domNode ?? throw new ArgumentNullException(nameof(domNode));
            _schemaContextNode = schemaContextNode;
            _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
            OriginLayerIndex = originLayerIndex; // Store the origin index
            _isAddItemPlaceholder = false;

            // Restore the persisted expansion state for this node if it exists,
            // otherwise, use the default logic (expand the first few levels).
            _isExpandedInternal = parentViewModel.GetSchemaNodeExpansionState(domNode.Path) ??
                                  ((_domNode is ObjectNode || _domNode is ArrayNode) && (_domNode.Depth < 2));

            if (_schemaContextNode != null)
            {
                // ModalEditorInstance = _parentViewModel.UiRegistry.ResolveEditor(_schemaContextNode); // You will need to implement ResolveEditor and uncomment this
            }

            if (_domNode is ValueNode vn) { _editValue = vn.Value.ToString(); }
            else if (_domNode is RefNode rn) { _editValue = rn.ReferencePath ?? string.Empty; }
            System.Diagnostics.Debug.WriteLine($"DGRIVM Constructor (DOM): NodeName: {NodeName}, Path: {_domNode?.Path}, Schema: {_schemaContextNode?.Name}, IsEditable: {IsEditable}, IsSchemaOnly: {IsSchemaOnlyNode}, ModalEditor: {ModalEditorInstance?.GetType().Name}");
        }

        // --- Constructor for Schema-Only Property Placeholders ---
        public DataGridRowItemViewModel(SchemaNode schemaPropertyNode, string propertyName, MainViewModel parentViewModel, int depth, string pathKey)
        {
            _domNode = null; // Marks as schema-only
            _schemaContextNode = schemaPropertyNode ?? throw new ArgumentNullException(nameof(schemaPropertyNode));
            _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
            _nameOverrideForSchemaOnly = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            _depthForSchemaOnly = depth;
            _pathKeyForSchemaOnlyNode = pathKey;
            _isAddItemPlaceholder = false;
            OriginLayerIndex = -1; // Schema-only nodes don't have an origin layer
            // Initialize IsExpanded based on persisted state or default
            _isExpandedInternal = parentViewModel.GetSchemaNodeExpansionState(pathKey) ?? 
                                ((schemaPropertyNode.NodeType == SchemaNodeType.Object || schemaPropertyNode.NodeType == SchemaNodeType.Array) && (depth < 2));
            
            _editValue = _schemaContextNode.DefaultValue?.ToString() ?? string.Empty;
            
            // Set ModalEditorInstance for schema-only nodes as well
            // ModalEditorInstance = _parentViewModel.UiRegistry.ResolveEditor(_schemaContextNode); // You will need to implement ResolveEditor and uncomment this
            System.Diagnostics.Debug.WriteLine($"DGRIVM Constructor (Schema-Only): NodeName: {NodeName}, PathKey: {_pathKeyForSchemaOnlyNode}, Schema: {_schemaContextNode?.Name}, IsEditable: {IsEditable}, IsSchemaOnly: {IsSchemaOnlyNode}, ModalEditor: {ModalEditorInstance?.GetType().Name}");
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
            OriginLayerIndex = -1; // Add item placeholders don't have an origin layer
            _isExpandedInternal = false; // Placeholders are not expandable
            _editValue = string.Empty; // No edit value for add item placeholder initially
            // _pathKeyForSchemaOnlyNode remains empty for AddItem placeholders

            // Create a unique, stable key so this placeholder can be found after a refresh.
            _pathKeyForSchemaOnlyNode = $"{parentArrayNode.Path}/_add_";
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
        /// True if this row represents an item in an array.
        /// </summary>
        public bool IsInArray => DomNode?.Parent is ArrayNode || _isAddItemPlaceholder;

        /// <summary>
        /// Determines if the context menu should show options for adding array items.
        /// This is true if the node is an array itself, or is an item within an array.
        /// </summary>
        public bool IsArrayContext => IsInArray || (DomNode is ArrayNode) || (IsSchemaOnlyNode && NodeType == ViewModelNodeType.Array);


        /// <summary>
        /// Gets a value indicating if this item should be edited with a ComboBox
        /// because it represents an enum or a list of allowed values.
        /// </summary>
        public bool IsEnumBased => SchemaContextNode?.AllowedValues?.Any() == true;

        /// <summary>
        /// Exposes the list of allowed values for enum-based nodes directly to the view.
        /// </summary>
        public IReadOnlyList<string>? AllowedValues => SchemaContextNode?.AllowedValues;

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
        /// Gets the icon character representing the node type.
        /// </summary>
        public string NodeTypeIcon
        {
            get
            {
                // For schema-only nodes, determine type from the schema context
                if (IsSchemaOnlyNode)
                {
                    return _schemaContextNode?.NodeType switch
                    {
                        SchemaNodeType.Object => "📁", // Folder icon for objects
                        SchemaNodeType.Array => "⛓",  // Chain/List icon for arrays
                        _ => "•"                     // Bullet for values
                    };
                }

                // For real DOM nodes, determine type from the node itself
                return _domNode switch
                {
                    ObjectNode => "📁",
                    ArrayNode => "⛓",
                    _ => "•"
                };
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
                    // For schema-only nodes, EditValue holds the current input or default for editing
                    return _editValue; 
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
                    System.Diagnostics.Debug.WriteLine($"VM '{NodeName}' (PathKey: {_pathKeyForSchemaOnlyNode}, Hash: {GetHashCode()}): IsExpanded changed from {oldValue} to {_isExpandedInternal}. Firing OnPropertyChanged.");
                    OnPropertyChanged(); 

                    if (IsSchemaOnlyNode && !string.IsNullOrEmpty(_pathKeyForSchemaOnlyNode))
                    {
                        ParentViewModel.SetSchemaNodeExpansionState(_pathKeyForSchemaOnlyNode, _isExpandedInternal);
                    }

                    System.Diagnostics.Debug.WriteLine($"VM '{NodeName}' (PathKey: {_pathKeyForSchemaOnlyNode}, Hash: {GetHashCode()}): Calling ParentViewModel.OnExpansionChanged.");
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
                if (IsSchemaOnlyNode && !string.IsNullOrEmpty(_pathKeyForSchemaOnlyNode)) // Also update persisted state if set internally
                {
                    ParentViewModel.SetSchemaNodeExpansionState(_pathKeyForSchemaOnlyNode, _isExpandedInternal);
                }
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
                bool isCurrentlyEditable;
                if (_isAddItemPlaceholder || IsSchemaOnlyNode)
                    isCurrentlyEditable = true;
                else if (IsDomNodePresent)
                {
                    if (_schemaContextNode?.IsReadOnly == true)
                        isCurrentlyEditable = false;
                    else
                        isCurrentlyEditable = _domNode is ValueNode || _domNode is RefNode;
                }
                else
                {
                    isCurrentlyEditable = false;
                }
                System.Diagnostics.Debug.WriteLine($"DGRIVM IsEditable: NodeName: {NodeName}, Path: {_domNode?.Path ?? _pathKeyForSchemaOnlyNode}, IsDomNodePresent: {IsDomNodePresent}, IsSchemaOnlyNode: {IsSchemaOnlyNode}, SchemaContext: {_schemaContextNode?.Name}, SchemaIsReadOnly: {_schemaContextNode?.IsReadOnly}, DomNodeType: {_domNode?.GetType().Name}, Result: {isCurrentlyEditable}");
                return isCurrentlyEditable;
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
                if (SetProperty(ref _isInEditMode, value))
                {
                    // If entering edit mode, capture current value for potential revert
                    if (_isInEditMode)
                    {
                        InitializeEditValue();
                        _originalValueBeforeEdit = EditValue; // Assuming EditValue holds current committed value
                    }
                    // If exiting edit mode, ParentViewModel might need to know.
                    // ParentViewModel.HandleEditModeChange(this, _isInEditMode);
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
            // This is now a getter that queries the MainViewModel's search results
            get 
            {
                if (_parentViewModel == null) return false;
                if (DomNode != null)
                {
                    return _parentViewModel.IsDomNodeInSearchResults(DomNode);
                }
                return false; // Default if no context
            }
            // Setter is removed. Highlighting is driven by MainViewModel's state.
            // set => SetProperty(ref _isHighlightedInSearch, value); // Keep for now for ClearHighlight, then remove if not needed
        }

        public void ClearHighlight()
        {
            // This method might not be strictly necessary if IsHighlightedInSearch is purely a getter
            // and the MainViewModel handles clearing its global sets, triggering a refresh that updates this getter.
            // However, if direct manipulation or notification is ever needed, it can be implemented here.
            // For now, let's ensure it sets the underlying field if it exists, or simply trigger a property change
            // to force re-evaluation by bindings.
            if (_isHighlightedInSearch) // only change and notify if it was true
            {
                 _isHighlightedInSearch = false; // Directly set to false
                 OnPropertyChanged(nameof(IsHighlightedInSearch)); // Notify UI to re-evaluate
            }
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

        // --- Public Properties for DataBinding and Logic ---

        /// <summary>
        /// Gets the index of the layer that this node's value originates from.
        /// -1 for schema-only nodes and placeholders.
        /// </summary>
        public int OriginLayerIndex { get; }

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
                    bool success = ParentViewModel.AddArrayItem(_parentArrayNodeForPlaceholder!, EditValue, _itemSchemaForPlaceholder);
                    if (success) IsInEditMode = false;
                    return success;
                }
                else if (IsSchemaOnlyNode)
                {
                    // bool success = _parentViewModel.MaterializeSchemaNodeAndBeginEdit(this, EditValue); // OLD
                    bool success = _parentViewModel.CreateNodeFromSchemaWithValue(this, EditValue); // NEW
                    if (success) IsInEditMode = false;
                    return success;
                }
                else if (_domNode != null)
                {

                    // For direct edits, we must find the *actual* node in the source layer,
                    // because _domNode might be a clone from the merged view.
                    if (OriginLayerIndex == ParentViewModel.ActiveEditorLayer?.LayerIndex)
                    {
                        // Find the real node in the source-of-truth layer's tree
                        var realNode = ParentViewModel.FindNodeInSourceLayer(_domNode.Path, OriginLayerIndex);

                        if (realNode is ValueNode realValueNode)
                        {
                            JsonElement oldValue = realValueNode.Value.Clone();
                            if (realValueNode.TryUpdateFromString(EditValue))
                            {
                                // *** FIX: Also update the local (cloned) node so the UI refreshes instantly ***
                                if (_domNode is ValueNode clonedValueNode)
                                {
                                    clonedValueNode.TryUpdateFromString(EditValue);
                                }

                                var newValue = realValueNode.Value;
                                if (oldValue.ValueKind != newValue.ValueKind || oldValue.GetRawText() != newValue.GetRawText())
                                {
                                    var activeLayerIndex = ParentViewModel.ActiveEditorLayer?.LayerIndex ?? 0;
                                    var operation = new ValueEditOperation(activeLayerIndex, realValueNode, oldValue, newValue);
                                    ParentViewModel.HistoryService.Record(operation);
                                }
                                SetValidationState(true, "");
                                IsInEditMode = false;
                                return true;
                            }
                            else
                            {
                                SetValidationState(false, "Invalid value format");
                                return false;
                            }
                        }
                        else if (realNode is RefNode realRefNode)
                        {
                            string oldPath = realRefNode.ReferencePath;
                            if (oldPath != EditValue)
                            {
                                // Update the real node
                                realRefNode.ReferencePath = EditValue;
                            
                                // *** FIX: Also update the local (cloned) node ***
                                if (_domNode is RefNode clonedRefNode)
                                {
                                    clonedRefNode.ReferencePath = EditValue;
                                }
                            
                                // Note: The RefNode edit is not being added to the undo history, which is a separate issue.
                                ParentViewModel.OnNodeValueChanged(this);
                            }
                            SetValidationState(true, "");
                            IsInEditMode = false;
                            return true;
                        }
                    }
                    else
                    {
                        // This is an override. The node is inherited from a lower layer.
                        // Call the new method on MainViewModel to handle creating the override.
                        ParentViewModel.CreateOverride(_domNode.Path, EditValue, _schemaContextNode);
                        IsInEditMode = false;
                        return true;
                    }
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

        public string SchemaNodePathKey => _pathKeyForSchemaOnlyNode; // Public getter

        public void ReEvaluateHighlightStatus()
        {
            OnPropertyChanged(nameof(IsHighlightedInSearch));
        }

        /// <summary>
        /// Gets a value indicating whether this node's value is defined in the currently active editor layer,
        /// but only when in a multi-layer cascade project.
        /// </summary>
        public bool IsDefinedInActiveLayer
        {
            get
            {
                // The node should only be highlighted as bold if cascade mode is active
                // AND the node's origin matches the currently selected active layer.
                return _parentViewModel.IsCascadeModeActive && (OriginLayerIndex == _parentViewModel.ActiveEditorLayer?.LayerIndex);
            }
        }

        /// <summary>
        /// Forces the ViewModel to synchronize its state with a new value from the data model.
        /// This is called after an Undo/Redo of a value change to update the UI without a full refresh.
        /// </summary>
        public void SyncValueFromModel(JsonElement newValue)
        {
            // Update the display clone's value. This is the crucial step.
            if (_domNode is ValueNode clonedNode)
            {
                clonedNode.Value = newValue;
            }

            // Trigger a refresh of all data-bound properties on the ViewModel.
            RefreshDisplayProperties();
        }

        /// <summary>
        /// Gets the name of the layer this node's value originates from.
        /// Returns "Schema" for schema-only nodes.
        /// </summary>
        public string OriginLayerName
        {
            get
            {
                if (OriginLayerIndex < 0)
                {
                    return "Schema";
                }
                // Ask the MainViewModel to resolve the layer name from the index
                return ParentViewModel.GetLayerNameByIndex(OriginLayerIndex) ?? "Unknown";
            }
        }

        /// <summary>
        /// Gets the relative file path where this node's value originates from.
        /// </summary>
        public string OriginFilePath
        {
            get
            {
                // For placeholders or container nodes (Object/Array), display nothing.
                if (IsSchemaOnlyNode || IsAddItemPlaceholder || DomNode is ObjectNode || DomNode is ArrayNode)
                {
                    return string.Empty;
                }

                if (DomNode == null)
                {
                    return string.Empty;
                }

                // For leaf nodes (ValueNode, RefNode), find their definitive origin file.
                // The OriginLayerIndex property tells us which layer provides the final, effective value.
                if (OriginLayerIndex >= 0 && OriginLayerIndex < ParentViewModel.AllLayers.Count)
                {
                    // Get the correct layer using the index.
                    var originLayer = ParentViewModel.AllLayers[OriginLayerIndex];
            
                    // Look up the node's path in that specific layer's origin map.
                    if (originLayer.IntraLayerValueOrigins.TryGetValue(DomNode.Path, out var filePath))
                    {
                        return filePath;
                    }
                }

                // This provides a fallback for newly created nodes that might not have an
                // origin index assigned yet, but whose origin has been tracked in the active layer.
                if (ParentViewModel.ActiveEditorLayer != null && 
                    ParentViewModel.ActiveEditorLayer.IntraLayerValueOrigins.TryGetValue(DomNode.Path, out var newFilePath))
                {
                    return newFilePath;
                }

                // If no origin can be found for the leaf node, return an empty string.
                return string.Empty;
            }
        }
        
        public List<LayerMenuItemViewModel> OverrideSourceLayers  
        {  
            get  
            {  
                if (!IsDomNodePresent || DomNode == null)  
                {  
                    return new List<LayerMenuItemViewModel>();  
                }  
                // Call the new authoritative method for the context menu  
                return ParentViewModel.GetAuthoritativeOverrideLayersForNode(DomNode.Path);  
            }  
        }

        public enum ViewModelNodeType
        {
            Object,
            Array,
            Value
        }

        /// <summary>
        /// Gets the simple type of the node for styling triggers.
        /// </summary>
        public ViewModelNodeType NodeType
        {
            get
            {
                if (IsSchemaOnlyNode)
                {
                    return _schemaContextNode?.NodeType switch
                    {
                        SchemaNodeType.Object => ViewModelNodeType.Object,
                        SchemaNodeType.Array => ViewModelNodeType.Array,
                        _ => ViewModelNodeType.Value
                    };
                }

                return _domNode switch
                {
                    ObjectNode => ViewModelNodeType.Object,
                    ArrayNode => ViewModelNodeType.Array,
                    _ => ViewModelNodeType.Value
                };
            }
        }

    }
} 
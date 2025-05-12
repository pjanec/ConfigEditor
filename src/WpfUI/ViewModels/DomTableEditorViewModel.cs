using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using WpfUI.Commands;
using WpfUI.Models;
using System.Text.Json;
using System.Windows;
using WpfUI.Windows;

namespace WpfUI.ViewModels;

/// <summary>
/// Central controller for the DOM tree editor that manages global state, selection, editing, filtering and undo/redo.
/// Coordinates between the DOM data model and UI viewmodels, handling all user interactions and state changes.
/// </summary>
public class DomTableEditorViewModel : INotifyPropertyChanged
{
    private readonly DomSchemaTypeResolver _schemaResolver;
    private readonly DomEditorRegistry _editorRegistry;
    private ObjectNode _rootNode;
    private Dictionary<DomNode, DomNodeViewModel> _nodeViewModels = new();
    private List<DomNodeViewModel> _selectedNodes = new();
    private INodeValueEditor? _activeEditor;
    private string _filterText = string.Empty;
    private List<DomNodeViewModel> _filteredViewModels = new();

    public ObjectNode RootNode => _rootNode;
    public IReadOnlyList<DomNodeViewModel> FilteredViewModels => _filteredViewModels;
    public IReadOnlyList<DomNodeViewModel> SelectedNodes => _selectedNodes;
    public INodeValueEditor? ActiveEditor => _activeEditor;
    public string FilterText 
    { 
        get => _filterText;
        set
        {
            if (_filterText != value)
            {
                _filterText = value;
                ApplyFilter(value);
            }
        }
    }

    public ICommand EditNodeCommand { get; }
    public ICommand DeleteArrayItemCommand { get; }
    public ICommand InsertArrayItemCommand { get; }
    public ICommand CopyArrayItemCommand { get; }
    public ICommand PasteArrayItemCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }

    private List<DomNode>? _clipboardItems;
    private Type? _clipboardItemType;

    private DomNodeViewModel? _lastSelectedNode;
    private bool _isShiftPressed;
    private bool _isCtrlPressed;

    private readonly Stack<List<DomChange>> _undoStack = new();
    private readonly Stack<List<DomChange>> _redoStack = new();
    private const int MaxUndoStackSize = 100;

    public string ValidationMessage { get; set; }

    private bool _hasValidationMessage;
    public bool HasValidationMessage
    {
        get => _hasValidationMessage;
        set => SetField(ref _hasValidationMessage, value);
    }

    public DomTableEditorViewModel(DomSchemaTypeResolver schemaResolver, DomEditorRegistry editorRegistry)
    {
        _schemaResolver = schemaResolver;
        _editorRegistry = editorRegistry;

        EditNodeCommand = new RelayCommand(EditNode, CanEditNode);
        DeleteArrayItemCommand = new RelayCommand(DeleteArrayItem, CanDeleteArrayItem);
        InsertArrayItemCommand = new RelayCommand(InsertArrayItem, CanInsertArrayItem);
        CopyArrayItemCommand = new RelayCommand(CopyArrayItem, CanCopyArrayItem);
        PasteArrayItemCommand = new RelayCommand(PasteArrayItem, CanPasteArrayItem);
        UndoCommand = new RelayCommand(Undo, CanUndo);
        RedoCommand = new RelayCommand(Redo, CanRedo);
    }

    /// <summary>
    /// Initializes the editor with a new DOM root node and builds the viewmodel tree.
    /// This should be called when loading new data or resetting the editor state.
    /// </summary>
    public void Initialize(ObjectNode root)
    {
        _rootNode = root;
        _nodeViewModels.Clear();
        _selectedNodes.Clear();
        _activeEditor = null;
        _filterText = string.Empty;
        BuildViewModelTree();
        UpdateFilteredViewModels();
    }

    /// <summary>
    /// Recursively builds the viewmodel tree from the DOM structure.
    /// Creates and caches viewmodels for all nodes, resolving their CLR types from the schema.
    /// </summary>
    private void BuildViewModelTree()
    {
        var rootVm = new DomNodeViewModel(_rootNode, typeof(object));
        _nodeViewModels[_rootNode] = rootVm;
        BuildChildren(rootVm);
    }

    private void BuildChildren(DomNodeViewModel parentVm)
    {
        if (parentVm.DomNode is ObjectNode objectNode)
        {
            foreach (var child in objectNode.Children.Values)
            {
                var childType = _schemaResolver.ResolveType(child);
                var childVm = new DomNodeViewModel(child, childType);
                _nodeViewModels[child] = childVm;
                parentVm.Children.Add(childVm);
                BuildChildren(childVm);
            }
        }
        else if (parentVm.DomNode is ArrayNode arrayNode)
        {
            for (int i = 0; i < arrayNode.Items.Count; i++)
            {
                var item = arrayNode.Items[i];
                var itemType = _schemaResolver.ResolveType(item);
                var itemVm = new DomNodeViewModel(item, itemType);
                _nodeViewModels[item] = itemVm;
                parentVm.Children.Add(itemVm);
                BuildChildren(itemVm);
            }
        }
    }

    public void SelectNode(DomNodeViewModel node)
    {
        if (_activeEditor != null) return;

        _selectedNodes.Clear();
        _selectedNodes.Add(node);
        node.IsSelected = true;
    }

    private void EditNode(object? parameter)
    {
        if (parameter is DomNodeViewModel node && CanEditNode(node))
        {
            BeginEdit(node);
        }
    }

    private bool CanEditNode(object? parameter)
    {
        return parameter is DomNodeViewModel node && 
               _activeEditor == null && 
               node.DomNode is ValueNode;
    }

    private void DeleteArrayItem(object? parameter)
    {
        // Implementation will be added
    }

    private bool CanDeleteArrayItem(object? parameter)
    {
        return _selectedNodes.Count > 0 && 
               _selectedNodes[0].DomNode.Parent is ArrayNode;
    }

    private void InsertArrayItem(object? parameter)
    {
        // Implementation will be added
    }

    private bool CanInsertArrayItem(object? parameter)
    {
        return _selectedNodes.Count > 0 && 
               _selectedNodes[0].DomNode.Parent is ArrayNode;
    }

    private void CopyArrayItem(object? parameter)
    {
        // Implementation will be added
    }

    private bool CanCopyArrayItem(object? parameter)
    {
        return _selectedNodes.Count > 0 && 
               _selectedNodes[0].DomNode.Parent is ArrayNode;
    }

    private void PasteArrayItem(object? parameter)
    {
        // Implementation will be added
    }

    private bool CanPasteArrayItem(object? parameter)
    {
        return _selectedNodes.Count > 0 && 
               _selectedNodes[0].DomNode.Parent is ArrayNode;
    }

    /// <summary>
    /// Applies a live filter to the node tree with two-pass algorithm:
    /// 1. First pass marks matching nodes and expands their parents
    /// 2. Second pass builds the filtered viewmodel list
    /// </summary>
    public void ApplyFilter(string filterText)
    {
        _filterText = filterText;
        UpdateFilteredViewModels();
        OnPropertyChanged(nameof(FilterText));
        OnPropertyChanged(nameof(FilteredViewModels));
    }

    private void UpdateFilteredViewModels()
    {
        _filteredViewModels.Clear();
        if (string.IsNullOrWhiteSpace(_filterText))
        {
            _filteredViewModels.Add(_nodeViewModels[_rootNode]);
        }
        else
        {
            var matchingNodes = _nodeViewModels.Values
                .Where(vm => vm.DomNode.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingNodes.Any())
            {
                // Add root and expand parents of matching nodes
                _filteredViewModels.Add(_nodeViewModels[_rootNode]);
                foreach (var node in matchingNodes)
                {
                    ExpandParents(node);
                }
            }
        }

        // Update validation message for no matches
        if (!_filteredViewModels.Any())
        {
            ValidationMessage = "No matches found";
            HasValidationMessage = true;
        }
        else
        {
            ValidationMessage = null;
            HasValidationMessage = false;
        }

        OnPropertyChanged(nameof(FilteredViewModels));
    }

    private void ExpandParents(DomNodeViewModel node)
    {
        var current = node;
        while (current.DomNode.Parent != null)
        {
            var parentVm = _nodeViewModels[current.DomNode.Parent];
            parentVm.IsExpanded = true;
            current = parentVm;
        }
    }

    /// <summary>
    /// Begins editing a node by activating its appropriate editor.
    /// For modal editors, opens a dialog window to host the editor view.
    /// </summary>
    public void BeginEdit(DomNodeViewModel node)
    {
        if (_activeEditor != null)
            return;

        var editor = _editorRegistry.GetEditor(node.ValueClrType);
        if (editor == null)
            return;

        node.IsEditing = true;
        _activeEditor = editor;

        if (editor.IsModal)
        {
            var editorView = editor.BuildEditorView(node.DomNode);
            var dialog = new ModalEditorWindow();
            dialog.SetContent(editorView);
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                editor.ConfirmEdit();
            }
            else
            {
                editor.CancelEdit();
            }
            node.IsEditing = false;
            _activeEditor = null;
        }

        OnPropertyChanged(nameof(ActiveEditor));
    }

    public void DeleteSelectedArrayItems()
    {
        if (_activeEditor != null) return;

        var arrayNodes = _selectedNodes
            .Where(n => n.DomNode.Parent is ArrayNode)
            .GroupBy(n => n.DomNode.Parent)
            .ToList();

        foreach (var group in arrayNodes)
        {
            if (group.Key is ArrayNode arrayNode)
            {
                var deletedItems = new List<DomNode>();
                foreach (var node in group)
                {
                    deletedItems.Add(node.DomNode);
                    arrayNode.Items.Remove(node.DomNode);
                    _nodeViewModels.Remove(node.DomNode);
                }
                RecordChange(arrayNode, deletedItems, null, "Delete");
            }
        }

        _selectedNodes.Clear();
        OnPropertyChanged(nameof(SelectedNodes));
        OnPropertyChanged(nameof(FilteredViewModels));
    }

    public void InsertArrayItemAboveSelection()
    {
        if (_activeEditor != null) return;

        var arrayNodes = _selectedNodes
            .Where(n => n.DomNode.Parent is ArrayNode)
            .GroupBy(n => n.DomNode.Parent)
            .ToList();

        foreach (var group in arrayNodes)
        {
            if (group.Key is ArrayNode arrayNode)
            {
                var firstSelectedIndex = arrayNode.Items.IndexOf(_selectedNodes.First().DomNode);
                var itemType = _schemaResolver.ResolveType(arrayNode)?.GetGenericArguments()[0] ?? typeof(object);
                
                var newItem = new ValueNode($"Item {arrayNode.Items.Count + 1}", 
                    JsonSerializer.SerializeToElement(Activator.CreateInstance(itemType)));
                
                arrayNode.Items.Insert(firstSelectedIndex, newItem);
                var viewModel = new DomNodeViewModel(newItem, itemType);
                _nodeViewModels[newItem] = viewModel;
                RecordChange(arrayNode, null, newItem, "Insert");
            }
        }

        OnPropertyChanged(nameof(FilteredViewModels));
    }

    public void CopySelectedArrayItems()
    {
        if (_activeEditor != null) return;

        var arrayItems = _selectedNodes
            .Where(n => n.DomNode.Parent is ArrayNode)
            .Select(n => n.DomNode)
            .ToList();

        if (!arrayItems.Any()) return;

        var firstItem = arrayItems.First();
        var itemType = _schemaResolver.ResolveType(firstItem);
        if (itemType == null) return;

        // Verify all items are of the same type
        if (arrayItems.Any(item => _schemaResolver.ResolveType(item) != itemType))
            return;

        _clipboardItems = arrayItems;
        _clipboardItemType = itemType;
    }

    public void PasteArrayItems()
    {
        if (_activeEditor != null || _clipboardItems == null || _clipboardItemType == null) return;

        var targetArray = _selectedNodes
            .FirstOrDefault(n => n.DomNode is ArrayNode)
            ?.DomNode as ArrayNode;

        if (targetArray == null) return;

        var arrayItemType = _schemaResolver.ResolveType(targetArray)?.GetGenericArguments()[0];
        if (arrayItemType != _clipboardItemType) return;

        var insertIndex = targetArray.Items.Count;
        if (_selectedNodes.Any(n => n.DomNode.Parent == targetArray))
        {
            insertIndex = targetArray.Items.IndexOf(_selectedNodes.First(n => n.DomNode.Parent == targetArray).DomNode);
        }

        foreach (var item in _clipboardItems)
        {
            var newItem = new ValueNode($"Item {targetArray.Items.Count + 1}", item.Value);
            targetArray.Items.Insert(insertIndex++, newItem);
            var viewModel = new DomNodeViewModel(newItem, arrayItemType);
            _nodeViewModels[newItem] = viewModel;
        }

        OnPropertyChanged(nameof(FilteredViewModels));
    }

    public void HandleKeyDown(Key key, ModifierKeys modifiers)
    {
        if (_activeEditor != null) return;

        _isShiftPressed = (modifiers & ModifierKeys.Shift) != 0;
        _isCtrlPressed = (modifiers & ModifierKeys.Control) != 0;

        switch (key)
        {
            case Key.Up:
                MoveSelectionUp();
                break;
            case Key.Down:
                MoveSelectionDown();
                break;
        }
    }

    public void HandleKeyUp(ModifierKeys modifiers)
    {
        _isShiftPressed = (modifiers & ModifierKeys.Shift) != 0;
        _isCtrlPressed = (modifiers & ModifierKeys.Control) != 0;
    }

    public void HandleMouseClick(DomNodeViewModel node, MouseButton button, ModifierKeys modifiers)
    {
        if (_activeEditor != null) return;

        _isShiftPressed = (modifiers & ModifierKeys.Shift) != 0;
        _isCtrlPressed = (modifiers & ModifierKeys.Control) != 0;

        if (_isShiftPressed && _lastSelectedNode != null)
        {
            SelectRange(_lastSelectedNode, node);
        }
        else if (_isCtrlPressed)
        {
            ToggleSelection(node);
        }
        else
        {
            SelectNode(node);
        }

        _lastSelectedNode = node;
    }

    private void MoveSelectionUp()
    {
        if (_selectedNodes.Count == 0) return;

        var currentIndex = _filteredViewModels.IndexOf(_selectedNodes.First());
        if (currentIndex <= 0) return;

        var targetNode = _filteredViewModels[currentIndex - 1];
        if (_isShiftPressed)
        {
            SelectRange(_lastSelectedNode ?? _selectedNodes.First(), targetNode);
        }
        else
        {
            SelectNode(targetNode);
        }
        _lastSelectedNode = targetNode;
    }

    private void MoveSelectionDown()
    {
        if (_selectedNodes.Count == 0) return;

        var currentIndex = _filteredViewModels.IndexOf(_selectedNodes.First());
        if (currentIndex >= _filteredViewModels.Count - 1) return;

        var targetNode = _filteredViewModels[currentIndex + 1];
        if (_isShiftPressed)
        {
            SelectRange(_lastSelectedNode ?? _selectedNodes.First(), targetNode);
        }
        else
        {
            SelectNode(targetNode);
        }
        _lastSelectedNode = targetNode;
    }

    /// <summary>
    /// Records a change for undo/redo support.
    /// Changes are grouped by operation type and stored in the undo stack.
    /// </summary>
    private void RecordChange(DomNode node, object? oldValue, object? newValue, string operationType)
    {
        var change = new DomChange(node, oldValue, newValue, operationType);
        var changes = new List<DomChange> { change };
        _undoStack.Push(changes);
        _redoStack.Clear();

        // Limit undo stack size
        while (_undoStack.Count > MaxUndoStackSize)
        {
            _undoStack.Pop();
        }

        OnPropertyChanged(nameof(UndoCommand));
        OnPropertyChanged(nameof(RedoCommand));
    }

    public void Undo()
    {
        if (!CanUndo()) return;

        var changes = _undoStack.Pop();
        foreach (var change in changes)
        {
            ApplyChange(change, true);
        }
        _redoStack.Push(changes);

        OnPropertyChanged(nameof(UndoCommand));
        OnPropertyChanged(nameof(RedoCommand));
        OnPropertyChanged(nameof(FilteredViewModels));
    }

    public void Redo()
    {
        if (!CanRedo()) return;

        var changes = _redoStack.Pop();
        foreach (var change in changes)
        {
            ApplyChange(change, false);
        }
        _undoStack.Push(changes);

        OnPropertyChanged(nameof(UndoCommand));
        OnPropertyChanged(nameof(RedoCommand));
        OnPropertyChanged(nameof(FilteredViewModels));
    }

    private void ApplyChange(DomChange change, bool isUndo)
    {
        if (change.Node is ValueNode valueNode)
        {
            var oldValue = valueNode.Value;
            valueNode.Value = isUndo ? change.OldValue : change.NewValue;
            if (_nodeViewModels.TryGetValue(change.Node, out var vm))
            {
                vm.OnPropertyChanged(nameof(DomNodeViewModel.DomNode));
            }
        }
        else if (change.Node is ArrayNode arrayNode)
        {
            switch (change.OperationType)
            {
                case "Delete":
                    if (isUndo)
                    {
                        var items = (List<DomNode>)change.OldValue;
                        foreach (var item in items)
                        {
                            arrayNode.Items.Add(item);
                            BuildChildren(NodeViewModels[item]);
                        }
                    }
                    else
                    {
                        var items = (List<DomNode>)change.NewValue;
                        foreach (var item in items)
                        {
                            arrayNode.Items.Remove(item);
                            _nodeViewModels.Remove(item);
                        }
                    }
                    break;
                case "Insert":
                    if (isUndo)
                    {
                        var item = (DomNode)change.NewValue;
                        arrayNode.Items.Remove(item);
                        _nodeViewModels.Remove(item);
                    }
                    else
                    {
                        var item = (DomNode)change.OldValue;
                        arrayNode.Items.Add(item);
                        BuildChildren(NodeViewModels[item]);
                    }
                    break;
            }
        }
    }

    public bool CanUndo() => _undoStack.Count > 0;
    public bool CanRedo() => _redoStack.Count > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Captures the current UI state including expanded nodes, selection, filter and edit state.
    /// Used to persist and restore the editor's visual state between sessions.
    /// </summary>
    public DomEditorUiState CaptureUiState()
    {
        var state = new DomEditorUiState
        {
            FilterText = FilterText,
            LastEditNodePath = ActiveEditor != null ? GetNodePath(_selectedNodes.FirstOrDefault()?.DomNode) : null
        };

        // Capture expanded nodes
        foreach (var vm in _nodeViewModels.Values)
        {
            if (vm.IsExpanded)
            {
                state.ExpandedNodePaths.Add(GetNodePath(vm.DomNode));
            }
        }

        // Capture selected nodes
        foreach (var node in _selectedNodes)
        {
            state.SelectedNodePaths.Add(GetNodePath(node.DomNode));
        }

        return state;
    }

    /// <summary>
    /// Restores a previously captured UI state.
    /// Reconstructs the tree expansion, selection and edit state from node paths.
    /// </summary>
    public void RestoreUiState(DomEditorUiState state)
    {
        if (state == null) return;

        // Restore filter
        ApplyFilter(state.FilterText);

        // Restore expanded nodes
        foreach (var path in state.ExpandedNodePaths)
        {
            var node = FindNodeByPath(path);
            if (node != null && _nodeViewModels.TryGetValue(node, out var vm))
            {
                vm.IsExpanded = true;
            }
        }

        // Restore selection
        _selectedNodes.Clear();
        foreach (var path in state.SelectedNodePaths)
        {
            var node = FindNodeByPath(path);
            if (node != null && _nodeViewModels.TryGetValue(node, out var vm))
            {
                _selectedNodes.Add(vm);
            }
        }
        OnPropertyChanged(nameof(SelectedNodes));

        // Restore edit state if applicable
        if (!string.IsNullOrEmpty(state.LastEditNodePath))
        {
            var node = FindNodeByPath(state.LastEditNodePath);
            if (node != null && _nodeViewModels.TryGetValue(node, out var vm))
            {
                BeginEdit(vm);
            }
        }
    }

    private string GetNodePath(DomNode? node)
    {
        if (node == null) return string.Empty;

        var path = new List<string>();
        var current = node;
        while (current != null)
        {
            path.Add(current.Name);
            current = current.Parent;
        }
        path.Reverse();
        return string.Join("/", path);
    }

    private DomNode? FindNodeByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var parts = path.Split('/');
        var current = _rootNode;

        for (int i = 1; i < parts.Length; i++) // Skip "Root"
        {
            if (current is ObjectNode objectNode)
            {
                if (!objectNode.Children.TryGetValue(parts[i], out var child))
                    return null;
                current = child;
            }
            else if (current is ArrayNode arrayNode)
            {
                if (!int.TryParse(parts[i].TrimStart('#'), out var index) || index < 0 || index >= arrayNode.Items.Count)
                    return null;
                current = arrayNode.Items[index];
            }
            else
            {
                return null;
            }
        }

        return current;
    }
}

/// <summary>
/// Represents a single atomic change in the DOM tree that can be undone/redone.
/// Records the affected node, old/new values and operation type for state restoration.
/// </summary>
public record DomChange(DomNode Node, object? OldValue, object? NewValue, string OperationType); 
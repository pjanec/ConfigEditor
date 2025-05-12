using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using WpfUI.Commands;
using WpfUI.Models;
using System.Text.Json;
using System.Windows;

namespace WpfUI.ViewModels;

/// <summary>
/// Central controller for the DOM tree editor that manages global state, selection, editing, filtering and undo/redo.
/// Coordinates between the DOM data model and UI viewmodels, handling all user interactions and state changes.
/// </summary>
public class DomTableEditorViewModel : INotifyPropertyChanged
{
    public ObjectNode RootNode { get; private set; }
    public Dictionary<DomNode, DomNodeViewModel> NodeViewModels { get; } = new();
    public List<DomNodeViewModel> SelectedNodes { get; } = new();
    public INodeValueEditor? ActiveEditor { get; private set; }
    public string FilterText { get; private set; } = string.Empty;
    public List<DomNodeViewModel> FilteredViewModels { get; } = new();
    public DomSchemaTypeResolver SchemaTypeResolver { get; }
    public DomEditorRegistry EditorRegistry { get; }

    private string? _validationMessage;
    public string? ValidationMessage
    {
        get => _validationMessage;
        set => SetField(ref _validationMessage, value);
    }

    private bool _hasValidationMessage;
    public bool HasValidationMessage
    {
        get => _hasValidationMessage;
        set => SetField(ref _hasValidationMessage, value);
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

    public DomTableEditorViewModel(DomSchemaTypeResolver schemaTypeResolver, DomEditorRegistry editorRegistry)
    {
        SchemaTypeResolver = schemaTypeResolver;
        EditorRegistry = editorRegistry;
        EditNodeCommand = new EditNodeCommand(this);
        DeleteArrayItemCommand = new DeleteArrayItemCommand(this);
        InsertArrayItemCommand = new InsertArrayItemCommand(this);
        CopyArrayItemCommand = new CopyArrayItemCommand(this);
        PasteArrayItemCommand = new PasteArrayItemCommand(this);
        UndoCommand = new RelayCommand(Undo, CanUndo);
        RedoCommand = new RelayCommand(Redo, CanRedo);
    }

    /// <summary>
    /// Initializes the editor with a new DOM root node and builds the viewmodel tree.
    /// This should be called when loading new data or resetting the editor state.
    /// </summary>
    public void Initialize(ObjectNode root)
    {
        RootNode = root;
        BuildViewModelTree(root);
        ApplyFilter(FilterText);
    }

    /// <summary>
    /// Recursively builds the viewmodel tree from the DOM structure.
    /// Creates and caches viewmodels for all nodes, resolving their CLR types from the schema.
    /// </summary>
    private void BuildViewModelTree(DomNode node)
    {
        var type = SchemaTypeResolver.ResolveType(node) ?? typeof(object);
        var viewModel = new DomNodeViewModel(node, type);
        NodeViewModels[node] = viewModel;

        if (node is ObjectNode objectNode)
        {
            foreach (var child in objectNode.Children.Values)
            {
                BuildViewModelTree(child);
                viewModel.Children.Add(NodeViewModels[child]);
            }
        }
        else if (node is ArrayNode arrayNode)
        {
            foreach (var item in arrayNode.Items)
            {
                BuildViewModelTree(item);
                viewModel.Children.Add(NodeViewModels[item]);
            }
        }
    }

    public void SelectSingle(DomNodeViewModel node)
    {
        SelectedNodes.Clear();
        SelectedNodes.Add(node);
        OnPropertyChanged(nameof(SelectedNodes));
    }

    public void SelectRange(DomNodeViewModel start, DomNodeViewModel end)
    {
        if (start == null || end == null) return;

        var startIndex = FilteredViewModels.IndexOf(start);
        var endIndex = FilteredViewModels.IndexOf(end);

        if (startIndex == -1 || endIndex == -1) return;

        var minIndex = Math.Min(startIndex, endIndex);
        var maxIndex = Math.Max(startIndex, endIndex);

        SelectedNodes.Clear();
        for (int i = minIndex; i <= maxIndex; i++)
        {
            SelectedNodes.Add(FilteredViewModels[i]);
        }

        OnPropertyChanged(nameof(SelectedNodes));
    }

    public void ToggleSelection(DomNodeViewModel node)
    {
        if (SelectedNodes.Contains(node))
        {
            SelectedNodes.Remove(node);
        }
        else
        {
            SelectedNodes.Add(node);
        }
        OnPropertyChanged(nameof(SelectedNodes));
    }

    /// <summary>
    /// Begins editing a node by activating its appropriate editor.
    /// For modal editors, opens a dialog window to host the editor view.
    /// </summary>
    public void BeginEdit(DomNodeViewModel node)
    {
        if (ActiveEditor != null)
            return;

        var editor = EditorRegistry.GetEditor(node.ValueClrType);
        if (editor == null)
            return;

        ActiveEditor = editor;
        node.IsEditing = true;

        if (editor.IsModal)
        {
            var editorView = editor.BuildEditorView(node.DomNode);
            var dialog = new ModalEditorWindow();
            dialog.SetContent(editorView);
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                ConfirmEdit();
            }
            else
            {
                CancelEdit();
            }
        }

        OnPropertyChanged(nameof(ActiveEditor));
    }

    public void ConfirmEdit()
    {
        if (ActiveEditor == null)
            return;

        if (ActiveEditor.TryGetEditedValue(out var newValue))
        {
            if (SelectedNodes.Count == 1 && SelectedNodes[0].DomNode is ValueNode valueNode)
            {
                var oldValue = valueNode.Value;
                valueNode.Value = JsonSerializer.SerializeToElement(newValue);
                RecordChange(valueNode, oldValue, valueNode.Value, "Edit");
            }
        }

        ActiveEditor = null;
        OnPropertyChanged(nameof(ActiveEditor));
    }

    public void CancelEdit()
    {
        if (ActiveEditor == null)
            return;

        ActiveEditor.CancelEdit();
        ActiveEditor = null;
        OnPropertyChanged(nameof(ActiveEditor));
    }

    /// <summary>
    /// Applies a live filter to the node tree with two-pass algorithm:
    /// 1. First pass marks matching nodes and expands their parents
    /// 2. Second pass builds the filtered viewmodel list
    /// </summary>
    public void ApplyFilter(string filterText)
    {
        FilterText = filterText;
        UpdateFilteredViewModels();
        OnPropertyChanged(nameof(FilterText));
        OnPropertyChanged(nameof(FilteredViewModels));
    }

    private void UpdateFilteredViewModels()
    {
        FilteredViewModels.Clear();
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            FilteredViewModels.Add(NodeViewModels[RootNode]);
            return;
        }

        var filterLower = FilterText.ToLower();
        var hasMatches = false;

        // First pass: Mark matching nodes and their parents
        foreach (var vm in NodeViewModels.Values)
        {
            vm.IsVisible = vm.DomNode.Name.ToLower().Contains(filterLower);
            if (vm.IsVisible)
            {
                hasMatches = true;
                // Expand all parent nodes
                var parent = vm.DomNode.Parent;
                while (parent != null)
                {
                    if (NodeViewModels.TryGetValue(parent, out var parentVm))
                    {
                        parentVm.IsExpanded = true;
                    }
                    parent = parent.Parent;
                }
            }
        }

        // Second pass: Add visible nodes to filtered list
        foreach (var vm in NodeViewModels.Values)
        {
            if (vm.IsVisible)
            {
                FilteredViewModels.Add(vm);
            }
        }

        // Update validation message for no matches
        if (!hasMatches)
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

    public void DeleteSelectedArrayItems()
    {
        if (ActiveEditor != null) return;

        var arrayNodes = SelectedNodes
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
                    NodeViewModels.Remove(node.DomNode);
                }
                RecordChange(arrayNode, deletedItems, null, "Delete");
            }
        }

        SelectedNodes.Clear();
        OnPropertyChanged(nameof(SelectedNodes));
        OnPropertyChanged(nameof(FilteredViewModels));
    }

    public void InsertArrayItemAboveSelection()
    {
        if (ActiveEditor != null) return;

        var arrayNodes = SelectedNodes
            .Where(n => n.DomNode.Parent is ArrayNode)
            .GroupBy(n => n.DomNode.Parent)
            .ToList();

        foreach (var group in arrayNodes)
        {
            if (group.Key is ArrayNode arrayNode)
            {
                var firstSelectedIndex = arrayNode.Items.IndexOf(group.First().DomNode);
                var itemType = SchemaTypeResolver.ResolveType(arrayNode)?.GetGenericArguments()[0] ?? typeof(object);
                
                var newItem = new ValueNode($"Item {arrayNode.Items.Count + 1}", 
                    JsonSerializer.SerializeToElement(Activator.CreateInstance(itemType)));
                
                arrayNode.Items.Insert(firstSelectedIndex, newItem);
                var viewModel = new DomNodeViewModel(newItem, itemType);
                NodeViewModels[newItem] = viewModel;
                RecordChange(arrayNode, null, newItem, "Insert");
            }
        }

        OnPropertyChanged(nameof(FilteredViewModels));
    }

    public void CopySelectedArrayItems()
    {
        if (ActiveEditor != null) return;

        var arrayItems = SelectedNodes
            .Where(n => n.DomNode.Parent is ArrayNode)
            .Select(n => n.DomNode)
            .ToList();

        if (!arrayItems.Any()) return;

        var firstItem = arrayItems.First();
        var itemType = SchemaTypeResolver.ResolveType(firstItem);
        if (itemType == null) return;

        // Verify all items are of the same type
        if (arrayItems.Any(item => SchemaTypeResolver.ResolveType(item) != itemType))
            return;

        _clipboardItems = arrayItems;
        _clipboardItemType = itemType;
    }

    public void PasteArrayItems()
    {
        if (ActiveEditor != null || _clipboardItems == null || _clipboardItemType == null) return;

        var targetArray = SelectedNodes
            .FirstOrDefault(n => n.DomNode is ArrayNode)
            ?.DomNode as ArrayNode;

        if (targetArray == null) return;

        var arrayItemType = SchemaTypeResolver.ResolveType(targetArray)?.GetGenericArguments()[0];
        if (arrayItemType != _clipboardItemType) return;

        var insertIndex = targetArray.Items.Count;
        if (SelectedNodes.Any(n => n.DomNode.Parent == targetArray))
        {
            insertIndex = targetArray.Items.IndexOf(SelectedNodes.First(n => n.DomNode.Parent == targetArray).DomNode);
        }

        foreach (var item in _clipboardItems)
        {
            var newItem = new ValueNode($"Item {targetArray.Items.Count + 1}", item.Value);
            targetArray.Items.Insert(insertIndex++, newItem);
            var viewModel = new DomNodeViewModel(newItem, arrayItemType);
            NodeViewModels[newItem] = viewModel;
        }

        OnPropertyChanged(nameof(FilteredViewModels));
    }

    public void HandleKeyDown(Key key, ModifierKeys modifiers)
    {
        if (ActiveEditor != null) return;

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
        if (ActiveEditor != null) return;

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
            SelectSingle(node);
        }

        _lastSelectedNode = node;
    }

    private void MoveSelectionUp()
    {
        if (SelectedNodes.Count == 0) return;

        var currentIndex = FilteredViewModels.IndexOf(SelectedNodes.First());
        if (currentIndex <= 0) return;

        var targetNode = FilteredViewModels[currentIndex - 1];
        if (_isShiftPressed)
        {
            SelectRange(_lastSelectedNode ?? SelectedNodes.First(), targetNode);
        }
        else
        {
            SelectSingle(targetNode);
        }
        _lastSelectedNode = targetNode;
    }

    private void MoveSelectionDown()
    {
        if (SelectedNodes.Count == 0) return;

        var currentIndex = FilteredViewModels.IndexOf(SelectedNodes.First());
        if (currentIndex >= FilteredViewModels.Count - 1) return;

        var targetNode = FilteredViewModels[currentIndex + 1];
        if (_isShiftPressed)
        {
            SelectRange(_lastSelectedNode ?? SelectedNodes.First(), targetNode);
        }
        else
        {
            SelectSingle(targetNode);
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
            if (NodeViewModels.TryGetValue(change.Node, out var vm))
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
                            BuildViewModelTree(item);
                        }
                    }
                    else
                    {
                        var items = (List<DomNode>)change.NewValue;
                        foreach (var item in items)
                        {
                            arrayNode.Items.Remove(item);
                            NodeViewModels.Remove(item);
                        }
                    }
                    break;
                case "Insert":
                    if (isUndo)
                    {
                        var item = (DomNode)change.NewValue;
                        arrayNode.Items.Remove(item);
                        NodeViewModels.Remove(item);
                    }
                    else
                    {
                        var item = (DomNode)change.OldValue;
                        arrayNode.Items.Add(item);
                        BuildViewModelTree(item);
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
            LastEditNodePath = ActiveEditor != null ? GetNodePath(SelectedNodes.FirstOrDefault()?.DomNode) : null
        };

        // Capture expanded nodes
        foreach (var vm in NodeViewModels.Values)
        {
            if (vm.IsExpanded)
            {
                state.ExpandedNodePaths.Add(GetNodePath(vm.DomNode));
            }
        }

        // Capture selected nodes
        foreach (var node in SelectedNodes)
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
            if (node != null && NodeViewModels.TryGetValue(node, out var vm))
            {
                vm.IsExpanded = true;
            }
        }

        // Restore selection
        SelectedNodes.Clear();
        foreach (var path in state.SelectedNodePaths)
        {
            var node = FindNodeByPath(path);
            if (node != null && NodeViewModels.TryGetValue(node, out var vm))
            {
                SelectedNodes.Add(vm);
            }
        }
        OnPropertyChanged(nameof(SelectedNodes));

        // Restore edit state if applicable
        if (!string.IsNullOrEmpty(state.LastEditNodePath))
        {
            var node = FindNodeByPath(state.LastEditNodePath);
            if (node != null && NodeViewModels.TryGetValue(node, out var vm))
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
        var current = RootNode;

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
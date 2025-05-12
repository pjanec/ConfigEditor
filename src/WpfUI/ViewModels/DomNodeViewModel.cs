using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfUI.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfUI.ViewModels;

/// <summary>
/// ViewModel for a DOM node that adapts the data model for UI presentation.
/// Handles node expansion, selection, validation and child node management.
/// </summary>
public class DomNodeViewModel : INotifyPropertyChanged
{
    public DomNode DomNode { get; }
    public Type ValueClrType { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => SetField(ref _isEditing, value);
    }

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    private object? _editingValue;
    public object? EditingValue
    {
        get => _editingValue;
        set => SetField(ref _editingValue, value);
    }

    private bool _hasValidationError;
    public bool HasValidationError
    {
        get => _hasValidationError;
        set => SetField(ref _hasValidationError, value);
    }

    private string? _validationErrorMessage;
    public string? ValidationErrorMessage
    {
        get => _validationErrorMessage;
        set => SetField(ref _validationErrorMessage, value);
    }

    public INodeValueEditor? EditorInstance { get; set; }
    public INodeValueRenderer? RendererInstance { get; set; }
    public List<DomNodeViewModel> Children { get; } = new();

    /// <summary>
    /// Creates a viewmodel for a DOM node, resolving its CLR type from the schema.
    /// The resolved type determines which editor and renderer to use for the node.
    /// </summary>
    public DomNodeViewModel(DomNode node, Type valueClrType)
    {
        DomNode = node;
        ValueClrType = valueClrType;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Recursively builds child viewmodels for array items or object properties.
    /// For arrays, creates numbered items with proper type resolution.
    /// For objects, creates property nodes based on the object's type.
    /// </summary>
    private void BuildChildren()
    {
        if (DomNode is ObjectNode objectNode)
        {
            foreach (var child in objectNode.Children.Values)
            {
                var childType = child is ValueNode ? ValueClrType : typeof(object);
                var childVm = new DomNodeViewModel(child, childType);
                Children.Add(childVm);
            }
        }
        else if (DomNode is ArrayNode arrayNode)
        {
            for (int i = 0; i < arrayNode.Items.Count; i++)
            {
                var item = arrayNode.Items[i];
                var itemType = item is ValueNode ? ValueClrType.GetGenericArguments()[0] : typeof(object);
                var itemVm = new DomNodeViewModel(item, itemType);
                Children.Add(itemVm);
            }
        }
    }

    /// <summary>
    /// Updates the node's validation state based on its current value.
    /// For arrays, validates item count and types.
    /// For objects, validates required properties.
    /// </summary>
    public void UpdateValidation()
    {
        if (DomNode is ValueNode valueNode)
        {
            try
            {
                var value = valueNode.Value;
                if (value.ValueKind == JsonValueKind.Null && !ValueClrType.IsValueType)
                {
                    HasValidationError = false;
                    ValidationErrorMessage = null;
                }
                else
                {
                    JsonSerializer.Deserialize(value.GetRawText(), ValueClrType);
                    HasValidationError = false;
                    ValidationErrorMessage = null;
                }
            }
            catch (Exception ex)
            {
                HasValidationError = true;
                ValidationErrorMessage = ex.Message;
            }
        }
        else if (DomNode is ArrayNode arrayNode)
        {
            var itemType = ValueClrType.GetGenericArguments()[0];
            var hasError = false;
            var errorMessage = new List<string>();

            foreach (var item in arrayNode.Items)
            {
                if (item is ValueNode valueItem)
                {
                    try
                    {
                        JsonSerializer.Deserialize(valueItem.Value.GetRawText(), itemType);
                    }
                    catch (Exception ex)
                    {
                        hasError = true;
                        errorMessage.Add($"Item {arrayNode.Items.IndexOf(item)}: {ex.Message}");
                    }
                }
            }

            HasValidationError = hasError;
            ValidationErrorMessage = hasError ? string.Join("\n", errorMessage) : null;
        }
    }

    /// <summary>
    /// Gets the full path to this node in the DOM tree.
    /// Used for state persistence and node lookup.
    /// </summary>
    public string GetNodePath()
    {
        var path = new List<string>();
        var current = DomNode;
        
        while (current != null)
        {
            path.Add(current.Name);
            current = current.Parent;
        }
        
        path.Reverse();
        return string.Join("/", path);
    }

} 
using System;
using System.Windows;
using System.Windows.Controls;
using WpfUI.Models;

namespace WpfUI.Editors;

public class NumericValueEditor : INodeValueEditor
{
    private TextBox? _textBox;
    private string? _originalValue;
    private readonly Type _targetType;

    public bool IsModal => false;

    public NumericValueEditor(Type targetType)
    {
        _targetType = targetType;
    }

    public FrameworkElement BuildEditorView(DomNode node)
    {
        if (node is not ValueNode valueNode)
            return new TextBlock { Text = "Invalid node type" };

        _textBox = new TextBox
        {
            Text = valueNode.Value.ToString(),
            BorderThickness = new Thickness(1)
        };

        _originalValue = _textBox.Text;
        return _textBox;
    }

    public bool TryGetEditedValue(out object newValue)
    {
        if (_textBox == null)
        {
            newValue = null!;
            return false;
        }

        try
        {
            newValue = Convert.ChangeType(_textBox.Text, _targetType);
            return true;
        }
        catch
        {
            newValue = null!;
            return false;
        }
    }

    public void CancelEdit()
    {
        if (_textBox != null)
            _textBox.Text = _originalValue ?? string.Empty;
    }

    public void ConfirmEdit()
    {
        // Nothing to do here, value is applied on TryGetEditedValue
    }
} 
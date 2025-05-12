using System.Windows;
using System.Windows.Controls;
using WpfUI.Models;

namespace WpfUI.Editors;

public class StringValueEditor : INodeValueEditor
{
    private TextBox? _textBox;
    private string? _originalValue;

    public bool IsModal => false;

    public FrameworkElement BuildEditorView(DomNode node)
    {
        if (node is not ValueNode valueNode)
            return new TextBlock { Text = "Invalid node type" };

        _textBox = new TextBox
        {
            Text = valueNode.Value.GetString() ?? string.Empty,
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

        newValue = _textBox.Text;
        return true;
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
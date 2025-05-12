using System.Windows;
using System.Windows.Controls;
using WpfUI.Models;

namespace WpfUI.Editors;

public class BooleanValueEditor : INodeValueEditor
{
    private CheckBox? _checkBox;
    private bool _originalValue;

    public bool IsModal => false;

    public FrameworkElement BuildEditorView(DomNode node)
    {
        if (node is not ValueNode valueNode)
            return new TextBlock { Text = "Invalid node type" };

        _checkBox = new CheckBox
        {
            IsChecked = valueNode.Value.GetBoolean(),
            VerticalAlignment = VerticalAlignment.Center
        };

        _originalValue = _checkBox.IsChecked ?? false;
        return _checkBox;
    }

    public bool TryGetEditedValue(out object newValue)
    {
        if (_checkBox == null)
        {
            newValue = null!;
            return false;
        }

        newValue = _checkBox.IsChecked ?? false;
        return true;
    }

    public void CancelEdit()
    {
        if (_checkBox != null)
            _checkBox.IsChecked = _originalValue;
    }

    public void ConfirmEdit()
    {
        // Nothing to do here, value is applied on TryGetEditedValue
    }
} 
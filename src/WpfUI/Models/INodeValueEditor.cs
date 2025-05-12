using System.Windows;

namespace WpfUI.Models;

public interface INodeValueEditor
{
    bool IsModal { get; }
    FrameworkElement BuildEditorView(DomNode node);
    bool TryGetEditedValue(out object newValue);
    void CancelEdit();
    void ConfirmEdit();
} 
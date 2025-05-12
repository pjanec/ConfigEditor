using System.Windows;

namespace WpfUI.Models;

public interface INodeValueRenderer
{
    FrameworkElement BuildRendererView(DomNode node);
    FrameworkElement? BuildHoverDetailsView(DomNode node);
} 
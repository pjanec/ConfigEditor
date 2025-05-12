using System.Windows;
using System.Windows.Controls;
using WpfUI.Models;

namespace WpfUI.Renderers;

public class BooleanValueRenderer : INodeValueRenderer
{
    public FrameworkElement BuildRendererView(DomNode node)
    {
        if (node is not ValueNode valueNode)
            return new TextBlock { Text = "Invalid node type" };

        return new CheckBox
        {
            IsChecked = valueNode.Value.GetBoolean(),
            IsEnabled = false,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public FrameworkElement? BuildHoverDetailsView(DomNode node)
    {
        if (node is not ValueNode valueNode)
            return null;

        return new TextBlock
        {
            Text = valueNode.Value.GetBoolean().ToString()
        };
    }
} 
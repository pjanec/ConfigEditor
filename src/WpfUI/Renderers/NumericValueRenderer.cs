using System.Windows;
using System.Windows.Controls;
using WpfUI.Models;

namespace WpfUI.Renderers;

public class NumericValueRenderer : INodeValueRenderer
{
    public FrameworkElement BuildRendererView(DomNode node)
    {
        if (node is not ValueNode valueNode)
            return new TextBlock { Text = "Invalid node type" };

        return new TextBlock
        {
            Text = valueNode.Value.ToString(),
            TextAlignment = TextAlignment.Right
        };
    }

    public FrameworkElement? BuildHoverDetailsView(DomNode node)
    {
        if (node is not ValueNode valueNode)
            return null;

        return new TextBlock
        {
            Text = valueNode.Value.ToString(),
            TextAlignment = TextAlignment.Right
        };
    }
} 
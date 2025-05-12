using System.Windows;
using System.Windows.Controls;
using WpfUI.Models;

namespace WpfUI.Renderers;

public class StringValueRenderer : INodeValueRenderer
{
    public FrameworkElement BuildRendererView(DomNode node)
    {
        if (node is not ValueNode valueNode)
            return new TextBlock { Text = "Invalid node type" };

        return new TextBlock
        {
            Text = valueNode.Value.GetString() ?? string.Empty,
            TextWrapping = TextWrapping.Wrap
        };
    }

    public FrameworkElement? BuildHoverDetailsView(DomNode node)
    {
        if (node is not ValueNode valueNode)
            return null;

        var text = valueNode.Value.GetString();
        if (string.IsNullOrEmpty(text))
            return null;

        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300
        };
    }
} 
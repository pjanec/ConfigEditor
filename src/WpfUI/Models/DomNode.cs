using System.Text.Json;

namespace WpfUI.Models;

public abstract class DomNode
{
    public string Name { get; }
    public DomNode? Parent { get; set; }

    protected DomNode(string name)
    {
        Name = name;
    }
}

public class ObjectNode : DomNode
{
    public Dictionary<string, DomNode> Children { get; } = new();

    public ObjectNode(string name) : base(name)
    {
    }
}

public class ArrayNode : DomNode
{
    public List<DomNode> Items { get; } = new();

    public ArrayNode(string name) : base(name)
    {
    }
}

public class ValueNode : DomNode
{
    public JsonElement Value { get; set; }

    public ValueNode(string name, JsonElement value) : base(name)
    {
        Value = value;
    }
} 
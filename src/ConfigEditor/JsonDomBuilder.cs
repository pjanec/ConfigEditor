using System;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Utility class to recursively construct a DomNode tree from a JsonElement.
    /// Used by the editor and loader for interpreting parsed JSON.
    /// </summary>
    public static class JsonDomBuilder
    {
        /// <summary>
        /// Builds a DomNode hierarchy from a JsonElement.
        /// </summary>
        /// <param name="name">The name to assign to the root node.</param>
        /// <param name="element">The parsed JSON element.</param>
        /// <param name="parent">The parent node (if any).</param>
        /// <returns>A DomNode tree representing the JSON structure.</returns>
        public static DomNode BuildFromJsonElement(string name, JsonElement element, DomNode? parent = null)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new ObjectNode(name, parent);
                    foreach (var prop in element.EnumerateObject())
                    {
                        var child = BuildFromJsonElement(prop.Name, prop.Value, obj);
                        obj.AddChild(child);
                    }
                    return obj;

                case JsonValueKind.Array:
                    var arr = new ArrayNode(name, parent);
                    int index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        var child = BuildFromJsonElement(index.ToString(), item, arr);
                        arr.Items.Add(child);
                        index++;
                    }
                    return arr;

                default:
                    return new ValueNode(name, element.Clone(), parent);
            }
        }
    }
}

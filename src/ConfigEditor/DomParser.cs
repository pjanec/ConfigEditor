using System.Collections.Generic;
using System.Text.Json;

namespace ConfigDom;

public static class DomParser
{
    public static DomNode ParseFromFlatMap(Dictionary<string, JsonElement> flatMap)
    {
        var root = new ObjectNode("$root", null);

        foreach (var (flatPath, value) in flatMap)
        {
            var segments = flatPath.Split('/');
            Insert(root, segments, 0, value);
        }

        return root;
    }

    private static void Insert(ObjectNode current, string[] segments, int index, JsonElement value)
    {
        string key = segments[index];
        bool isLast = index == segments.Length - 1;

        if (isLast)
        {
            current.Children[key] = new LeafNode(key, value, current);
            return;
        }

        string nextSegment = segments[index + 1];
        bool nextIsArray = int.TryParse(nextSegment, out _);

        if (!current.Children.TryGetValue(key, out var existing))
        {
            existing = nextIsArray
                ? new ArrayNode(key, current)
                : new ObjectNode(key, current);
            current.Children[key] = existing;
        }

        if (nextIsArray)
        {
            var arrayNode = (ArrayNode)existing;
            int arrayIdx = int.Parse(nextSegment);

            while (arrayNode.Items.Count <= arrayIdx)
                arrayNode.Items.Add(new NullNode("$null", arrayNode));

            if (segments.Length - 1 == index + 1)
            {
                arrayNode.Items[arrayIdx] = new LeafNode( nextSegment, value, arrayNode );
            }
            else
            {
                if (arrayNode.Items[arrayIdx] is not ObjectNode innerObj)
                {
                    innerObj = new ObjectNode(nextSegment, arrayNode);
                    arrayNode.Items[arrayIdx] = innerObj;
                }
                Insert(innerObj, segments, index + 2, value);
            }
        }
        else
        {
            Insert((ObjectNode)existing, segments, index + 1, value);
        }
    }
}

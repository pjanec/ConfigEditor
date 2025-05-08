using System.Collections.Generic;
using System.Text.Json;

namespace ConfigDom;

/// <summary>
/// Represents a single layer in the editor's configuration cascade.
/// Each layer maintains its own DOM tree and flat path map.
/// </summary>
public class Json5EditorLayer
{
    /// <summary>
    /// The index of this layer in the cascade (0 = base, higher = more specific).
    /// </summary>
    public int LayerIndex { get; }

    /// <summary>
    /// The display name of this layer (for UI purposes).
    /// </summary>
    public string LayerName { get; }

    /// <summary>
    /// The flat path → value map for this layer.
    /// </summary>
    public Dictionary<string, JsonElement> FlatPathMap { get; }

    /// <summary>
    /// The root node of this layer's DOM tree.
    /// </summary>
    public DomNode RootNode { get; set; }

    public Json5EditorLayer(int layerIndex, string layerName, Dictionary<string, JsonElement> flatPathMap, DomNode rootNode)
    {
        LayerIndex = layerIndex;
        LayerName = layerName;
        FlatPathMap = flatPathMap;
        RootNode = rootNode;
    }
}
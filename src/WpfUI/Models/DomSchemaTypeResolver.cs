using System;
using System.Collections.Generic;

namespace WpfUI.Models;

public class DomSchemaTypeResolver
{
    private readonly Dictionary<string, Type> _typeMap = new();

    public void RegisterType(string nodePath, Type clrType)
    {
        _typeMap[nodePath] = clrType;
    }

    public Type? ResolveType(DomNode node)
    {
        var path = GetNodePath(node);
        return _typeMap.TryGetValue(path, out var type) ? type : null;
    }

    private string GetNodePath(DomNode node)
    {
        var parts = new List<string>();
        var current = node;

        while (current != null)
        {
            parts.Add(current.Name);
            current = current.Parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }
} 
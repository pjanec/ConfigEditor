using System;
using System.Collections.Generic;

namespace WpfUI.Models;

/// <summary>
/// Registry for value editors and renderers that maps CLR types to their UI components.
/// Provides type-based lookup for finding appropriate editors and renderers for DOM nodes.
/// </summary>
public class DomEditorRegistry
{
    private readonly Dictionary<Type, INodeValueEditor> _editors = new();
    private readonly Dictionary<Type, INodeValueRenderer> _renderers = new();

    /// <summary>
    /// Registers an editor for a specific CLR type.
    /// The editor will be used when editing nodes of this type.
    /// </summary>
    public void RegisterEditor(Type type, INodeValueEditor editor)
    {
        _editors[type] = editor;
    }

    /// <summary>
    /// Registers a renderer for a specific CLR type.
    /// The renderer will be used to display nodes of this type.
    /// </summary>
    public void RegisterRenderer(Type type, INodeValueRenderer renderer)
    {
        _renderers[type] = renderer;
    }

    /// <summary>
    /// Gets the appropriate editor for a node's type.
    /// Falls back to object editor if no specific editor is registered.
    /// </summary>
    public INodeValueEditor? GetEditor(Type type)
    {
        return _editors.TryGetValue(type, out var editor) ? editor : null;
    }

    /// <summary>
    /// Gets the appropriate renderer for a node's type.
    /// Falls back to string renderer if no specific renderer is registered.
    /// </summary>
    public INodeValueRenderer? GetRenderer(Type type)
    {
        return _renderers.TryGetValue(type, out var renderer) ? renderer : null;
    }
} 
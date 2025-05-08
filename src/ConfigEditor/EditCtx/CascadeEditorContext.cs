using ConfigEditor.Dom;
using ConfigEditor.History;
using ConfigEditor.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConfigEditor.EditCtx;

/// <summary>
/// Represents a single layer in the configuration cascade.
/// Each layer has a name (for UI display) and a list of source files.
/// </summary>
public record CascadeLayerSource( string Name, List<SourceFile> Files );

/// <summary>
/// Provides a mounted editable DOM subtree from cascading JSON sources.
/// </summary>
public class CascadeEditorContext : IMountedDomEditorContext
{
	private readonly string _mountPath;
	private readonly List<CascadeLayerSource> _sourceLayers;
	private readonly List<EditorCascadeLayer> _editorLayers;
	private readonly DomEditHistory _editHistory = new();
	private readonly MergeOriginTracker _originTracker = new();

	public CascadeEditorContext( string mountPath, List<CascadeLayerSource> sourceLayers )
	{
		_mountPath = mountPath;
		_sourceLayers = sourceLayers;
		_editorLayers = sourceLayers.Select( ( layer, index ) =>
		{
			var flatMap = layer.Files.ToDictionary(
				f => f.RelativePath,
				f => f.DomRoot.ExportJson()
			);
			var rootNode = DomParser.ParseFromFlatMap( flatMap );
			return new EditorCascadeLayer( index, layer.Name, flatMap, rootNode );
		} ).ToList();
	}

	public string MountPath => _mountPath;

	public IReadOnlyList<EditorCascadeLayer> Layers => _editorLayers;

	public void Load()
	{
		// Reload all source layers and rebuild editor layers
		for( int i = 0; i < _sourceLayers.Count; i++ )
		{
			var sourceLayer = _sourceLayers[i];
			var editorLayer = _editorLayers[i];

			// Update flat map with new values
			foreach( var file in sourceLayer.Files )
			{
				editorLayer.FlatPathMap[file.RelativePath] = file.DomRoot.ExportJson();
			}

			// Rebuild DOM tree
			editorLayer.RootNode = DomParser.ParseFromFlatMap( editorLayer.FlatPathMap );
		}
	}

	public DomNode GetRoot() => GetMergedDomUpToLayer( _editorLayers.Count - 1 );

	public DomNode GetMergedDomUpToLayer( int inclusiveLayerIndex )
	{
		return DomMerger.Merge( _editorLayers.Take( inclusiveLayerIndex + 1 ).Select( x => x.RootNode ) );
	}

	public Func<string, DomNode?> GetEffectiveNodeFunc( int inclusiveLayerIndex )
	{
		var mergedRoot = GetMergedDomUpToLayer( inclusiveLayerIndex );
		return path => DomTreePathHelper.FindNodeAtPath( mergedRoot, path );
	}

	public bool TryGetSourceFile( string domPath, out SourceFile? file, out int layerIndex )
	{
		for( int i = _editorLayers.Count - 1; i >= 0; i-- )
		{
			var layer = _editorLayers[i];
			if( layer.FlatPathMap.ContainsKey( domPath ) )
			{
				file = _sourceLayers[i].Files.FirstOrDefault( f => f.RelativePath == domPath );
				layerIndex = i;
				return file != null;
			}
		}

		file = null;
		layerIndex = -1;
		return false;
	}

	public void ApplyEdit( DomEditAction action )
	{
		// Determine which layer to write to
		var origin = _originTracker.GetOrigin( action.Path );
		var targetLayerIndex = origin?.layerIndex ?? _editorLayers.Count - 1; // Default to most specific

		// Get the target layer
		var targetLayer = _editorLayers[targetLayerIndex];

		// Create or update the value in the layer's flat map
		targetLayer.FlatPathMap[action.Path] = action.NewValue;

		// Rebuild the layer's DOM tree
		targetLayer.RootNode = DomParser.ParseFromFlatMap( targetLayer.FlatPathMap );

		// Record the edit for undo/redo
		_editHistory.Apply( action );
	}

	public void Undo()
	{
		var undo = _editHistory.Undo();
		if( undo != null )
		{
			// Find the layer that owns this path
			var origin = _originTracker.GetOrigin( undo.Path );
			if( origin.HasValue )
			{
				var layer = _editorLayers[origin.Value.layerIndex];

				// Update the layer's flat map
				layer.FlatPathMap[undo.Path] = undo.NewValue;

				// Rebuild the layer's DOM tree
				layer.RootNode = DomParser.ParseFromFlatMap( layer.FlatPathMap );
			}
		}
	}

	public void Redo()
	{
		var redo = _editHistory.Redo();
		if( redo != null )
		{
			// Find the layer that owns this path
			var origin = _originTracker.GetOrigin( redo.Path );
			if( origin.HasValue )
			{
				var layer = _editorLayers[origin.Value.layerIndex];

				// Update the layer's flat map
				layer.FlatPathMap[redo.Path] = redo.NewValue;

				// Rebuild the layer's DOM tree
				layer.RootNode = DomParser.ParseFromFlatMap( layer.FlatPathMap );
			}
		}
	}

	public bool CanUndo => _editHistory.CanUndo;
	public bool CanRedo => _editHistory.CanRedo;

	public bool TryResolvePath( string absolutePath, out DomNode? node )
	{
		if( !absolutePath.StartsWith( _mountPath.TrimEnd( '/' ) ) )
		{
			node = null;
			return false;
		}

		node = DomTreePathHelper.FindNodeAtPath( GetRoot(), absolutePath );
		return node != null;
	}

	public string GetLevelName( int layerIndex )
	{
		return layerIndex >= 0 && layerIndex < _editorLayers.Count
			? _editorLayers[layerIndex].LayerName
			: "Unknown";
	}

	public int GetLevelIndex( string path )
	{
		for( int i = _editorLayers.Count - 1; i >= 0; i-- )
		{
			if( _editorLayers[i].FlatPathMap.ContainsKey( path ) )
				return i;
		}
		return -1;
	}
}
using ConfigEditor.Dom;
using System.Collections.Generic;
using ConfigEditor.History;
using ConfigEditor.IO;

namespace ConfigEditor.EditCtx
{
	/// <summary>
	/// Represents an editable config context based on a single flat JSON file.
	/// Simpler than cascaded contexts, does not support layering.
	/// Useful for plugins or one-off files.
	/// </summary>
	public class FlatJsonEditorContext : IMountedDomEditorContext
	{
		public string MountPath { get; }
		private readonly string _filePath;
		private SourceFile _file;
		private readonly DomEditHistory _history = new();

		public FlatJsonEditorContext( string mountPath, SourceFile file )
		{
			MountPath = mountPath;
			_filePath = file.FilePath;
			_file = file;
		}

		public FlatJsonEditorContext( string mountPath, string filePath )
		{
			MountPath = mountPath;
			_filePath = filePath;
			_file = Json5SourceFileLoader.LoadSingleFile( filePath );
		}

		public void Load()
		{
			_file = Json5SourceFileLoader.LoadSingleFile( _filePath );
		}

		public DomNode GetRoot() => _file.DomRoot;

		public bool TryGetSourceFile( string domPath, out SourceFile? file, out int layerIndex )
		{
			file = domPath.StartsWith( MountPath ) ? _file : null;
			layerIndex = 0;
			return file != null;
		}

		public void ApplyEdit( DomEditAction action )
		{
			_history.Apply( action );
			action.Apply( _file.DomRoot );
		}

		public void Undo()
		{
			var undo = _history.Undo();
			undo?.Apply( _file.DomRoot );
		}

		public void Redo()
		{
			var redo = _history.Redo();
			redo?.Apply( _file.DomRoot );
		}

		public bool CanUndo => _history.CanUndo;
		public bool CanRedo => _history.CanRedo;

		public bool TryResolvePath( string absolutePath, out DomNode? node )
		{
			node = DomTreePathHelper.FindNodeAtPath( _file.DomRoot, absolutePath );
			return node != null;
		}
	}
}

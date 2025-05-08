using ConfigEditor.Dom;
using ConfigEditor.History;
using ConfigEditor.IO;

namespace ConfigEditor.EditCtx
{
	/// <summary>
	/// Represents a provider of a mounted editable DOM subtree within the editor.
	/// Used for cascade sources, flat JSON, or other editable branches.
	/// </summary>
	public interface IMountedDomEditorContext
	{
		/// <summary>
		/// The path under which this context is mounted in the master DOM.
		/// </summary>
		string MountPath { get; }

		/// <summary>
		/// Loads or reloads all source files and builds the root DOM subtree.
		/// </summary>
		void Load();

		/// <summary>
		/// Returns the root node of the subtree.
		/// </summary>
		/// <returns>The root DOM node.</returns>
		DomNode GetRoot();

		/// <summary>
		/// Attempts to retrieve the original source file responsible for a given DOM path.
		/// </summary>
		/// <param name="domPath">The full path to a node.</param>
		/// <param name="file">The file responsible, if found.</param>
		/// <returns>True if found; false otherwise.</returns>
		bool TryGetSourceFile( string domPath, out SourceFile? file, out int layerIndex );

		/// <summary>
		/// Applies an edit to the live in-memory model.
		/// </summary>
		/// <param name="action">The edit action to apply.</param>
		void ApplyEdit( DomEditAction action );

		/// <summary>
		/// Undo the last edit.
		/// </summary>
		void Undo();

		/// <summary>
		/// Redo the last undone edit.
		/// </summary>
		void Redo();

		/// <summary>
		/// Whether an undo action is currently available.
		/// </summary>
		bool CanUndo { get; }

		/// <summary>
		/// Whether a redo action is currently available.
		/// </summary>
		bool CanRedo { get; }

		/// <summary>
		/// Attempts to resolve a path within this subtree.
		/// Used for resolving references.
		/// </summary>
		/// <param name="absolutePath">Absolute path from DOM root.</param>
		/// <param name="node">Resolved node, if successful.</param>
		/// <returns>True if the path resolves to a node.</returns>
		bool TryResolvePath( string absolutePath, out DomNode? node );
	}

	/// <summary>
	/// Extension helpers for resolving paths within editor contexts.
	/// </summary>
	public static class MountedDomEditorContextExtensions
	{
		public static bool DefaultTryResolvePath( this IMountedDomEditorContext context, string absolutePath, out DomNode? node )
		{
			if( !absolutePath.StartsWith( context.MountPath.TrimEnd( '/' ) ) )
			{
				node = null;
				return false;
			}

			node = DomTreePathHelper.FindNodeAtPath( context.GetRoot(), absolutePath );
			return node != null;
		}
	}
}

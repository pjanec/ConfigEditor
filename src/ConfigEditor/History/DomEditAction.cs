using ConfigEditor.Dom;
using System.Text.Json;

namespace ConfigEditor.History
{
	/// <summary>
	/// Represents a single atomic edit to a DOM node.
	/// Supports forward and inverse application for undo/redo.
	/// </summary>
	public class DomEditAction
	{
		public string Path { get; }
		public JsonElement NewValue { get; }
		public JsonElement OldValue { get; }

		public DomEditAction( string path, JsonElement newValue, JsonElement oldValue )
		{
			Path = path;
			NewValue = newValue;
			OldValue = oldValue;
		}

		/// <summary>
		/// Applies this edit to the specified DOM root.
		/// </summary>
		public void Apply( DomNode root )
		{
			DomTreePathHelper.SetValueAtPath( root, Path, NewValue );
		}

		/// <summary>
		/// Generates an inverse edit using the original OldValue.
		/// </summary>
		public DomEditAction GetInverse()
		{
			return new DomEditAction( Path, OldValue, NewValue );
		}
	}
}

using ConfigEditor.IO;
using System.Collections.Generic;

namespace ConfigEditor.EditCtx
{
	/// <summary>
	/// Tracks the origin (layer index and source file) of each node in the merged DOM tree.
	/// </summary>
	public class MergeOriginTracker
	{
		private readonly Dictionary<string, (SourceFile file, int layerIndex)> _origins = new();

		/// <summary>
		/// Records the origin of a node at the given path.
		/// </summary>
		public void TrackOrigin( string path, SourceFile file, int layerIndex )
		{
			_origins[path] = (file, layerIndex);
		}

		/// <summary>
		/// Gets the origin information for a node at the given path.
		/// </summary>
		public (SourceFile file, int layerIndex)? GetOrigin( string path )
		{
			return _origins.TryGetValue( path, out var origin ) ? origin : null;
		}

		/// <summary>
		/// Gets the layer index for a node at the given path.
		/// </summary>
		public int? GetLayerIndex( string path )
		{
			return GetOrigin( path )?.layerIndex;
		}

		/// <summary>
		/// Gets the source file for a node at the given path.
		/// </summary>
		public SourceFile? GetSourceFile( string path )
		{
			return GetOrigin( path )?.file;
		}
	}
}
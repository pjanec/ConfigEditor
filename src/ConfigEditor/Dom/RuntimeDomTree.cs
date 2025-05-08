using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ConfigEditor.Dom
{
	/// <summary>
	/// Hosts the runtime DOM tree and manages providers for static and dynamic branches.
	/// Supports data querying and hot-reload of static subtrees.
	/// </summary>
	public class RuntimeDomTree
	{
		private readonly ObjectNode _masterRoot;
		private readonly Dictionary<string, IRuntimeDomProvider> _providers = new();

		public RuntimeDomTree( ObjectNode root )
		{
			_masterRoot = root;
		}

		/// <summary>
		/// Registers a named provider at a given mount path into the DOM.
		/// </summary>
		public void RegisterProvider( string mountPath, IRuntimeDomProvider provider )
		{
			_providers[mountPath] = provider;
			MountSubtree( mountPath, provider.GetRoot() );
		}

		/// <summary>
		/// Replaces the subtree at the given path with new content (e.g., reloaded BSON).
		/// </summary>
		public void ReloadStaticBranch( string mountPath, DomNode newSubtree )
		{
			MountSubtree( mountPath, newSubtree );
		}

		/// <summary>
		/// Returns the root of the full runtime DOM.
		/// </summary>
		public DomNode GetRoot() => _masterRoot;

		/// <summary>
		/// Typed access to a subtree as a deserialized object.
		/// </summary>
		public T Get<T>( string path )
		{
			var node = DomTreePathHelper.FindNodeAtPath( _masterRoot, path );
			if( node == null )
				throw new InvalidOperationException( $"Path not found: {path}" );

			var json = node.ExportJson();
			return json .Deserialize<T>( ) ?? throw new InvalidOperationException( "Deserialization failed" );
		}

		/// <summary>
		/// Refreshes all providers (no-op for static ones).
		/// </summary>
		public void RefreshAll()
		{
			foreach( var provider in _providers.Values )
				provider.Refresh();
		}

		private void MountSubtree( string mountPath, DomNode subtree )
		{
			var target = DomTreePathHelper.EnsurePathExists( _masterRoot, mountPath );
			if( target is ObjectNode container && subtree is ObjectNode subTreeObj )
			{
				foreach( var child in subTreeObj.Children )
					container.AddChild( child.Value );
			}
		}
	}
}

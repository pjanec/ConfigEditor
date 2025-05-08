using ConfigEditor.Dom;

namespace ConfigEditor.Providers
{
	/// <summary>
	/// Simple wrapper for a static DOM subtree. Used to register fixed branches in editor or runtime.
	/// Implements IRuntimeDomProvider.
	/// </summary>
	public class StaticDomBranchProvider : IRuntimeDomProvider
	{
		private readonly DomNode _root;

		public StaticDomBranchProvider( DomNode root, string? name = null )
		{
			_root = root;
			Name = name ?? "static";
		}

		public string Name { get; }

		public DomNode GetRoot() => _root;

		public void Refresh() { /* No-op for static content */ }
	}
}

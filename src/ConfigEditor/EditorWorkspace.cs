using System.Collections.Generic;

namespace ConfigDom
{
    /// <summary>
    /// Manages the editable master DOM tree and all mounted config sources during editing.
    /// Supports both editable contexts and read-only providers.
    /// </summary>
    public class EditorWorkspace
    {
        private readonly ObjectNode _masterRoot;
        private readonly Dictionary<string, IMountedDomEditorContext> _editableContexts = new();
        private readonly Dictionary<string, IRuntimeDomProvider> _mountedProviders = new();

        public EditorWorkspace(ObjectNode masterRoot)
        {
            _masterRoot = masterRoot;
        }

        /// <summary>
        /// Registers a cascading or flat config source as an editable context.
        /// </summary>
        public void RegisterContext(string mountPath, IMountedDomEditorContext context)
        {
            _editableContexts[mountPath] = context;
            MountSubtree(mountPath, context.GetRoot());
        }

        /// <summary>
        /// Registers a fixed provider (e.g., DB snapshot or static content) as read-only.
        /// </summary>
        public void RegisterProvider(string mountPath, IRuntimeDomProvider provider)
        {
            _mountedProviders[mountPath] = provider;
            MountSubtree(mountPath, provider.GetRoot());
        }

        /// <summary>
        /// Returns the root node of the full editor DOM.
        /// </summary>
        public DomNode GetRoot() => _masterRoot;

        private void MountSubtree(string mountPath, DomNode subtree)
        {
            var target = DomTreePathHelper.EnsurePathExists(_masterRoot, mountPath);
            if (target is ObjectNode container)
            {
                foreach (var child in ((ObjectNode)subtree).Children)
                {
                    container.AddChild(child.Value);
                }
            }
        }
    }
}

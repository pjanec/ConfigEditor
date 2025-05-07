using System;
using System.Collections.Generic;

namespace ConfigDom
{
    /// <summary>
    /// The central registry and coordinator of mounted DOM editor contexts.
    /// Maintains a unified view of all configuration branches for editing.
    /// Responsible for initializing view models, resolving refs, and tracking updates.
    /// </summary>
    public class EditorWorkspace
    {
        private readonly Dictionary<string, IMountedDomEditorContext> _contexts = new();
        private readonly Dictionary<string, DomNodeViewModel> _mountRoots = new();

        /// <summary>
        /// Registers all known editor contexts and builds the master viewmodel tree.
        /// Should be called once at initialization.
        /// </summary>
        /// <param name="mounts">A mapping from mount paths to their corresponding provider contexts.</param>
        public void RegisterContexts(Dictionary<string, IMountedDomEditorContext> mounts)
        {
            _contexts.Clear();
            _mountRoots.Clear();

            foreach (var kvp in mounts)
            {
                string mountPath = kvp.Key;
                var ctx = kvp.Value;
                _contexts[mountPath] = ctx;

                DomNode root = ctx.GetRoot();
                var vm = new DomNodeViewModel(root, isEditable: true);
                _mountRoots[mountPath] = vm;
            }

            ResolveAllRefs();
        }

        /// <summary>
        /// Gets the viewmodel corresponding to the given mount path.
        /// </summary>
        /// <param name="mountPath">The registered mount path.</param>
        /// <returns>The root viewmodel for that mount, or null if not found.</returns>
        public DomNodeViewModel? GetRootViewModel(string mountPath)
        {
            return _mountRoots.TryGetValue(mountPath, out var vm) ? vm : null;
        }

        /// <summary>
        /// Resolves all symbolic $ref references across all registered branches.
        /// Also updates resolved preview values in RefNodeViewModels.
        /// </summary>
        public void ResolveAllRefs()
        {
            foreach (var vm in _mountRoots.Values)
            {
                ResolveRefsRecursive(vm);
            }
        }

        private void ResolveRefsRecursive(DomNodeViewModel vm)
        {
            if (vm is RefNodeViewModel refVm)
            {
                string targetPath = refVm.RefPath;
                foreach (var ctx in _contexts.Values)
                {
                    if (ctx.TryResolvePath(targetPath, out var target))
                    {
                        refVm.ResolvedTargetNode = target;
                        refVm.ResolvedPreviewValue = target.ExportJson().ToString();
                        break;
                    }
                }
            }

            foreach (var child in vm.Children)
                ResolveRefsRecursive(child);
        }
    }
}

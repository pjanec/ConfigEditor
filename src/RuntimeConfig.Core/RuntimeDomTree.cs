using RuntimeConfig.Core.Dom;
using RuntimeConfig.Core.Providers;
using RuntimeConfig.Core.Querying;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RuntimeConfig.Core
{
    public class RuntimeDomTree
    {
        private readonly Dictionary<string, IRuntimeDomProvider> _providers = new();
        public ObjectNode? ResolvedRoot { get; private set; }

        public void RegisterProvider(string mountPath, IRuntimeDomProvider provider)
        {
            _providers[mountPath] = provider;
        }

        public async Task RefreshAsync()
        {
            await Task.Run(() =>
            {
                var rawRoot = new ObjectNode("$root", null);
                foreach (var (mountPath, provider) in _providers)
                {
                    var providerContent = provider.Load();
                    var mountNode = DomTree.FindNodeByPath(rawRoot, mountPath) ?? CreateMountPath(rawRoot, mountPath);
                    if (mountNode is ObjectNode targetObject)
                    {
                        DomMerger.MergeInto(targetObject, providerContent);
                    }
                }
                var resolver = new RefResolver(rawRoot);
                ResolvedRoot = resolver.Resolve();
            });
        }

        public DomQuery Query()
        {
            if (ResolvedRoot == null)
            {
                throw new InvalidOperationException("The configuration tree has not been loaded. Call RefreshAsync() first.");
            }
            return new DomQuery(ResolvedRoot);
        }

        private DomNode CreateMountPath(ObjectNode root, string path)
        {
            var segments = path.Split('/');
            DomNode current = root;
            foreach (var segment in segments)
            {
                if (string.IsNullOrEmpty(segment)) continue;
                if (current is ObjectNode obj)
                {
                    var child = obj.GetChild(segment);
                    if (child == null)
                    {
                        child = new ObjectNode(segment, obj);
                        obj.AddChild(segment, child);
                    }
                    current = child;
                }
                else
                {
                    throw new InvalidOperationException($"Cannot create mount path. Segment '{segment}' conflicts with an existing non-object node.");
                }
            }
            return current;
        }
    }
} 
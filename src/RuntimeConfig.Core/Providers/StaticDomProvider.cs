using RuntimeConfig.Core.Dom;

namespace RuntimeConfig.Core.Providers
{
    public class StaticDomProvider : IRuntimeDomProvider
    {
        private readonly ObjectNode _rootNode;
        public StaticDomProvider(ObjectNode rootNode) { _rootNode = rootNode; }
        public ObjectNode Load() => _rootNode;
        public void Refresh() { /* No-op */ }
    }
} 
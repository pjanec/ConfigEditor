using RuntimeConfig.Core.Dom;

namespace RuntimeConfig.Core.Providers
{
    public interface IRuntimeDomProvider
    {
        ObjectNode Load();
        void Refresh();
    }
} 
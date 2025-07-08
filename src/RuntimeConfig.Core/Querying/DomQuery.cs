using RuntimeConfig.Core.Dom;
using RuntimeConfig.Core.Serialization;
using System.Collections.Generic;
using System.Text.Json;

namespace RuntimeConfig.Core.Querying
{
    public class DomQuery
    {
        private readonly DomNode _queryRoot;
        private readonly JsonDomSerializer _serializer;

        public DomQuery(DomNode queryRoot)
        {
            _queryRoot = queryRoot;
            _serializer = new JsonDomSerializer(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public T Get<T>(string path)
        {
            var node = DomTree.FindNodeByPath(_queryRoot, path);
            if (node == null)
            {
                throw new KeyNotFoundException($"Configuration path '{path}' not found from root '{_queryRoot.Path}'.");
            }
            var jsonElement = _serializer.ToJsonElement(node);
            return jsonElement.Deserialize<T>()!;
        }
    }
} 
using System;
using System.Text.Json;

namespace ConfigDom
{
    /// <summary>
    /// Example dynamic provider that simulates live metric updates.
    /// Can be used in runtime for testing or monitoring.
    /// </summary>
    public class DynamicMetricsProvider : IRuntimeDomProvider
    {
        private readonly ObjectNode _root;
        private readonly Random _rand = new();

        public DynamicMetricsProvider(string name = "metrics")
        {
            Name = name;
            _root = new ObjectNode("metrics");
            GenerateMetrics();
        }

        public string Name { get; }

        public DomNode GetRoot() => _root;

        public void Refresh()
        {
            _root.Children.Clear();
            GenerateMetrics();
        }

        private void GenerateMetrics()
        {
            _root.AddChild(new LeafNode("cpu", JsonDocument.Parse(_rand.NextDouble().ToString("F2")).RootElement));
            _root.AddChild(new LeafNode("mem", JsonDocument.Parse(_rand.Next(1000, 8000).ToString()).RootElement));
        }
    }
}

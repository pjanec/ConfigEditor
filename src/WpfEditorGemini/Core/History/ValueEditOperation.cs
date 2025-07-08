using System.Text.Json;
using RuntimeConfig.Core.Dom;
using JsonConfigEditor.ViewModels;

namespace JsonConfigEditor.Core.History
{
    public class ValueEditOperation : EditOperation
    {
        private readonly ValueNode _node;
        private readonly JsonElement _oldValue;
        private readonly JsonElement _newValue;

        public ValueNode Node => _node;

        public override string? NodePath => _node.Path;

        public ValueEditOperation(int layerIndex, ValueNode node, JsonElement oldValue, JsonElement newValue)
            : base(layerIndex)
        {
            _node = node;
            _oldValue = oldValue.Clone(); // Clone to ensure snapshot
            _newValue = newValue.Clone();
        }

        public override void Redo(MainViewModel vm) => vm.SetNodeValue(_node, _newValue);
        public override void Undo(MainViewModel vm) => vm.SetNodeValue(_node, _oldValue);
    }
} 
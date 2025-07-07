using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.ViewModels;

namespace JsonConfigEditor.Core.History
{
    public class InsertArrayItemOperation : EditOperation
    {
        private readonly ArrayNode _parent;
        private readonly DomNode _newNode;
        private readonly int _index;

        public override string? NodePath => _newNode.Path;

        public InsertArrayItemOperation(int layerIndex, ArrayNode parent, DomNode newNode, int index) : base(layerIndex)
        {
            _parent = parent;
            _newNode = newNode;
            _index = index;
        }

        public override bool RequiresFullRefresh => true;
        public override void Redo(MainViewModel vm) => _parent.InsertItem(_index, _newNode);
        public override void Undo(MainViewModel vm) => _parent.RemoveItemAt(_index);
    }
} 
using RuntimeConfig.Core.Dom;
using JsonConfigEditor.ViewModels;

namespace JsonConfigEditor.Core.History
{
    public class AddNodeOperation : EditOperation
    {
        private readonly DomNode _parent;
        private readonly DomNode _newNode;

        public override string? NodePath => _newNode.Path;

        public AddNodeOperation(int layerIndex, DomNode parent, DomNode newNode) : base(layerIndex)
        {
            _parent = parent;
            _newNode = newNode;
        }

        public override bool RequiresFullRefresh => true;

        public override void Redo(MainViewModel vm) => vm.AddNodeToParent(_parent, _newNode);
        public override void Undo(MainViewModel vm) => vm.RemoveNodeFromParent(_newNode);
    }
} 
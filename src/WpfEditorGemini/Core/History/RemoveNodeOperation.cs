using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.ViewModels;

namespace JsonConfigEditor.Core.History
{
    public class RemoveNodeOperation : EditOperation
    {
        private readonly DomNode _parent;
        private readonly DomNode _removedNode;
        private readonly string _nameOrIndexAtTimeOfRemoval;
        private readonly int _originalIndexInArray;

        public override string? NodePath => _removedNode.Path;

        public RemoveNodeOperation(int layerIndex, DomNode parent, DomNode removedNode, string nameOrIndexAtTimeOfRemoval, int originalIndexInArray)
            : base(layerIndex)
        {
            _parent = parent;
            _removedNode = removedNode;
            _nameOrIndexAtTimeOfRemoval = nameOrIndexAtTimeOfRemoval;
            _originalIndexInArray = originalIndexInArray;
        }

        public override bool RequiresFullRefresh => true;

        public override void Redo(MainViewModel vm) => vm.RemoveNodeFromParent(_removedNode);

        public override void Undo(MainViewModel vm)
        {
            if (_parent is ArrayNode arrayParent)
            {
                arrayParent.InsertItem(_originalIndexInArray, _removedNode);
            }
            else if (_parent is ObjectNode objectParent)
            {
                objectParent.AddChild(_removedNode.Name, _removedNode);
            }
            vm.MapDomNodeToSchemaRecursive(_removedNode);
        }
    }
} 
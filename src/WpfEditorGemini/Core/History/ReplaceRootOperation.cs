using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.ViewModels;

namespace JsonConfigEditor.Core.History
{
    public class ReplaceRootOperation : EditOperation
    {
        private readonly DomNode? _oldRoot;
        private readonly DomNode _newRoot;

        public override string? NodePath => _newRoot.Path;

        public ReplaceRootOperation(int layerIndex, DomNode? oldRoot, DomNode newRoot)
            : base(layerIndex)
        {
            _oldRoot = oldRoot;
            _newRoot = newRoot;
        }

        public override bool RequiresFullRefresh => true;

        public override void Redo(MainViewModel vm)
        {
            vm.SetRootNode(_newRoot);
        }

        public override void Undo(MainViewModel vm)
        {
            vm.SetRootNode(_oldRoot);
        }
    }
} 
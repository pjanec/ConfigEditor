using ConfigEditor.Dom;

namespace ConfigEditor.ViewModel
{
    public class RefNodeViewModel : DomNodeViewModel
    {
        public new RefNode Node => (RefNode)base.Node;

        public DomNode? ResolvedTarget => Node.ResolvedTarget;
        public object? ResolvedPreviewValue => Node.ResolvedPreviewValue;

        public RefNodeViewModel(RefNode node, DomNodeViewModel? parent = null)
            : base(node, parent)
        {
        }

        public bool HasCycle { get; internal set; }
    }
}

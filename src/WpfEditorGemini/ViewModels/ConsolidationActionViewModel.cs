using JsonConfigEditor.Core.Services;

namespace JsonConfigEditor.ViewModels
{
    public class ConsolidationActionViewModel : ViewModelBase
    {
        public ConsolidationAction Action { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // MODIFICATION: Updated the description to include the layer name
        public string Description => $"In layer '{Action.LayerName}', merge '{Action.DescendantFile}' into '{Action.AncestorFile}'";

        public ConsolidationActionViewModel(ConsolidationAction action)
        {
            Action = action;
            IsSelected = true; // Default to being selected
        }
    }
} 
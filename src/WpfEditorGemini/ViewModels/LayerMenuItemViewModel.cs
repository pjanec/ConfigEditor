using System.Windows.Input;

namespace JsonConfigEditor.ViewModels
{
    /// <summary>
    /// Represents a single layer in the "Override Sources" context submenu.
    /// This ViewModel drives the display of each menu item, including its name,
    /// status markers, and the command to switch to that layer.
    /// </summary>
    public class LayerMenuItemViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;

        /// <summary>
        /// Gets the display name of the layer (e.g., "Base", "Production").
        /// </summary>
        public string LayerName { get; }

        /// <summary>
        /// Gets the index of the layer this menu item represents.
        /// </summary>
        public int LayerIndex { get; }

        /// <summary>
        /// Gets a value indicating whether the selected configuration property
        /// is defined in this layer. Used to show a status marker.
        /// </summary>
        public bool IsDefinedInThisLayer { get; }

        /// <summary>
        /// Gets a value indicating whether this layer provides the final,
        /// effective value for the selected property. Used for highlighting.
        /// </summary>
        public bool IsEffectiveInThisLayer { get; }

        /// <summary>
        /// Command to switch the editor's active layer to this one.
        /// </summary>
        public ICommand SwitchToLayerCommand { get; }

        public LayerMenuItemViewModel(
            string layerName,
            int layerIndex,
            bool isDefinedInThisLayer,
            bool isEffectiveInThisLayer,
            MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            LayerName = layerName;
            LayerIndex = layerIndex;
            IsDefinedInThisLayer = isDefinedInThisLayer;
            IsEffectiveInThisLayer = isEffectiveInThisLayer;

            SwitchToLayerCommand = new RelayCommand(SwitchToLayer);
        }

        private void SwitchToLayer()
        {
            // Delegate the logic to change the active layer to the MainViewModel.
            _mainViewModel.SetSelectedEditorLayerByIndex(LayerIndex);
        }
    }
}



using JsonConfigEditor.ViewModels;
using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
// Added for NodeValueTemplateSelector
using JsonConfigEditor.Views;

namespace JsonConfigEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();

            if (DataContext is MainViewModel vmInstance)
            {
                vmInstance.PropertyChanged += MainViewModel_PropertyChanged;
                 // Set UiRegistry for the NodeValueTemplateSelector
                if (this.Resources["NodeValueTemplateSelector"] is NodeValueTemplateSelector selector)
                {
                    selector.UiRegistry = vmInstance.UiRegistry;
                }
            }
            else if (this.Resources["NodeValueTemplateSelector"] is NodeValueTemplateSelector selector && DataContext is MainViewModel vm)
            { // Fallback for original logic, though DataContext should be vmInstance here
                selector.UiRegistry = vm.UiRegistry;
            }
            
            // Set up keyboard navigation
            SetupKeyboardNavigation();
            
            // Load schemas from current assembly on startup
            LoadDefaultSchemas();

#if DEBUG
            // Auto-load sample JSON for testing
            string sampleJsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_config.json");
            if (System.IO.File.Exists(sampleJsonPath))
            {
                // Fire and forget is okay for this auto-load, or await if startup sequence allows
                _ = ViewModel.LoadFileAsync(sampleJsonPath);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Sample JSON file not found: {sampleJsonPath}");
                // Optionally, create the file if it doesn't exist for the very first run:
                // string sampleJsonContent = "{\"hello\": \"world\"}"; // Replace with actual content or load from embedded resource
                // System.IO.File.WriteAllText(sampleJsonPath, sampleJsonContent);
                // _ = ViewModel.LoadFileAsync(sampleJsonPath); 
            }
#endif
        }

        /// <summary>
        /// Sets up keyboard navigation for the DataGrid.
        /// </summary>
        private void SetupKeyboardNavigation()
        {
            MainDataGrid.PreviewKeyDown += MainDataGrid_PreviewKeyDown;
            MainDataGrid.BeginningEdit += MainDataGrid_BeginningEdit;
            MainDataGrid.CellEditEnding += MainDataGrid_CellEditEnding;
        }

        /// <summary>
        /// Handles keyboard navigation in the DataGrid.
        /// </summary>
        private void MainDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var dataGrid = (DataGrid)sender;
            var selectedItem = dataGrid.SelectedItem as DataGridRowItemViewModel;

            if (selectedItem == null)
                return;

            switch (e.Key)
            {
                case Key.Enter:
                    System.Diagnostics.Debug.WriteLine("Enter pressed on item: " + selectedItem.NodeName + ", IsEditable: " + selectedItem.IsEditable + ", IsInEditMode (before): " + selectedItem.IsInEditMode);
                    if (selectedItem.IsInEditMode)
                    {
                        if (selectedItem.CommitEdit()) // ViewModel's commit logic
                        {
                           selectedItem.IsInEditMode = false; // Update ViewModel state first
                           dataGrid.CommitEdit(); // Tell DataGrid to finalize its edit state
                           System.Diagnostics.Debug.WriteLine("Edit committed, IsInEditMode: " + selectedItem.IsInEditMode);
                           e.Handled = true; // We've handled the commit
                        }
                        else
                        {
                            // Commit failed (e.g., validation error in CommitEdit)
                            // Keep edit mode active, DataGrid should show validation error if TextBox is set up for it.
                            // Don't handle 'e' so DataGrid might show its own error indication if any.
                            System.Diagnostics.Debug.WriteLine("CommitEdit failed. Keeping edit mode.");
                        }
                    }
                    else if (selectedItem.IsEditable)
                    {
                        selectedItem.IsInEditMode = true;
                        System.Diagnostics.Debug.WriteLine("IsInEditMode (after): " + selectedItem.IsInEditMode);
                        dataGrid.BeginEdit(e); 
                        e.Handled = true;
                    }
                    break;

                case Key.Escape:
                    System.Diagnostics.Debug.WriteLine("Escape pressed, IsInEditMode: " + selectedItem.IsInEditMode);
                    if (selectedItem.IsInEditMode)
                    {
                        selectedItem.CancelEdit(); // ViewModel cancels, resets value, sets IsInEditMode = false
                        dataGrid.CancelEdit(DataGridEditingUnit.Row); // Tell DataGrid to cancel its edit operation for the row
                        // dataGrid.CancelEdit(DataGridEditingUnit.Cell); // Alternative: if only cell edit needs cancelling
                        System.Diagnostics.Debug.WriteLine("CancelEdit called, IsInEditMode after: " + selectedItem.IsInEditMode);
                        e.Handled = true;
                    }
                    break;

                case Key.F2:
                    System.Diagnostics.Debug.WriteLine("F2 pressed, IsEditable: " + selectedItem.IsEditable + ", IsInEditMode (before): " + selectedItem.IsInEditMode);
                    if (!selectedItem.IsInEditMode && selectedItem.IsEditable)
                    {
                        selectedItem.IsInEditMode = true;
                        System.Diagnostics.Debug.WriteLine("F2: IsInEditMode set to true");
                        // Consistently call BeginEdit like in Enter key handler
                        // dataGrid.BeginEdit(e); // Pass original event args if needed by BeginEdit overload
                        dataGrid.BeginEdit();    // Or the parameterless overload if sufficient
                        System.Diagnostics.Debug.WriteLine("F2: dataGrid.BeginEdit() called");
                        e.Handled = true;
                    }
                    break;

                case Key.Space:
                    // Space key: Toggle expansion
                    if (selectedItem.IsExpandable && !selectedItem.IsInEditMode)
                    {
                        selectedItem.IsExpanded = !selectedItem.IsExpanded;
                        e.Handled = true;
                    }
                    break;

                case Key.Left:
                    // Left arrow: Collapse if expanded, otherwise move to parent
                    if (!selectedItem.IsInEditMode)
                    {
                        if (selectedItem.IsExpanded)
                        {
                            selectedItem.IsExpanded = false;
                            e.Handled = true;
                        }
                        else
                        {
                            // Move to parent node
                            MoveToParentNode(selectedItem);
                            e.Handled = true;
                        }
                    }
                    break;

                case Key.Right:
                    // Right arrow: Expand if collapsed and expandable
                    if (!selectedItem.IsInEditMode && selectedItem.IsExpandable && !selectedItem.IsExpanded)
                    {
                        selectedItem.IsExpanded = true;
                        e.Handled = true;
                    }
                    break;

                case Key.Delete:
                    // Delete key: Delete node (if supported)
                    if (!selectedItem.IsInEditMode && selectedItem.IsDomNodePresent)
                    {
                        // TODO: Implement node deletion
                        e.Handled = true;
                    }
                    break;
            }
        }

        /// <summary>
        /// Moves selection to the parent node of the current selection.
        /// </summary>
        private void MoveToParentNode(DataGridRowItemViewModel currentItem)
        {
            if (currentItem.DomNode?.Parent == null)
                return;

            // Find the parent item in the flat list
            foreach (var item in ViewModel.FlatItemsSource)
            {
                if (item.DomNode == currentItem.DomNode.Parent)
                {
                    MainDataGrid.SelectedItem = item;
                    MainDataGrid.ScrollIntoView(item);
                    break;
                }
            }
        }

        /// <summary>
        /// Handles the beginning of cell editing.
        /// </summary>
        private void MainDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var item = e.Row.Item as DataGridRowItemViewModel;
            if (item != null && !item.IsEditable)
            {
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Handles the end of cell editing.
        /// </summary>
        private void MainDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            var item = e.Row.Item as DataGridRowItemViewModel;
            if (item == null)
                return;

            if (e.EditAction == DataGridEditAction.Commit)
            {
                // Commit the edit
                if (!item.CommitEdit())
                {
                    e.Cancel = true; // Cancel if commit failed
                }
                else
                {
                    item.IsInEditMode = false;
                }
            }
            else
            {
                // Cancel the edit
                item.CancelEdit();
            }
        }

        // Handler for double-click on a value cell to start editing
        private void ValueCell_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridCell cell && cell.DataContext is DataGridRowItemViewModel vm)
            {
                // Ensure we are on the actual DataGrid, not a header or other element.
                var dataGrid = FindVisualParent<DataGrid>(cell);
                if (dataGrid == null) return;

                if (vm.IsEditable && !vm.IsInEditMode)
                {
                    vm.IsInEditMode = true; 

                    // Ensure the cell/row is selected and focused before calling BeginEdit
                    dataGrid.SelectedItem = vm;
                    dataGrid.CurrentCell = new DataGridCellInfo(cell); // Use the clicked cell directly
                    
                    // It's often better to let the DataGrid handle bringing the cell into edit mode if possible
                    // after its properties are set. BeginEdit() might be called implicitly by focusing a cell in edit mode.
                    // Or call it explicitly.
                    dataGrid.BeginEdit(); 
                    e.Handled = true;
                }
            }
        }

        // Helper to find visual parent (add this if not already present)
        public static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }

        // Handler for double-click on a name cell to toggle expansion
        private void NameCell_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridCell cell && cell.DataContext is DataGridRowItemViewModel vm)
            {
                if (vm.IsExpandable)
                {
                    vm.IsExpanded = !vm.IsExpanded;
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Loads default schemas from the current assembly.
        /// </summary>
        private async void LoadDefaultSchemas()
        {
            try
            {
                var currentAssemblyPath = Assembly.GetExecutingAssembly().Location;
                await ViewModel.LoadSchemasAsync(new[] { currentAssemblyPath });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load default schemas: {ex.Message}", "Schema Loading Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Handles the window closing event.
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (ViewModel.IsDirty)
            {
                var result = MessageBox.Show("You have unsaved changes. Do you want to save before closing?", 
                    "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                switch (result)
                {
                    case MessageBoxResult.Yes:
                        try
                        {
                            if (string.IsNullOrEmpty(ViewModel.CurrentFilePath))
                            {
                                var saveDialog = new SaveFileDialog
                                {
                                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                                    DefaultExt = "json"
                                };

                                if (saveDialog.ShowDialog() == true)
                                {
                                    ViewModel.SaveFileAsync(saveDialog.FileName).Wait();
                                }
                                else
                                {
                                    e.Cancel = true;
                                    return;
                                }
                            }
                            else
                            {
                                ViewModel.SaveFileAsync().Wait();
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to save file: {ex.Message}", "Save Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            e.Cancel = true;
                            return;
                        }
                        break;

                    case MessageBoxResult.Cancel:
                        e.Cancel = true;
                        return;
                }
            }

            base.OnClosing(e);
        }

        private void MainViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedGridItem))
            {
                if (ViewModel.SelectedGridItem != null)
                {
                    MainDataGrid.ScrollIntoView(ViewModel.SelectedGridItem);
                    // Attempt to focus the row. Finding the DataGridRow container can be tricky.
                    var row = MainDataGrid.ItemContainerGenerator.ContainerFromItem(ViewModel.SelectedGridItem) as DataGridRow;
                    if (row != null)
                    {
                        row.Focus();
                    }
                    else
                    {
                        // If container is not generated yet, try focusing after layout update
                        MainDataGrid.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var rowAfterLayout = MainDataGrid.ItemContainerGenerator.ContainerFromItem(ViewModel.SelectedGridItem) as DataGridRow;
                            rowAfterLayout?.Focus();
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }
            }
        }
    }
} 
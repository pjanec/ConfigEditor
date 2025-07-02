using JsonConfigEditor.ViewModels;
using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
// Added for NodeValueTemplateSelector
using JsonConfigEditor.Views;
using System.Linq;

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
                        // Ensure the current cell is the one we intend to edit.
                        // This usually means the 'Value' column for the selectedItem.
                        // Find the actual DataGridCell visual element for the target column.
                        DataGridRow? row = dataGrid.ItemContainerGenerator.ContainerFromItem(selectedItem) as DataGridRow;
                        DataGridCell? cellToEdit = null;
                        DataGridColumn? valueColumn = dataGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Value");

                        if (row != null && valueColumn != null)
                        {
                            // Try to get the cell directly
                            var cellContent = valueColumn.GetCellContent(row);
                            if (cellContent != null)
                            {
                                cellToEdit = cellContent.Parent as DataGridCell;
                            }
                        }
                        
                        if (cellToEdit != null)
                        {
                            dataGrid.CurrentCell = new DataGridCellInfo(cellToEdit); // Set CurrentCell using DataGridCell
                            // cellToEdit.Focus(); // Focusing the cell before BeginEdit can sometimes help
                        }
                        else if (valueColumn != null) // Fallback if visual cell not found immediately
                        {
                            dataGrid.CurrentCell = new DataGridCellInfo(selectedItem, valueColumn); 
                        }

                        selectedItem.IsInEditMode = true;
                        System.Diagnostics.Debug.WriteLine("IsInEditMode (after): " + selectedItem.IsInEditMode);
                        bool editStarted = dataGrid.BeginEdit(e); 
                        System.Diagnostics.Debug.WriteLine($"dataGrid.BeginEdit() returned: {editStarted}");

                        // Attempt to focus the editor after BeginEdit has been called
                        // This needs to happen after the visual tree is updated with the editing element.
                        // We dispatch it to a lower priority to allow the DataGrid to set up the editor.
                        dataGrid.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var cellContent = dataGrid.CurrentCell.Column.GetCellContent(dataGrid.CurrentCell.Item);
                            if (cellContent is ContentPresenter presenter) // Editing templates often use ContentPresenter
                            {
                                var editingElement = FindVisualChild<UIElement>(presenter);
                                editingElement?.Focus();
                                if (editingElement is CheckBox cb) // Special handling for checkbox spacebar toggle
                                {
                                     // Focus might be enough, spacebar should toggle if focused.
                                }
                            }
                            else if (cellContent is UIElement uiElement) // Direct UIElement in cell
                            {
                                uiElement.Focus();
                            }
                        }), System.Windows.Threading.DispatcherPriority.Background);
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
            if (item == null || !item.IsInEditMode)
            {
                return;
            }

            if (e.EditAction == DataGridEditAction.Commit)
            {
                // Commit the data in the ViewModel. This method now also sets
                // item.IsInEditMode = false upon its own success.
                if (item.CommitEdit())
                {
                    // The data is committed. To force the UI to update, we post an action
                    // to the dispatcher to move focus away from the cell's content.
                    // This reliably forces the DataGrid to exit the cell's editing state.
                    var dataGrid = (DataGrid)sender;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        dataGrid.Focus();
                        var rowContainer = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromItem(item);
                        rowContainer?.Focus();

                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
                else
                {
                    // The ViewModel's commit logic failed (e.g., validation).
                    // Explicitly cancel the DataGrid's commit action to keep the editor open.
                    e.Cancel = true;
                }
            }
            else // EditAction is Cancel
            {
                item.CancelEdit();
            }
        }

        // Handler for double-click on a value cell to start editing
        private void ValueCell_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("ValueCell_MouseDoubleClick called.");
            if (sender is DataGridCell cell && cell.DataContext is DataGridRowItemViewModel vm)
            {
                System.Diagnostics.Debug.WriteLine($"ValueCell: Item '{vm.NodeName}', IsEditable: {vm.IsEditable}, IsInEditMode: {vm.IsInEditMode}");
                // Ensure we are on the actual DataGrid, not a header or other element.
                var dataGrid = FindVisualParent<DataGrid>(cell);
                if (dataGrid == null) 
                {
                    System.Diagnostics.Debug.WriteLine("ValueCell: DataGrid parent not found.");
                    return;
                }

                if (vm.IsEditable && !vm.IsInEditMode)
                {
                    vm.IsInEditMode = true; 
                    System.Diagnostics.Debug.WriteLine($"ValueCell: Set IsInEditMode to true for '{vm.NodeName}'.");

                    // Ensure the cell/row is selected and focused before calling BeginEdit
                    dataGrid.SelectedItem = vm;
                    // It's important that the CurrentCell is set to the cell that was double-clicked.
                    // This column is the 'Value' column.
                    DataGridColumn valueColumn = dataGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Value");
                    if (valueColumn != null) 
                    {
                         dataGrid.CurrentCell = new DataGridCellInfo(vm, valueColumn);
                    }
                    else
                    {
                        // Fallback if column not found by header, use the clicked cell's column.
                        // This might be more robust if header names change.
                        dataGrid.CurrentCell = new DataGridCellInfo(cell); 
                    }
                    
                    dataGrid.BeginEdit(); 
                    System.Diagnostics.Debug.WriteLine($"ValueCell: Called BeginEdit() for '{vm.NodeName}'.");
                    e.Handled = true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ValueCell: Edit condition not met for '{vm.NodeName}'. IsEditable: {vm.IsEditable}, IsInEditMode: {vm.IsInEditMode}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ValueCell_MouseDoubleClick: Sender is not DataGridCell or DataContext is not DataGridRowItemViewModel.");
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

        // Helper to find visual child (add this)
        public static T? FindVisualChild<T>(DependencyObject parent) where T : UIElement
        {
            if (parent == null) return null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T tChild)
                    return tChild;
                
                T? childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
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
                var selectedVm = ViewModel.SelectedGridItem;
                if (selectedVm != null)
                {
                    // Ensure the VM is not in edit mode if we are just changing selection
                    if (selectedVm.IsInEditMode)
                    {
                        // This case should ideally not happen if selection change is programmatic
                        // and not part of an edit commit/cancel.
                        // For now, we'll assume that if SelectedGridItem changes, we are not in an active edit
                        // or the edit has just concluded.
                        // selectedVm.IsInEditMode = false; // Consider if this is safe or needed
                    }

                    MainDataGrid.ScrollIntoView(selectedVm);
                    
                    Action focusAction = () => 
                    {
                        if (MainDataGrid.SelectedItem != selectedVm)
                        {
                            System.Diagnostics.Debug.WriteLine($"FocusAction: SelectedItem changed from '{selectedVm?.NodeName}' before action could run. Aborting focus attempt.");
                            return;
                        }

                        var row = MainDataGrid.ItemContainerGenerator.ContainerFromItem(selectedVm) as DataGridRow;
                        if (row == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"FocusAction: Row for '{selectedVm.NodeName}' not found initially. Updating layout and scrolling.");
                            MainDataGrid.UpdateLayout(); 
                            MainDataGrid.ScrollIntoView(selectedVm); 
                            row = MainDataGrid.ItemContainerGenerator.ContainerFromItem(selectedVm) as DataGridRow;
                        }

                        if (row != null)
                        {
                            DataGridColumn? nameColumn = MainDataGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Name");
                            if (nameColumn != null)
                            {
                                DataGridCell? cell = GetCell(row, nameColumn); // Use the new helper
                                if (cell != null)
                                {
                                    MainDataGrid.CurrentCell = new DataGridCellInfo(cell); // Update CurrentCell
                                    System.Diagnostics.Debug.WriteLine($"FocusAction: Set CurrentCell to actual cell for '{selectedVm.NodeName}'.");
                                    if (!cell.IsKeyboardFocusWithin)
                                    {
                                        bool success = cell.Focus(); // Focus the cell
                                        System.Diagnostics.Debug.WriteLine($"FocusAction: cell.Focus() for '{selectedVm.NodeName}' " + (success ? "succeeded." : "failed."));
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"FocusAction: Cell for '{selectedVm.NodeName}' already had keyboard focus within.");
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"FocusAction: DataGridCell for 'Name' column not found for '{selectedVm.NodeName}'. Falling back to row focus and item/column CurrentCell.");
                                    MainDataGrid.CurrentCell = new DataGridCellInfo(selectedVm, nameColumn); // Set CurrentCell using item and column
                                    if (!row.IsKeyboardFocusWithin)
                                    {
                                        bool success = row.Focus(); // Fallback to row focus
                                        System.Diagnostics.Debug.WriteLine($"FocusAction: Fallback row.Focus() for '{selectedVm.NodeName}' " + (success ? "succeeded." : "failed."));
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"FocusAction: Fallback - Row for '{selectedVm.NodeName}' already had keyboard focus within.");
                                    }
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"FocusAction: 'Name' column not found. Focusing row for '{selectedVm.NodeName}'.");
                                if (!row.IsKeyboardFocusWithin)
                                {
                                    bool success = row.Focus();
                                     System.Diagnostics.Debug.WriteLine($"FocusAction: Name column not found - row.Focus() for '{selectedVm.NodeName}' " + (success ? "succeeded." : "failed."));
                                }
                                else
                                {
                                     System.Diagnostics.Debug.WriteLine($"FocusAction: Name column not found - Row for '{selectedVm.NodeName}' already had keyboard focus within.");
                                }
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"FocusAction: Row for '{selectedVm.NodeName}' still not found after layout/scroll. Cannot set focus.");
                        }
                    };

                    if (!MainDataGrid.IsKeyboardFocusWithin)
                    {
                        System.Diagnostics.Debug.WriteLine($"FocusAction: MainDataGrid does not have focus. Focusing DataGrid first for '{selectedVm?.NodeName}'.");
                        MainDataGrid.Focus(); 
                        MainDataGrid.Dispatcher.BeginInvoke(focusAction, System.Windows.Threading.DispatcherPriority.DataBind);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"FocusAction: MainDataGrid ALREADY has focus. Dispatching for '{selectedVm?.NodeName}'.");
                        MainDataGrid.Dispatcher.BeginInvoke(focusAction, System.Windows.Threading.DispatcherPriority.DataBind);
                    }
                }
            }
        }

        private void ExpandToggleButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("ExpandToggleButton_Click FIRED!");
            if (sender is ToggleButton tb && tb.DataContext is DataGridRowItemViewModel vm)
            {
                System.Diagnostics.Debug.WriteLine($"ToggleButton Clicked for VM: {vm.NodeName}, Current VM.IsExpanded (before manual set): {vm.IsExpanded}, ToggleButton IsChecked: {tb.IsChecked}");

                // Manually ensure the ViewModel's IsExpanded property reflects the ToggleButton's state.
                // The IsExpanded setter on the VM will handle calling OnExpansionChanged.
                // tb.IsChecked is bool? (nullable), vm.IsExpanded is bool.
                bool newExpansionState = tb.IsChecked ?? false; // Default to false if tb.IsChecked is null (shouldn't happen here)
                
                if (vm.IsExpanded != newExpansionState)
                {
                    vm.IsExpanded = newExpansionState; 
                }
                System.Diagnostics.Debug.WriteLine($"VM.IsExpanded (after manual set): {vm.IsExpanded}");
            }
        }

        // Helper function to get a specific cell from a row and column
        public static DataGridCell? GetCell(DataGridRow row, DataGridColumn column)
        {
            if (row == null || column == null) return null;

            // Get the FrameworkElement that is the content of the cell.
            var cellContent = column.GetCellContent(row);
            if (cellContent == null)
            {
                // Cell content might not be generated yet. Try to force it.
                // This can happen if the row is virtualized and not fully realized.
                // Applying template to the row might help ensure its parts are created.
                row.ApplyTemplate(); 
                cellContent = column.GetCellContent(row);
                if (cellContent == null) return null;
            }

            // The DataGridCell is the parent of the cell's content.
            DependencyObject? parent = System.Windows.Media.VisualTreeHelper.GetParent(cellContent);
            while (parent != null && !(parent is DataGridCell))
            {
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return parent as DataGridCell;
        }
    }
} 
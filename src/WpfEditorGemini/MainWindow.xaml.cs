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

        private System.Threading.Timer? _saveSettingsDebounceTimer;

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
            
            // *** NEW: Apply loaded settings ***
            this.Height = ViewModel.UserSettings.Window.Height;
            this.Width = ViewModel.UserSettings.Window.Width;
            ApplyColumnWidths();

            // Hook into the SizeChanged event to trigger debounced saving
            this.SizeChanged += OnWindowSizeChanged;
            
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
                        // This part for COMMITTING the edit is fine, leave it as is.
                        if (selectedItem.CommitEdit())
                        {
                           selectedItem.IsInEditMode = false;
                           dataGrid.CommitEdit();
                           System.Diagnostics.Debug.WriteLine("Edit committed, IsInEditMode: " + selectedItem.IsInEditMode);
                           e.Handled = true;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("CommitEdit failed. Keeping edit mode.");
                        }
                    }
                    else if (selectedItem.IsEditable)
                    {
                        // Replace the old, complex logic with a single call
                        // to our new shared method.
                        StartEditingCell(selectedItem);
                        e.Handled = true;
                    }
                    break;
                case Key.Escape:
                    System.Diagnostics.Debug.WriteLine("Escape pressed, IsInEditMode: " + selectedItem.IsInEditMode);
                    if (selectedItem.IsInEditMode)
                    {
                        selectedItem.CancelEdit();
                        // ViewModel cancels, resets value, sets IsInEditMode = false
                        dataGrid.CancelEdit(DataGridEditingUnit.Row);
                        // Tell DataGrid to cancel its edit operation for the row
                        // dataGrid.CancelEdit(DataGridEditingUnit.Cell);
                        // Alternative: if only cell edit needs cancelling
                        System.Diagnostics.Debug.WriteLine("CancelEdit called, IsInEditMode after: " + selectedItem.IsInEditMode);
                        e.Handled = true;
                    }
                    break;
                case Key.F2:
                    System.Diagnostics.Debug.WriteLine("F2 pressed, IsEditable: " + selectedItem.IsEditable + ", IsInEditMode (before): " + selectedItem.IsInEditMode);
                    if (!selectedItem.IsInEditMode && selectedItem.IsEditable)
                    {
                        StartEditingCell(selectedItem); // Use the shared method for consistency.
                        e.Handled = true;
                    }
                    break;
                case Key.Up:
                case Key.Down:
                    // NEW: When in edit mode for an enum, cycle through the allowed values
                    if (selectedItem.IsInEditMode && selectedItem.IsEnumBased)
                    {
                        var allowedValues = selectedItem.AllowedValues;
                        string currentValue = selectedItem.EditValue;

                        int currentIndex = -1;
                        for (int i = 0; i < allowedValues.Count; i++)
                        {
                            if (string.Equals(allowedValues[i], currentValue, StringComparison.OrdinalIgnoreCase))
                            {
                                currentIndex = i;
                                break;
                            }
                        }

                        if (currentIndex == -1) // If current value not found, default to the first item.
                        {
                            currentIndex = 0;
                        }
                        else // Cycle through the list.
                        {
                            if (e.Key == Key.Down)
                            {
                                currentIndex = (currentIndex + 1) % allowedValues.Count;
                            }
                            else // Key.Up
                            {
                                currentIndex = (currentIndex - 1 + allowedValues.Count) % allowedValues.Count;
                            }
                        }
                
                        selectedItem.EditValue = allowedValues[currentIndex];
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
                    if (!selectedItem.IsInEditMode && selectedItem.IsDomNodePresent)
                    {
                        // Get the command and parameter from the ViewModel
                        var command = ViewModel.DeleteSelectedNodesCommand;
                        var parameter = selectedItem;

                        // Check if the command can be executed for the selected item
                        if (command.CanExecute(parameter))
                        {
                            // Execute the command
                            command.Execute(parameter);
                        }

                        // Mark the event as handled to prevent any other controls from processing it.
                        e.Handled = true;
                    }
                    break;
                case Key.Insert:
                    if (Keyboard.Modifiers == ModifierKeys.Alt)
                    {
                        // Alt+Insert for adding a child
                        if (ViewModel.AddNewChildNodeCommand.CanExecute(selectedItem))
                        {
                            ViewModel.AddNewChildNodeCommand.Execute(selectedItem);
                            e.Handled = true;
                        }
                    }
                    else
                    {
                        // Regular Insert for adding a sibling
                        if (ViewModel.AddNewSiblingNodeCommand.CanExecute(selectedItem))
                        {
                            ViewModel.AddNewSiblingNodeCommand.Execute(selectedItem);
                            e.Handled = true;
                        }
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
            System.Diagnostics.Debug.WriteLine($"--- MainDataGrid_BeginningEdit Fired for '{(item?.NodeName ?? "null")}' ---");
            if (item != null && !item.IsEditable)
            {
                System.Diagnostics.Debug.WriteLine("-> Canceling edit because item is not editable.");
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

        private void StartEditingCell(DataGridRowItemViewModel? item)
        {
            if (item == null || !item.IsEditable || item.IsInEditMode)
            {
                return;
            }

            // This logic is adapted from your working Enter key handler
            var dataGrid = MainDataGrid; 
            // Use the named DataGrid
    
            // Ensure the container for the row is generated, scrolling if necessary.
            var row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromItem(item);
            if (row == null)
            {
                dataGrid.ScrollIntoView(item);
                row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromItem(item);
                if (row == null) return;
            }

            DataGridColumn? valueColumn = dataGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Value");
            if (valueColumn == null) return;
            // Set the current cell to prepare for editing
            dataGrid.CurrentCell = new DataGridCellInfo(item, valueColumn);
            // Set the ViewModel state and tell the DataGrid to show the editing template
            item.IsInEditMode = true;
            dataGrid.BeginEdit();

            // If the cell being edited is an enum/combo box, schedule an action
            // to open the dropdown after the editing template is loaded.
            if (item.IsEnumBased)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var editingRow = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromItem(item);
                    if (editingRow == null) return;

                    var cell = GetCell(editingRow, valueColumn);
                    if (cell == null) return;

                    // Find the ComboBox within the cell's editing template.
                    var comboBox = FindVisualChild<ComboBox>(cell);
                    if (comboBox != null)
                    {
                        // Give the ComboBox focus and open its dropdown.
                        comboBox.Focus();
                        comboBox.IsDropDownOpen = true;
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void ValueCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // We only care about the second click of a double-click.
            if (e.ClickCount == 2)
            {
                if (sender is DataGridCell cell && cell.DataContext is DataGridRowItemViewModel vm)
                {
                    // Call the same, reliable logic as the Enter key.
                    StartEditingCell(vm);
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

        private void DataGridRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && !row.IsSelected)
            {
                row.IsSelected = true;
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

            SaveLayoutSettings(); // Ensure settings are saved on close
            base.OnClosing(e);
        }

        private void MainViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedGridItem))
            {
                System.Diagnostics.Debug.WriteLine("--- PropertyChanged for SelectedGridItem Fired ---");
                var selectedVm = ViewModel.SelectedGridItem;
                if (selectedVm != null)
                {
                    System.Diagnostics.Debug.WriteLine($"-> New SelectedGridItem is '{selectedVm.NodeName}'. Queuing focus action.");
                    MainDataGrid.ScrollIntoView(selectedVm);
                    Action focusAction = () => 
                    {
                        System.Diagnostics.Debug.WriteLine($">>> Dispatcher 'focusAction' starting for '{selectedVm.NodeName}'");
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
                        System.Diagnostics.Debug.WriteLine($">>> Dispatcher 'focusAction' finished for '{selectedVm.NodeName}'");
                    };
                    MainDataGrid.Dispatcher.BeginInvoke(focusAction, System.Windows.Threading.DispatcherPriority.Loaded);
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

        private void ApplyColumnWidths()
        {
            foreach (var column in MainDataGrid.Columns)
            {
                if (column.Header is string header &&
                    ViewModel.UserSettings.DataGrid.ColumnWidths.TryGetValue(header, out var width))
                {
                    column.Width = new DataGridLength(width);
                }
            }
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Dispose the old timer to reset the delay
            _saveSettingsDebounceTimer?.Dispose();
            // Create a new timer that will fire once after 2 seconds
            _saveSettingsDebounceTimer = new System.Threading.Timer(
                callback: _ => Dispatcher.Invoke(SaveLayoutSettings),
                state: null,
                dueTime: 2000,
                period: System.Threading.Timeout.Infinite
            );
        }

        private void SaveLayoutSettings()
        {
            // Update the settings model with the current layout
            ViewModel.UserSettings.Window.Height = this.ActualHeight;
            ViewModel.UserSettings.Window.Width = this.ActualWidth;

            ViewModel.UserSettings.DataGrid.ColumnWidths.Clear();
            foreach (var column in MainDataGrid.Columns)
            {
                if (column.Header is string header)
                {
                    // Store the actual pixel width, which is robust
                    ViewModel.UserSettings.DataGrid.ColumnWidths[header] = column.ActualWidth;
                }
            }

            // Tell the service to save the updated model to disk
            _ = ViewModel.SaveCurrentUserSettings(); // This new method needs to be added to MainViewModel
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

        // This is a new helper method to be added to MainWindow.xaml.cs
        private DataGridCell? GetCellForViewModel(DataGrid dataGrid, object viewModel, string columnName)
        {
            var row = dataGrid.ItemContainerGenerator.ContainerFromItem(viewModel) as DataGridRow;
            if (row == null)
            {
                dataGrid.ScrollIntoView(viewModel);
                row = dataGrid.ItemContainerGenerator.ContainerFromItem(viewModel) as DataGridRow;
            }
            if (row == null) return null;

            var column = dataGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == columnName);
            if (column == null) return null;

            return GetCell(row, column);
        }


    }
} 

using JsonConfigEditor.ViewModels;
using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            
            // Set up keyboard navigation
            SetupKeyboardNavigation();
            
            // Load schemas from current assembly on startup
            LoadDefaultSchemas();
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
                    // Enter key: Start editing or commit edit
                    if (selectedItem.IsInEditMode)
                    {
                        if (selectedItem.CommitEdit())
                        {
                            selectedItem.IsInEditMode = false;
                            e.Handled = true;
                        }
                    }
                    else if (selectedItem.IsEditable)
                    {
                        selectedItem.IsInEditMode = true;
                        e.Handled = true;
                    }
                    break;

                case Key.Escape:
                    // Escape key: Cancel edit
                    if (selectedItem.IsInEditMode)
                    {
                        selectedItem.CancelEdit();
                        e.Handled = true;
                    }
                    break;

                case Key.F2:
                    // F2 key: Start editing
                    if (!selectedItem.IsInEditMode && selectedItem.IsEditable)
                    {
                        selectedItem.IsInEditMode = true;
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
    }
} 
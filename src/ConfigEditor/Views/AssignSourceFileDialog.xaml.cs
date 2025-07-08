// Views/AssignSourceFileDialog.xaml.cs

using JsonConfigEditor.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JsonConfigEditor.Views
{
    public partial class AssignSourceFileDialog : Window
    {
        public AssignSourceFileDialog()
        {
            InitializeComponent();
            // This event handler will run once the ListBox is loaded and ready.
            SuggestionsListBox.Loaded += SuggestionsListBox_Loaded;
        }

        private void SuggestionsListBox_Loaded(object sender, RoutedEventArgs e)
        {
            // Check if there are any items in the list.
            if (SuggestionsListBox.Items.Count > 0)
            {
                // Get the UI container for the first item in the list.
                var firstItemContainer = SuggestionsListBox.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                if (firstItemContainer != null)
                {
                    // Programmatically set keyboard focus to the first item.
                    Keyboard.Focus(firstItemContainer);
                }
            }
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AssignSourceFileViewModel vm && sender is ListBoxItem item)
            {
                // Set the selected item in the ViewModel
                vm.SelectedFile = item.Content as string;
                
                // Execute the existing confirm command
                if (vm.ConfirmCommand.CanExecute(null))
                {
                    vm.ConfirmCommand.Execute(null);
                }
            }
        }
    }
}

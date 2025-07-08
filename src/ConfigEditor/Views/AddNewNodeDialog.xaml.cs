using System.Windows;

namespace JsonConfigEditor.Views
{
    public partial class AddNewNodeDialog : Window
    {
        public AddNewNodeDialog()
        {
            InitializeComponent();
            // Focus the first text box when the dialog loads
            Loaded += (sender, e) => NameTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // The commit logic is in the ViewModel's command.
            // We only close the dialog if the command can execute successfully.
            if (DataContext is ViewModels.AddNewNodeViewModel vm && vm.CommitCommand.CanExecute(null))
            {
                DialogResult = true;
            }
        }
    }
} 
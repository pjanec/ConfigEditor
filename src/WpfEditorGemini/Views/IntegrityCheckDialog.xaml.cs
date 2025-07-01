using JsonConfigEditor.Core.Services;
using JsonConfigEditor.ViewModels;
using System.Windows;

namespace JsonConfigEditor.Views
{
    /// <summary>
    /// Interaction logic for IntegrityCheckDialog.xaml
    /// </summary>
    public partial class IntegrityCheckDialog : Window
    {
        public IntegrityCheckDialog(MainViewModel mainViewModel, IntegrityCheckType initialSelection)
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            
            // The action to close this window is passed into the ViewModel.
            // This allows the ViewModel to close the dialog after its command is executed,
            // without having a direct reference to the View.
            var closeAction = new System.Action(() => this.Close());

            DataContext = new IntegrityCheckViewModel(mainViewModel, initialSelection, closeAction);
        }
    }
}



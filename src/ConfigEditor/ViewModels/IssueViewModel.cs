using JsonConfigEditor.Core.Services;
using JsonConfigEditor.Core.Validation; // Add this using
using System.Windows.Input;

namespace JsonConfigEditor.ViewModels
{
    /// <summary>
    /// Represents a single item (an error, warning, or informational message)
    /// to be displayed in the Issues panel.
    /// </summary>
    public class IssueViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;
        private readonly IntegrityIssue _issue;

        /// <summary>
        /// The severity level of the issue.
        /// </summary>
        public ValidationSeverity Severity => _issue.Severity;

        /// <summary>
        /// The descriptive message detailing the issue.
        /// </summary>
        public string Message => _issue.Message;

        /// <summary>
        /// The name of the layer where the issue was found.
        /// </summary>
        public string LayerName => _issue.LayerName;

        /// <summary>
        /// The DOM path to the problematic node, if applicable.
        /// </summary>
        public string? DomPath => _issue.DomPath;

        /// <summary>
        /// The source file associated with the issue, if applicable.
        /// </summary>
        public string? FilePath => _issue.FilePath;

        /// <summary>
        /// Command executed when the user double-clicks this issue,
        /// navigating the editor to the source of the problem.
        /// </summary>
        public ICommand NavigateCommand { get; }

        public IssueViewModel(IntegrityIssue issue, MainViewModel mainViewModel)
        {
            _issue = issue;
            _mainViewModel = mainViewModel;
            NavigateCommand = new RelayCommand(NavigateToIssue, CanNavigateToIssue);
        }

        private bool CanNavigateToIssue()
        {
            // Navigation is possible if there is a DOM path to select.
            return !string.IsNullOrEmpty(DomPath);
        }

        private void NavigateToIssue()
        {
            // Delegate the navigation logic to the MainViewModel.
            _mainViewModel.NavigateToIssue(this);
        }
    }
}



using System.Windows;

namespace WpfUI.Windows
{
    /// <summary>
    /// Dialog window that hosts modal editors for complex node editing.
    /// Provides OK/Cancel buttons and manages the editor's lifecycle.
    /// </summary>
    public partial class ModalEditorWindow : Window
    {
        public bool? Result { get; private set; }

        public ModalEditorWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Sets the editor's content to be displayed in the dialog.
        /// The content should be a FrameworkElement created by an INodeValueEditor.
        /// </summary>
        public void SetContent(FrameworkElement content)
        {
            EditorContent.Content = content;
        }

        /// <summary>
        /// Handles the OK button click by setting the result to true and closing the dialog.
        /// This triggers the editor's ConfirmEdit method.
        /// </summary>
        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            Result = true;
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Handles the Cancel button click by setting the result to false and closing the dialog.
        /// This triggers the editor's CancelEdit method.
        /// </summary>
        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
            Close();
        }
    }
} 
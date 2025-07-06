using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows;

namespace JsonConfigEditor.ViewModels
{
    public class AssignSourceFileViewModel : ViewModelBase
    {
        private readonly Action<string> _onPathSelected;
        private readonly Func<string, string?> _openPathBuilder;
        private string? _selectedFile;

        public AssignSourceFileViewModel(List<string> suggestions, Action<string> onPathSelected, Func<string, string?> openPathBuilder, string nodePath)
        {
            _onPathSelected = onPathSelected;
            _openPathBuilder = openPathBuilder;
            NodePath = nodePath;
            
            SuggestedFiles = new ObservableCollection<string>(suggestions);
            if (SuggestedFiles.Any())
            {
                SelectedFile = SuggestedFiles.First();
            }

            ConfirmCommand = new RelayCommand(ExecuteConfirm, CanExecuteConfirm);
            CancelCommand = new RelayCommand(ExecuteCancel);
            OpenPathBuilderCommand = new RelayCommand(ExecuteOpenPathBuilder);
        }

        public string NodePath { get; }
        public ObservableCollection<string> SuggestedFiles { get; }

        public string? SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (SetProperty(ref _selectedFile, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand OpenPathBuilderCommand { get; }

        private void ExecuteConfirm()
        {
            if (SelectedFile != null)
            {
                _onPathSelected(SelectedFile);
                CloseDialog(true);
            }
        }

        private bool CanExecuteConfirm()
        {
            return !string.IsNullOrEmpty(SelectedFile);
        }

        private void ExecuteCancel()
        {
            CloseDialog(false);
        }

        private void ExecuteOpenPathBuilder()
        {
            var customPath = _openPathBuilder(NodePath);
            if (!string.IsNullOrEmpty(customPath))
            {
                if (!SuggestedFiles.Contains(customPath))
                {
                    SuggestedFiles.Add(customPath);
                }
                SelectedFile = customPath;
            }
        }

        private void CloseDialog(bool? result)
        {
            // Find the dialog window and close it
            if (Application.Current?.MainWindow != null)
            {
                var dialog = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.DataContext == this);
                if (dialog != null)
                {
                    dialog.DialogResult = result;
                    dialog.Close();
                }
            }
        }
    }
} 
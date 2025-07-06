using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace JsonConfigEditor.ViewModels
{
    public class AssignSourceFileViewModel : ViewModelBase
    {
        private readonly Action<string> _onPathSelected;
        private string? _selectedFile;
        private string _alternativeFileName = string.Empty;
        private string _finalPathPreview = string.Empty;

        public AssignSourceFileViewModel(List<string> suggestions, Action<string> onPathSelected, string nodePath, string activeLayerName)
        {
            _onPathSelected = onPathSelected;
            NodePath = nodePath;
            ActiveLayerName = activeLayerName;
              
            Explanation = $"This node needs to be housed in a file. No suitable file exists in the active '{ActiveLayerName}' layer.";

            SuggestedFiles = new ObservableCollection<string>(suggestions);
            if (SuggestedFiles.Any())
            {
                SelectedFile = SuggestedFiles.First();
            }

            ConfirmCommand = new RelayCommand(ExecuteConfirm, CanExecuteConfirm);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        public string NodePath { get; }
        public string ActiveLayerName { get; }
        public string Explanation { get; }
        public ObservableCollection<string> SuggestedFiles { get; }

        public string? SelectedFile
        {
            get => _selectedFile;
            set 
            {
                if (SetProperty(ref _selectedFile, value))
                {
                    UpdateFinalPathPreview();
                }
            }
        }

        public string AlternativeFileName
        {
            get => _alternativeFileName;
            set
            {
                if (SetProperty(ref _alternativeFileName, value))
                {
                    UpdateFinalPathPreview();
                }
            }
        }

        public string FinalPathPreview
        {
            get => _finalPathPreview;
            private set => SetProperty(ref _finalPathPreview, value);
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        private void UpdateFinalPathPreview()
        {
            string finalPath;
            var selectedSuggestion = SelectedFile?.Split(' ')[0] ?? "";
              
            if (!string.IsNullOrWhiteSpace(AlternativeFileName))
            {
                var directory = Path.GetDirectoryName(selectedSuggestion)?.Replace('\\', '/');
                finalPath = string.IsNullOrEmpty(directory) 
                    ? AlternativeFileName 
                    : $"{directory}/{AlternativeFileName}";
            }
            else
            {
                finalPath = selectedSuggestion;
            }
            FinalPathPreview = finalPath;
            // This ensures the OK button's state is re-evaluated after every change.
            CommandManager.InvalidateRequerySuggested();
        }

        private void ExecuteConfirm()
        {
            _onPathSelected(FinalPathPreview);
            CloseDialog(true);
        }

        private bool CanExecuteConfirm()
        {
            if (!string.IsNullOrWhiteSpace(AlternativeFileName))
            {
                // Validate the alternative name: no slashes and must end with .json
                return !AlternativeFileName.Contains('/') && 
                       !AlternativeFileName.Contains('\\') &&  
                       AlternativeFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
            }
            // If alternative is empty, it's valid as long as a suggestion is selected
            return !string.IsNullOrWhiteSpace(SelectedFile);
        }

        private void ExecuteCancel()
        {
            CloseDialog(false);
        }

        private void CloseDialog(bool? result)
        {
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
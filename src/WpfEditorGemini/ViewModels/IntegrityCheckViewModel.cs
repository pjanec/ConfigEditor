using JsonConfigEditor.Core.Services;
using System;
using System.Windows.Input;

namespace JsonConfigEditor.ViewModels
{
    /// <summary>
    /// ViewModel for the Integrity Check configuration dialog. It holds the state
    /// of the various check options and triggers the check on the MainViewModel.
    /// </summary>
    public class IntegrityCheckViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;
        private readonly Action _closeAction;

        private bool _checkFilePathConsistency;
        public bool CheckFilePathConsistency
        {
            get => _checkFilePathConsistency;
            set => SetProperty(ref _checkFilePathConsistency, value);
        }

        private bool _checkOverlappingDefinitions;
        public bool CheckOverlappingDefinitions
        {
            get => _checkOverlappingDefinitions;
            set => SetProperty(ref _checkOverlappingDefinitions, value);
        }

        private bool _checkSchemaCompliance;
        public bool CheckSchemaCompliance
        {
            get => _checkSchemaCompliance;
            set => SetProperty(ref _checkSchemaCompliance, value);
        }
        
        private bool _checkPropertyNameCasing;
        public bool CheckPropertyNameCasing
        {
            get => _checkPropertyNameCasing;
            set => SetProperty(ref _checkPropertyNameCasing, value);
        }
        
        private bool _checkEmptyFilesOrFolders;
        public bool CheckEmptyFilesOrFolders
        {
            get => _checkEmptyFilesOrFolders;
            set => SetProperty(ref _checkEmptyFilesOrFolders, value);
        }

        // Add a property for the new checkbox
        private bool _checkFileSystemVsSchemaCasing;
        public bool CheckFileSystemVsSchemaCasing
        {
            get => _checkFileSystemVsSchemaCasing;
            set => SetProperty(ref _checkFileSystemVsSchemaCasing, value);
        }

        /// <summary>
        /// Command to execute the selected integrity checks.
        /// </summary>
        public ICommand RunChecksCommand { get; }

        public IntegrityCheckViewModel(MainViewModel mainViewModel, IntegrityCheckType initialSelection, Action closeAction)
        {
            _mainViewModel = mainViewModel;
            _closeAction = closeAction;

            // Initialize checkbox states from the provided initial selection
            CheckFilePathConsistency = initialSelection.HasFlag(IntegrityCheckType.FilePathConsistency);
            CheckOverlappingDefinitions = initialSelection.HasFlag(IntegrityCheckType.OverlappingDefinitions);
            CheckSchemaCompliance = initialSelection.HasFlag(IntegrityCheckType.SchemaCompliance);
            CheckPropertyNameCasing = initialSelection.HasFlag(IntegrityCheckType.PropertyNameCasing);
            CheckEmptyFilesOrFolders = initialSelection.HasFlag(IntegrityCheckType.EmptyFilesOrFolders);
            // Initialize the new property
            CheckFileSystemVsSchemaCasing = initialSelection.HasFlag(IntegrityCheckType.FileSystemSchemaCasing);
            
            RunChecksCommand = new RelayCommand(ExecuteRunChecks);
        }

        /// <summary>
        /// Gathers the selected check types and tells the MainViewModel to execute the integrity check.
        /// </summary>
        private void ExecuteRunChecks()
        {
            var checksToRun = IntegrityCheckType.None;
            if (CheckFilePathConsistency) checksToRun |= IntegrityCheckType.FilePathConsistency;
            if (CheckOverlappingDefinitions) checksToRun |= IntegrityCheckType.OverlappingDefinitions;
            if (CheckSchemaCompliance) checksToRun |= IntegrityCheckType.SchemaCompliance;
            if (CheckPropertyNameCasing) checksToRun |= IntegrityCheckType.PropertyNameCasing;
            if (CheckEmptyFilesOrFolders) checksToRun |= IntegrityCheckType.EmptyFilesOrFolders;
            // Add the new check to the flag
            if (CheckFileSystemVsSchemaCasing) checksToRun |= IntegrityCheckType.FileSystemSchemaCasing;
            
            // Delegate execution to the MainViewModel
            _mainViewModel.ExecuteIntegrityCheck(checksToRun);
            
            // Close the dialog window
            _closeAction?.Invoke();
        }
    }
}



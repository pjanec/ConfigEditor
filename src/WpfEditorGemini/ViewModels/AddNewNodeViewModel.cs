using RuntimeConfig.Core.Dom;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using JsonConfigEditor.ViewModels;

namespace JsonConfigEditor.ViewModels
{
    public class AddNewNodeViewModel : ViewModelBase
    {
        #region Fields
        private readonly Action<string, string, NodeType?> _onCommit;
        private readonly Func<string, bool> _isNameValid;

        private string _propertyName = string.Empty;
        private string _valueString = string.Empty;
        private NodeType _deducedType = NodeType.String;
        private NodeType? _selectedType;
        private string _validationError = string.Empty;
        #endregion

        #region Properties
        public string PropertyName
        {
            get => _propertyName;
            set
            {
                if (SetProperty(ref _propertyName, value))
                {
                    ValidateName();
                }
            }
        }

        public string ValueString
        {
            get => _valueString;
            set
            {
                if (SetProperty(ref _valueString, value))
                {
                    DeduceTypeFromValue();
                }
            }
        }

        public NodeType DeducedType
        {
            get => _deducedType;
            private set => SetProperty(ref _deducedType, value);
        }

        public NodeType? SelectedType
        {
            get => _selectedType;
            set => SetProperty(ref _selectedType, value);
        }
          
        public string ValidationError
        {
            get => _validationError;
            private set => SetProperty(ref _validationError, value);
        }

        public ICommand CommitCommand { get; }
        #endregion

        public AddNewNodeViewModel(Action<string, string, NodeType?> onCommit, Func<string, bool> isNameValid)
        {
            _onCommit = onCommit;
            _isNameValid = isNameValid;
            CommitCommand = new RelayCommand(ExecuteCommit, CanExecuteCommit);

            // Listen for property changes to re-evaluate CanExecute
            this.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When a property that affects validation changes, tell the command to re-query its state.
            if (e.PropertyName == nameof(ValidationError) || e.PropertyName == nameof(PropertyName))
            {
                (CommitCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private void DeduceTypeFromValue()
        {
            var value = ValueString;
            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                DeducedType = NodeType.String;
                return;
            }

            if (value.Trim() == "{}") DeducedType = NodeType.Object;
            else if (value.Trim() == "[]") DeducedType = NodeType.Array;
            else if (bool.TryParse(value, out _)) DeducedType = NodeType.Boolean;
            else if (value.Equals("null", StringComparison.OrdinalIgnoreCase)) DeducedType = NodeType.Null;
            else if (double.TryParse(value, out _)) DeducedType = NodeType.Number;
            else DeducedType = NodeType.String;
        }

        private void ValidateName()
        {
            if (string.IsNullOrWhiteSpace(PropertyName))
            {
                ValidationError = "Property name cannot be empty.";
            }
            else if (!_isNameValid(PropertyName))
            {
                ValidationError = "A property with this name already exists.";
            }
            else
            {
                ValidationError = string.Empty;
            }
        }

        private bool CanExecuteCommit()
        {
            // Commit is only possible if the name is not empty and has no validation errors.
            return !string.IsNullOrWhiteSpace(PropertyName) && string.IsNullOrEmpty(ValidationError);
        }

        private void ExecuteCommit()
        {
            // Final validation check before committing
            ValidateName();
            if (!CanExecuteCommit()) return;

            _onCommit(PropertyName, ValueString, SelectedType);
        }
    }
} 
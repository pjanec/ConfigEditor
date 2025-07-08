using JsonConfigEditor.Core.Services;
using JsonConfigEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace JsonConfigEditor.Views
{
    public partial class ConsolidationDialog : Window
    {
        public ConsolidationDialog(List<ConsolidationAction> actions, Action<List<ConsolidationAction>> applyCallback)
        {
            InitializeComponent();
            DataContext = new ConsolidationDialogViewModel(actions, applyCallback, () => this.Close());
        }
    }

    public class ConsolidationDialogViewModel : ViewModelBase
    {
        public ObservableCollection<ConsolidationActionViewModel> Actions { get; }
        public ICommand ApplyCommand { get; }
        private readonly Action<List<ConsolidationAction>> _applyCallback;
        private readonly Action _closeAction;

        public ConsolidationDialogViewModel(List<ConsolidationAction> actions, Action<List<ConsolidationAction>> applyCallback, Action closeAction)
        {
            Actions = new ObservableCollection<ConsolidationActionViewModel>(
                actions.Select(a => new ConsolidationActionViewModel(a))
            );
            _applyCallback = applyCallback;
            _closeAction = closeAction;
            ApplyCommand = new RelayCommand(ExecuteApply);
        }

        private void ExecuteApply()
        {
            var selectedActions = Actions.Where(vm => vm.IsSelected).Select(vm => vm.Action).ToList();
            _applyCallback(selectedActions);
            _closeAction();
        }
    }
} 
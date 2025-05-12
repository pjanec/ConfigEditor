using System.Windows.Input;
using WpfUI.ViewModels;

namespace WpfUI.Commands;

public class EditNodeCommand : ICommand
{
    private readonly DomTableEditorViewModel _viewModel;

    public EditNodeCommand(DomTableEditorViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public bool CanExecute(object? parameter)
    {
        return parameter is DomNodeViewModel node && !node.IsEditing;
    }

    public void Execute(object? parameter)
    {
        if (parameter is DomNodeViewModel node)
        {
            _viewModel.BeginEdit(node);
        }
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
} 
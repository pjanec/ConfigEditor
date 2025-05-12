using System.Windows.Input;
using WpfUI.ViewModels;

namespace WpfUI.Commands;

public class DeleteArrayItemCommand : ICommand
{
    private readonly DomTableEditorViewModel _viewModel;

    public DeleteArrayItemCommand(DomTableEditorViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public bool CanExecute(object? parameter)
    {
        return _viewModel.SelectedNodes.Any(n => n.DomNode.Parent is ArrayNode);
    }

    public void Execute(object? parameter)
    {
        _viewModel.DeleteSelectedArrayItems();
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
} 
using System.Windows.Input;
using WpfUI.Models;
using WpfUI.ViewModels;

namespace WpfUI.Commands;

public class CopyArrayItemCommand : ICommand
{
    private readonly DomTableEditorViewModel _viewModel;

    public CopyArrayItemCommand(DomTableEditorViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public bool CanExecute(object? parameter)
    {
        return _viewModel.SelectedNodes.Any(n => n.DomNode.Parent is ArrayNode);
    }

    public void Execute(object? parameter)
    {
        _viewModel.CopySelectedArrayItems();
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
} 
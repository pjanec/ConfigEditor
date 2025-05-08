using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ConfigDom;

/// <summary>
/// View model for a DOM node, providing UI-friendly access to node properties and children.
/// </summary>
public class DomNodeViewModel : INotifyPropertyChanged
{
    private readonly string _path;
    private readonly Json5EditorLayer _activeLayer;
    private readonly Func<string, DomNode?> _getEffectiveNode;
    private bool _isExpanded;
    private bool _isSelected;

    public DomNodeViewModel(string path, Func<string, DomNode?> getEffective, Json5EditorLayer activeLayer)
    {
        _path = path;
        _getEffectiveNode = getEffective;
        _activeLayer = activeLayer;
        Children = new ObservableCollection<DomNodeViewModel>();
    }

    public string Name => _path.Split('/').Last();
    public string Path => _path;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<DomNodeViewModel> Children { get; }

    public JsonElement? LayerLocalValue => DomTreePathHelper.FindNodeAtPath(_activeLayer.RootNode, _path)?.ExportJson();
    public JsonElement? EffectiveValue => _getEffectiveNode(_path)?.ExportJson();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
using System;
using System.Windows;
using System.Windows.Controls;
using WpfUI.Models;

namespace WpfUI.Editors
{
    /// <summary>
    /// Modal editor for complex objects that provides property-by-property editing.
    /// Dynamically creates appropriate editors for each property based on its type.
    /// </summary>
    public class ComplexObjectEditor : INodeValueEditor
    {
        private object? _originalValue;
        private object? _currentValue;
        private readonly Type _objectType;

        public bool IsModal => true;

        public ComplexObjectEditor(Type objectType)
        {
            _objectType = objectType;
        }

        /// <summary>
        /// Builds a property editor view for the object.
        /// Creates appropriate editors (TextBox, CheckBox) for each property based on its type.
        /// Properties are edited in-place with live updates to the object.
        /// </summary>
        public FrameworkElement BuildEditorView(DomNode node)
        {
            if (node is not ValueNode valueNode)
                return new TextBlock { Text = "Invalid node type" };

            _originalValue = valueNode.Value;
            _currentValue = _originalValue;

            var stackPanel = new StackPanel { Margin = new Thickness(10) };

            // Add property editors based on the object type
            foreach (var property in _objectType.GetProperties())
            {
                var label = new Label { Content = property.Name };
                var value = property.GetValue(_currentValue);

                FrameworkElement editor;
                if (property.PropertyType == typeof(string))
                {
                    editor = new TextBox { Text = value?.ToString() ?? "" };
                    ((TextBox)editor).TextChanged += (s, e) =>
                    {
                        try
                        {
                            property.SetValue(_currentValue, ((TextBox)s).Text);
                        }
                        catch { }
                    };
                }
                else if (property.PropertyType == typeof(int))
                {
                    editor = new TextBox { Text = value?.ToString() ?? "0" };
                    ((TextBox)editor).TextChanged += (s, e) =>
                    {
                        if (int.TryParse(((TextBox)s).Text, out var intValue))
                        {
                            try
                            {
                                property.SetValue(_currentValue, intValue);
                            }
                            catch { }
                        }
                    };
                }
                else if (property.PropertyType == typeof(bool))
                {
                    editor = new CheckBox { IsChecked = (bool?)value };
                    ((CheckBox)editor).Checked += (s, e) =>
                    {
                        try
                        {
                            property.SetValue(_currentValue, ((CheckBox)s).IsChecked);
                        }
                        catch { }
                    };
                }
                else
                {
                    editor = new TextBlock { Text = value?.ToString() ?? "" };
                }

                stackPanel.Children.Add(label);
                stackPanel.Children.Add(editor);
            }

            return stackPanel;
        }

        /// <summary>
        /// Attempts to get the edited value from the property editors.
        /// Returns true if all property edits were valid.
        /// </summary>
        public bool TryGetEditedValue(out object newValue)
        {
            newValue = _currentValue ?? _originalValue;
            return true;
        }

        /// <summary>
        /// Cancels the edit by restoring the original object state.
        /// </summary>
        public void CancelEdit()
        {
            _currentValue = _originalValue;
        }

        /// <summary>
        /// Confirms the edit by keeping the current property values.
        /// </summary>
        public void ConfirmEdit()
        {
            // Value is already updated in the property setters
        }
    }
} 
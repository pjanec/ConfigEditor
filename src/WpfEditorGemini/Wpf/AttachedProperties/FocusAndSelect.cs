using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JsonConfigEditor.Wpf.AttachedProperties
{
    public static class FocusAndSelect
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(FocusAndSelect),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsEnabledProperty, value);
        }

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox && (bool)e.NewValue)
            {
                textBox.Loaded += (s, args) =>
                {
                    textBox.Focus();
                    textBox.SelectAll();
                };
                // Attempt to focus immediately if already loaded
                if (textBox.IsLoaded)
                {
                    Keyboard.Focus(textBox); // More robust focus
                    textBox.SelectAll();
                }
            }
        }
    }
} 
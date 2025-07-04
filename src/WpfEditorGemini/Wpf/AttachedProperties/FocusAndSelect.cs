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
            // The check must be for FrameworkElement, which defines the 'Loaded' event.
            if (d is FrameworkElement frameworkElement && (bool)e.NewValue)
            {
                frameworkElement.Loaded += (s, args) =>
                {
                    // Set focus on any FrameworkElement
                    frameworkElement.Focus();

                    // If it happens to be a TextBox, also select the text
                    if (frameworkElement is TextBox textBox)
                    {
                        textBox.SelectAll();
                    }
                };
                
                if (frameworkElement.IsLoaded)
                {
                    // Use the more robust Keyboard.Focus for elements already loaded
                    Keyboard.Focus(frameworkElement);

                    if (frameworkElement is TextBox textBox)
                    {
                        textBox.SelectAll();
                    }
                }
            }
        }

    }
} 
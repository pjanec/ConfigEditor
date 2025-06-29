using JsonConfigEditor.ViewModels; // Required for MainViewModel
using System.Linq;
using System.Windows;

namespace JsonConfigEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string? StartupFilePath { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize logging or other application-wide services here if needed

            if (e.Args.Length > 0)
            {
                // Assuming the first argument is the file path
                // In a real app, more robust argument parsing might be needed
                StartupFilePath = e.Args[0];
                System.Diagnostics.Debug.WriteLine($"[App.OnStartup] Startup file path provided: {StartupFilePath}");
            }

            // The MainViewModel will be instantiated by MainWindow.xaml or its code-behind.
            // MainWindow's constructor or OnLoaded event can then access App.StartupFilePath
            // and pass it to the MainViewModel.
            // For example, in MainWindow.xaml.cs constructor:
            // var mainViewModel = DataContext as MainViewModel;
            // if (mainViewModel != null && !string.IsNullOrEmpty(App.StartupFilePath))
            // {
            //     mainViewModel.InitializeWithStartupFile(App.StartupFilePath);
            // }
        }
    }
}
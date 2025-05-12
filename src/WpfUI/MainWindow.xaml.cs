using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.Json;
using WpfUI.Editors;
using WpfUI.Models;
using WpfUI.Renderers;
using WpfUI.ViewModels;

namespace WpfUI
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private readonly DomTableEditorViewModel _viewModel;

		public MainWindow()
		{
			InitializeComponent();

			var schemaResolver = new DomSchemaTypeResolver();
			var editorRegistry = new DomEditorRegistry();
			RegisterEditorsAndRenderers(editorRegistry);

			_viewModel = new DomTableEditorViewModel(schemaResolver, editorRegistry);
			DataContext = _viewModel;

			InitializeTestData(schemaResolver);
		}

		private void RegisterEditorsAndRenderers(DomEditorRegistry registry)
		{
			// Register string editors and renderers
			registry.RegisterEditor(typeof(string), new StringValueEditor());
			registry.RegisterRenderer(typeof(string), new StringValueRenderer());

			// Register numeric editors and renderers
			registry.RegisterEditor(typeof(int), new NumericValueEditor(typeof(int)));
			registry.RegisterRenderer(typeof(int), new NumericValueRenderer());
			registry.RegisterEditor(typeof(double), new NumericValueEditor(typeof(double)));
			registry.RegisterRenderer(typeof(double), new NumericValueRenderer());

			// Register boolean editors and renderers
			registry.RegisterEditor(typeof(bool), new BooleanValueEditor());
			registry.RegisterRenderer(typeof(bool), new BooleanValueRenderer());

			// Register complex object editor
			registry.RegisterEditor(typeof(object), new ComplexObjectEditor(typeof(object)));
		}

		private void InitializeTestData(DomSchemaTypeResolver schemaResolver)
		{
			// Create root object
			var root = new ObjectNode("Root");

			// Add some test properties
			var settings = new ObjectNode("Settings");
			root.Children["Settings"] = settings;

			settings.Children["Hostname"] = new ValueNode("Hostname", JsonSerializer.SerializeToElement("localhost"));
			settings.Children["Port"] = new ValueNode("Port", JsonSerializer.SerializeToElement(8080));
			settings.Children["IsEnabled"] = new ValueNode("IsEnabled", JsonSerializer.SerializeToElement(true));

			// Add an array of users
			var users = new ArrayNode("Users");
			root.Children["Users"] = users;

			var user1 = new ObjectNode("User1");
			user1.Children["Name"] = new ValueNode("Name", JsonSerializer.SerializeToElement("Alice"));
			user1.Children["Age"] = new ValueNode("Age", JsonSerializer.SerializeToElement(30));
			users.Items.Add(user1);

			var user2 = new ObjectNode("User2");
			user2.Children["Name"] = new ValueNode("Name", JsonSerializer.SerializeToElement("Bob"));
			user2.Children["Age"] = new ValueNode("Age", JsonSerializer.SerializeToElement(25));
			users.Items.Add(user2);

			// Register types in schema resolver
			schemaResolver.RegisterType("Root/Settings/Hostname", typeof(string));
			schemaResolver.RegisterType("Root/Settings/Port", typeof(int));
			schemaResolver.RegisterType("Root/Settings/IsEnabled", typeof(bool));
			schemaResolver.RegisterType("Root/Users/User1/Name", typeof(string));
			schemaResolver.RegisterType("Root/Users/User1/Age", typeof(int));
			schemaResolver.RegisterType("Root/Users/User2/Name", typeof(string));
			schemaResolver.RegisterType("Root/Users/User2/Age", typeof(int));

			// Initialize the editor
			_viewModel.Initialize(root);
		}

		private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (sender is FrameworkElement element && element.DataContext is DomNodeViewModel node)
			{
				_viewModel.EditNodeCommand.Execute(node);
			}
		}

		private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (sender is FrameworkElement element && element.DataContext is DomNodeViewModel node)
			{
				_viewModel.HandleMouseClick(node, e.ChangedButton, Keyboard.Modifiers);
			}
		}

		private void TreeView_KeyDown(object sender, KeyEventArgs e)
		{
			if (_viewModel.ActiveEditor != null)
			{
				// Let the editor handle its own keyboard events
				return;
			}

			_viewModel.HandleKeyDown(e.Key, Keyboard.Modifiers);

			switch (e.Key)
			{
				case Key.Enter:
					if (sender is TreeView treeView && treeView.SelectedItem is DomNodeViewModel node)
					{
						_viewModel.EditNodeCommand.Execute(node);
						e.Handled = true;
					}
					break;

				case Key.Delete:
					_viewModel.DeleteArrayItemCommand.Execute(null);
					e.Handled = true;
					break;

				case Key.Insert:
					_viewModel.InsertArrayItemCommand.Execute(null);
					e.Handled = true;
					break;

				case Key.C:
					if (Keyboard.Modifiers == ModifierKeys.Control)
					{
						_viewModel.CopyArrayItemCommand.Execute(null);
						e.Handled = true;
					}
					break;

				case Key.V:
					if (Keyboard.Modifiers == ModifierKeys.Control)
					{
						_viewModel.PasteArrayItemCommand.Execute(null);
						e.Handled = true;
					}
					break;

				case Key.Escape:
					if (sender is TextBox textBox && textBox.IsFocused)
					{
						_viewModel.FilterText = string.Empty;
						e.Handled = true;
					}
					break;
			}
		}

		private void TreeView_KeyUp(object sender, KeyEventArgs e)
		{
			_viewModel.HandleKeyUp(Keyboard.Modifiers);
		}
	}
}
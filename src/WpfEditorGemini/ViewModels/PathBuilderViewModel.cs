using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace JsonConfigEditor.ViewModels
{
    public class PathBuilderViewModel : ViewModelBase
    {
        private string? _selectedSegment;
        private string _newSegmentText = string.Empty;

        public PathBuilderViewModel(string nodePath)
        {
            NodePath = nodePath;
            PathSegments = new ObservableCollection<string>();

            var segments = nodePath.Split('/')
                .Where(s => !string.IsNullOrEmpty(s) && s != "$root")
                .ToList();

            string defaultFileName = "data.json";

            // Add "$root" as the base option for saving in the root directory.
            PathSegments.Add("$root");
            string cumulativePath = "";

            // FIX: Generate directory options only up to the "grandparent" level.
            // The immediate parent path is implied by the filename itself.
            if (segments.Count > 1)
            {
                var directorySegments = segments.Take(segments.Count - 1).ToList();
        
                // Suggest a filename based on the immediate parent's name.
                defaultFileName = directorySegments.Last() + ".json";

                // But only create directory choices from the grandparent level and up.
                if (directorySegments.Count > 1)
                {
                    var grandParentDirectorySegments = directorySegments.Take(directorySegments.Count - 1);
                    foreach (var segment in grandParentDirectorySegments)
                    {
                        cumulativePath += "/" + segment;
                        PathSegments.Add("$root" + cumulativePath);
                    }
                }
            }
            else if (segments.Any())
            {
                defaultFileName = segments.First() + ".json";
            }

            NewSegmentText = defaultFileName;

            // Default the selection to the most specific (last) directory in the list.
            if (PathSegments.Any())
            {
                SelectedSegment = PathSegments.Last();
            }

            AddSegmentCommand = new RelayCommand(ExecuteAddSegment, CanExecuteAddSegment);
            RemoveSegmentCommand = new RelayCommand(ExecuteRemoveSegment, CanExecuteRemoveSegment);
            ConfirmCommand = new RelayCommand(ExecuteConfirm, CanExecuteConfirm);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        public string NodePath { get; }
        public ObservableCollection<string> PathSegments { get; }

        public string? SelectedSegment
        {
            get => _selectedSegment;
            set
            {
                if (SetProperty(ref _selectedSegment, value))
                {
                    UpdatePreviewPath();
                }
            }
        }

        public string NewSegmentText
        {
            get => _newSegmentText;
            set
            {
                if (SetProperty(ref _newSegmentText, value))
                {
                    UpdatePreviewPath();
                }
            }
        }

        public string PreviewPath { get; private set; } = string.Empty;
        public string? SelectedPath { get; private set; }

        public ICommand AddSegmentCommand { get; }
        public ICommand RemoveSegmentCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        private void ExecuteAddSegment()
        {
            if (!IsSegmentValid(NewSegmentText))
            {
                MessageBox.Show(
                    "A path segment or filename cannot contain path separators ('/' or '\\').",
                    "Invalid Input",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string basePath = SelectedSegment ?? "$root";
            string newSegment = (basePath == "$root" ? "" : basePath.Substring("$root".Length)) + "/" + NewSegmentText;
            PathSegments.Add("$root" + newSegment);
            SelectedSegment = PathSegments.Last();
            NewSegmentText = string.Empty;
        }

        private bool CanExecuteAddSegment()
        {
            return !string.IsNullOrWhiteSpace(NewSegmentText);
        }

        private void ExecuteRemoveSegment()
        {
            if (SelectedSegment != null && SelectedSegment != "$root")
            {
                PathSegments.Remove(SelectedSegment);
                SelectedSegment = PathSegments.LastOrDefault();
            }
        }

        private bool CanExecuteRemoveSegment()
        {
            return SelectedSegment != null && SelectedSegment != "$root";
        }

        private void ExecuteConfirm()
        {
            if (!IsSegmentValid(NewSegmentText))
            {
                MessageBox.Show(
                    "The filename cannot contain path separators ('/' or '\\').\nPlease enter a valid filename.",
                    "Invalid Filename",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            SelectedPath = PreviewPath;
            CloseDialog(true);
        }

        private bool CanExecuteConfirm()
        {
            return !string.IsNullOrEmpty(PreviewPath);
        }

        private void ExecuteCancel()
        {
            CloseDialog(false);
        }

        private void UpdatePreviewPath()
        {
            // FIX: Combine the selected directory and the filename to create the final relative path.
            string directory = SelectedSegment ?? "";
            string filename = NewSegmentText ?? "";

            if (directory == "$root")
            {
                PreviewPath = filename;
            }
            else if (!string.IsNullOrEmpty(directory))
            {
                string actualDirectory = directory.StartsWith("$root/") ? directory.Substring("$root/".Length) : directory;
                PreviewPath = Path.Combine(actualDirectory, filename).Replace('\\', '/');
            }
            else
            {
                PreviewPath = filename;
            }

            OnPropertyChanged(nameof(PreviewPath));
        }

        private bool IsSegmentValid(string segment)
        {
            return !string.IsNullOrWhiteSpace(segment) &&
                   !segment.Contains('/') &&
                   !segment.Contains('\\');
        }

        private void CloseDialog(bool? result)
        {
            if (Application.Current?.MainWindow != null)
            {
                var dialog = Application.Current.Windows.OfType<Window>()
                     .FirstOrDefault(w => w.DataContext == this);
                if (dialog != null)
                {
                    dialog.DialogResult = result;
                    dialog.Close();
                }
            }
        }
    }
}
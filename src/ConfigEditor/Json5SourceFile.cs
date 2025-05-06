using System;
using System.IO;
using System.Text;

namespace ConfigEditor.Util
{
    public class Json5SourceFile
    {
        public string FilePath { get; }
        public string Text { get; private set; }
        public bool IsDirty { get; private set; }

        public Json5SourceFile(string filePath, string initialText)
        {
            FilePath = filePath;
            Text = initialText;
            IsDirty = false;
        }

        public void UpdateText(string newText)
        {
            if (Text != newText)
            {
                Text = newText;
                IsDirty = true;
            }
        }

        public void Save()
        {
            File.WriteAllText(FilePath, Text, Encoding.UTF8);
            IsDirty = false;
        }

        public override string ToString() => $"Json5SourceFile[{FilePath}, Dirty={IsDirty}]";
    }
}

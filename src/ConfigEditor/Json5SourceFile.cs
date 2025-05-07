namespace ConfigDom
{
    /// <summary>
    /// Represents a single parsed JSON source file in the editor.
    /// Includes original text and the corresponding DOM subtree.
    /// </summary>
    public class Json5SourceFile
    {
        public string AbsolutePath { get; }
        public string RelativePath { get; }
        public DomNode DomRoot { get; }
        public string OriginalText { get; }

        public Json5SourceFile(string absolutePath, string relativePath, DomNode domRoot, string originalText)
        {
            AbsolutePath = absolutePath;
            RelativePath = relativePath;
            DomRoot = domRoot;
            OriginalText = originalText;
        }
    }
}

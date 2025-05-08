using ConfigEditor.Dom;

namespace ConfigEditor.IO
{
	/// <summary>
	/// Represents a single parsed JSON source file in the editor.
	/// Includes original text and the corresponding DOM subtree.
	/// </summary>
	public class SourceFile
	{
		public string RelativePath { get; }
		public DomNode DomRoot { get; }
		public string OriginalText { get; }
		/// <summary>
		/// The absolute path to the source file.
		/// </summary>
		public string FilePath { get; }

		public SourceFile( string filePath, string relativePath, DomNode domRoot, string originalText )
		{
			FilePath = filePath;
			RelativePath = relativePath;
			DomRoot = domRoot;
			OriginalText = originalText;
		}
	}
}

using JsonConfigEditor.Core.Dom;
using System.IO;
using System.Threading.Tasks;

namespace JsonConfigEditor.Core.Serialization
{
    /// <summary>
    /// Service interface for serializing DOM trees back to JSON.
    /// (From specification document, Section 2.8)
    /// </summary>
    public interface IDomNodeToJsonSerializer
    {
        /// <summary>
        /// Serializes a DOM tree to a JSON string.
        /// </summary>
        /// <param name="rootNode">The root DOM node to serialize</param>
        /// <param name="indented">Whether to format the JSON with indentation</param>
        /// <returns>The JSON string representation</returns>
        string SerializeToString(DomNode rootNode, bool indented = true);

        /// <summary>
        /// Asynchronously serializes a DOM tree to a JSON file.
        /// </summary>
        /// <param name="rootNode">The root DOM node to serialize</param>
        /// <param name="filePath">The path where to save the JSON file</param>
        /// <param name="indented">Whether to format the JSON with indentation</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SerializeToFileAsync(DomNode rootNode, string filePath, bool indented = true);
    }
} 
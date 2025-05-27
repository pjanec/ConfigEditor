using JsonConfigEditor.Core.Dom;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JsonConfigEditor.Core.Parsing
{
    /// <summary>
    /// Service interface for parsing JSON into DOM trees.
    /// (From specification document, Section 2.1)
    /// </summary>
    public interface IJsonDomParser
    {
        /// <summary>
        /// Parses a JSON string into a DOM tree.
        /// </summary>
        /// <param name="jsonContent">The JSON content to parse</param>
        /// <returns>The root DOM node</returns>
        /// <exception cref="JsonException">Thrown when the JSON is malformed</exception>
        DomNode ParseFromString(string jsonContent);

        /// <summary>
        /// Asynchronously parses a JSON file into a DOM tree.
        /// </summary>
        /// <param name="filePath">The path to the JSON file</param>
        /// <returns>A task containing the root DOM node</returns>
        /// <exception cref="JsonException">Thrown when the JSON is malformed</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist</exception>
        Task<DomNode> ParseFromFileAsync(string filePath);

        /// <summary>
        /// Parses a JsonElement into a DOM tree.
        /// </summary>
        /// <param name="jsonElement">The JSON element to parse</param>
        /// <param name="name">The name for the root node</param>
        /// <param name="parent">The parent node (null for root)</param>
        /// <returns>The DOM node representing the JSON element</returns>
        DomNode ParseFromJsonElement(JsonElement jsonElement, string name, DomNode? parent = null);
    }
} 
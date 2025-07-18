using RuntimeConfig.Core.Schema;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JsonConfigEditor.Core.SchemaLoading
{
    /// <summary>
    /// Service interface for loading schema definitions from C# assemblies.
    /// (From specification document, Section 2.2)
    /// </summary>
    public interface ISchemaLoaderService
    {
        /// <summary>
        /// Gets the loaded root schemas keyed by their mount paths.
        /// </summary>
        IReadOnlyDictionary<string, SchemaNode> RootSchemas { get; }

        /// <summary>
        /// Gets any error messages that occurred during schema loading.
        /// </summary>
        IReadOnlyList<string> ErrorMessages { get; }

        /// <summary>
        /// NEW: Gets log messages generated during the schema loading process.
        /// </summary>
        IReadOnlyList<string> LogMessages { get; }

        /// <summary>
        /// Asynchronously loads schema definitions from the specified assembly paths.
        /// </summary>
        /// <param name="assemblyPaths">The paths to assemblies containing schema classes</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task LoadSchemasFromAssembliesAsync(IEnumerable<string> assemblyPaths);

        /// <summary>
        /// Finds the most specific schema node for a given DOM path.
        /// </summary>
        /// <param name="domPath">The path in the DOM tree</param>
        /// <returns>The most specific schema node, or null if no schema matches</returns>
        SchemaNode? FindSchemaForPath(string domPath);

        /// <summary>
        /// Clears all loaded schemas and error messages.
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets the primary or first loaded root schema.
        /// This is a simplification; a more robust system might identify root schemas by a specific marker or convention.
        /// </summary>
        /// <returns>The root schema, or null if none are loaded or identifiable as the primary root.</returns>
        SchemaNode? GetRootSchema();
    }
} 
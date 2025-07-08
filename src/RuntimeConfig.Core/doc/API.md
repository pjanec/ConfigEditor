### RuntimeConfig.Core API Documentation (Part 1 of 6\)

## Core Entry Point & Document Object Model

This document covers the foundational components of the RuntimeConfig.Core library. These are the primary classes a runtime application will interact with and the core data structures that represent the configuration in memory.

### 1\. public class RuntimeDomTree

**Namespace**: RuntimeConfig.Core

Description  
This is the primary entry-point class for a runtime application that needs to consume configuration. Its purpose is to orchestrate the entire loading process—managing providers, merging their data, resolving all references—and to provide a single, clean, and high-performance query interface to the final, effective configuration state. It is designed to be simple to use for the consumer, hiding the complexity of the underlying loading and merging process.  
**Public API**

/// \<summary\>  
/// Initializes a new, empty runtime DOM tree.  
/// \</summary\>  
public RuntimeDomTree();

/// \<summary\>  
/// The root of the fully resolved, queryable configuration tree.   
/// This property is populated after RefreshAsync() is called and represents the final, effective state.  
/// \</summary\>  
public ObjectNode? ResolvedRoot { get; }

/// \<summary\>  
/// Registers a data provider to be mounted at a specific path in the global configuration tree.  
/// This method should be called for all providers before calling RefreshAsync().  
/// \</summary\>  
/// \<param name="mountPath"\>The path where the provider's content will be attached (e.g., "config/app1").\</param\>  
/// \<param name="provider"\>The data provider instance (e.g., a CascadingJsonProvider).\</param\>  
public void RegisterProvider(string mountPath, IRuntimeDomProvider provider);

/// \<summary\>  
/// Asynchronously performs the full load and resolve sequence. This is the main operational method.  
/// It calls Load() on all registered providers, merges their data into a single tree, and then  
/// resolves all $ref references across the entire tree to produce the final ResolvedRoot.  
/// \</summary\>  
public Task RefreshAsync();

/// \<summary\>  
/// Returns a query interface that operates on the fully resolved DOM tree.  
/// This should only be called after RefreshAsync() has successfully completed.  
/// \</summary\>  
/// \<exception cref="InvalidOperationException"\>Thrown if called before the tree has been resolved.\</exception\>  
public DomQuery Query();

**Usage Scenarios**

* **Consumer App**: This is the **only** class a simple consumer app needs to instantiate from the library. The app will create an instance, register one or more providers, call RefreshAsync(), and then use the Query() method to retrieve its configuration values.  
* **Editor App**: The editor will **not** use RuntimeDomTree. The editor needs to manage the state of individual layers and files, a level of detail that RuntimeDomTree is intentionally designed to abstract away.

### 2\. Foundational DOM Classes

**Namespace**: RuntimeConfig.Core.Dom

Description  
These classes form the in-memory representation of the configuration data. They are the "nouns" of the library and are used by all other components. An entire configuration, once parsed, is held in a tree of these DomNode objects.

* public abstract class DomNode  
  * **Description**: The abstract base class for all nodes in the tree.  
  * **API**:  
    * public string Name { get; }: The property name or array index of the node.  
    * public DomNode? Parent { get; }: A reference to the parent node.  
    * public string Path { get; }: The full, unique path from the root (e.g., $root/database/port).  
* public class ObjectNode : DomNode  
  * **Description**: Represents a JSON object; a container for other nodes keyed by name.  
  * **API**: public IReadOnlyDictionary\<string, DomNode\> Children { get; }  
* public class ArrayNode : DomNode  
  * **Description**: Represents a JSON array; a container for an ordered list of other nodes.  
  * **API**: public IReadOnlyList\<DomNode\> Items { get; }  
* public class ValueNode : DomNode  
  * **Description**: Represents a terminal leaf node in the tree (a string, number, boolean, or null).  
  * **API**: public System.Text.Json.JsonElement Value { get; }  
* public class RefNode : DomNode  
  * **Description**: A special node that represents a symbolic $ref link to another part of the tree. These nodes exist only during the loading phase and are eliminated by the RefreshAsync process.  
  * **API**: public string RefPath { get; }

**Usage Scenarios**

* **Consumer App**: The consumer will not interact with these classes directly, but will receive objects deserialized from them via the DomQuery API.  
* **Editor App**: The editor will use these classes extensively. They will be the fundamental data structures it uses to build its own internal, stateful models (e.g., EditorCascadeLayer) that represent the configuration being edited.

### 3\. DOM Utility Classes

**Namespace**: RuntimeConfig.Core.Dom

Description  
These are static helper classes that perform stateless, fundamental operations on DomNode trees.

* **public static class DomTree**  
  * **Description**: Provides common utility methods for traversing and manipulating the DOM.  
  * **API**:  
    * public static DomNode? FindNodeByPath(DomNode root, string path): Finds a node within a tree by its full path.  
    * public static DomNode CloneNode(DomNode node, DomNode? newParent): Performs a deep clone of a node and its entire subtree.  
* **public static class DomMerger**  
  * **Description**: This is a critical "shared kernel" class. It provides the canonical, stateless logic for merging two DOM trees, defining the rules for how layers override each other.  
  * **API**:  
    * public static void MergeInto(ObjectNode target, ObjectNode source): Deeply merges the source node's children into the target node. The rules are:  
      * If a key exists in both and both values are objects, they are merged recursively.  
      * Otherwise, the value/array from the source completely replaces the one in the target.

**Usage Scenarios**

* **Consumer App**: The internal CascadingJsonProvider will use DomMerger to combine layers into a final result. The consumer app itself will not call these utilities.  
* **Editor App**: The editor will use DomTree.FindNodeByPath for navigation and DomTree.CloneNode when creating copies of data. Most importantly, it will use DomMerger.MergeInto to create its "merged preview" for the user, guaranteeing that what the user sees is identical to what the runtime consumer will get.




### RuntimeConfig.Core API Documentation (Part 2 of 6\)

## Provider Infrastructure & Core Services

This document covers the components responsible for supplying data to the RuntimeDomTree. It includes the provider interface, concrete implementations for common scenarios, and the core services that process configuration layers.

### 1\. Provider Interface

#### public interface IRuntimeDomProvider

**Namespace**: RuntimeConfig.Core.Providers

Description  
This is the fundamental contract for any class that can supply a configuration subtree to the RuntimeDomTree. A provider's responsibility is to load data from a source (a file, a database, a web service, etc.) and represent it as a DomNode tree.  
**Public API**

/// \<summary\>  
/// Loads or constructs the provider's data and returns it as the root of a DOM subtree.  
/// This method can be called multiple times, for instance, during a refresh.  
/// \</summary\>  
/// \<returns\>The root ObjectNode of the provider's data.\</returns\>  
ObjectNode Load();

/// \<summary\>  
/// A signal for the provider to refresh its internal state if it's dynamic.  
/// For static providers (like files), this can be a no-op.  
/// \</summary\>  
void Refresh();

### 2\. Concrete Provider Implementations

**Namespace**: RuntimeConfig.Core.Providers

These are the ready-to-use provider classes included in the library.

* **public class CascadingJsonProvider : IRuntimeDomProvider**  
  * **Description**: The most powerful provider, designed for runtime applications that need to consume a full, multi-layer cascading project. Internally, it uses the shared LayerProcessor and DomMerger services to produce a single, final ObjectNode representing the effective configuration.  
  * **API**: public CascadingJsonProvider(IReadOnlyList\<LayerDefinition\> layers)  
  * **Usage Scenarios**:  
    * **Consumer App**: This is the primary provider for a runtime application. The app will typically read a project definition, create a list of LayerDefinition objects, and pass them to this provider's constructor.  
    * **Editor App**: The editor will **not** use this class. It needs more granular control and will use the LayerProcessor directly.  
* **public class SingleJsonProvider : IRuntimeDomProvider**  
  * **Description**: A simple provider that loads configuration from a single JSON file.  
  * **API**: public SingleJsonProvider(string filePath)  
  * **Usage Scenarios**:  
    * **Consumer App**: Useful for simple applications or for mounting secondary, single-file configurations (e.g., a plugin's settings) alongside a main cascading configuration.  
    * **Editor App**: The editor will likely not use this, as it has its own file-loading logic.  
* **public class StaticDomProvider : IRuntimeDomProvider**  
  * **Description**: A provider that wraps a pre-existing, in-memory DomNode tree. Its primary use is to inject a configuration snapshot that has been loaded and deserialized from a file (e.g., a pre-processed snapshot.json).  
  * **API**: public StaticDomProvider(ObjectNode rootNode)  
  * **Usage Scenarios**:  
    * **Consumer App**: Ideal for production environments where the application should load a pre-built, pre-validated configuration snapshot for maximum performance and reliability.  
    * **Editor App**: The editor could use this to load a snapshot file for inspection or comparison.

### 3\. Core Services and Models

These components contain the critical, shared logic for processing configuration layers.

#### Models

**Namespace**: RuntimeConfig.Core.Models

* public record LayerDefinition(string Name, string BasePath): A simple model representing a single layer to be processed. BasePath is the absolute path to the layer's root directory.  
* public record SourceFileInfo(string FilePath, string RelativePath, DomNode DomRoot): A model representing a single parsed source file.  
* **public record LayerProcessingResult**: The rich result object returned by the LayerProcessor.  
  * public ObjectNode MergedRootNode { get; }: The final, merged DOM tree for the entire layer.  
  * public IReadOnlyList\<SourceFileInfo\> LoadedSourceFiles { get; }: A list of all individual source files that were loaded.  
  * public IReadOnlyDictionary\<string, string\> ValueOrigins { get; }: A map tracking the origin file for every value in the merged tree.  
  * public IReadOnlyList\<string\> Errors { get; }: A list of any errors that occurred while processing the layer.

#### public class LayerProcessor

**Namespace**: RuntimeConfig.Core.Services

Description  
This is a critical, stateless, shared service. Its sole responsibility is to correctly process a single configuration layer from a directory on disk. It contains the canonical logic for file discovery, path-to-DOM mapping, and intra-layer merging. Sharing this service is what guarantees consistency between the editor's preview and the runtime's behavior.  
**Public API**

/// \<summary\>  
/// Processes a single layer from its base path. This service finds all .json files,  
/// parses them, and merges them based on directory structure into a single result object.  
/// \</summary\>  
/// \<param name="layerBasePath"\>The absolute file path to the layer's root directory.\</param\>  
/// \<returns\>A LayerProcessingResult containing the merged tree and detailed source information.\</returns\>  
public LayerProcessingResult Process(string layerBasePath);

**Usage Scenarios**

* **Consumer App**: The internal CascadingJsonProvider uses this service to process each layer sequentially. It only needs the MergedRootNode from the result and discards the rest of the detailed information.  
* **Editor App**: The editor calls this service directly for each layer it loads. It uses the **entire** LayerProcessingResult to build its own stateful EditorCascadeLayer model, using ValueOrigins to know where to save changes and Errors to populate the UI's issue panel.







RuntimeConfig.Core API Documentation (Part 3 of 6)
Querying & Serialization API
This document covers the APIs responsible for getting data out of the DomNode tree and for converting the tree to and from a standard JSON format.
1. Querying API
public class DomQuery
Namespace: RuntimeConfig.Core.Querying
Description
This class provides a clean, strongly-typed API for retrieving data from a DomNode tree. An instance of this class is obtained from a RuntimeDomTree after it has been refreshed and resolved. It always operates on the final, effective configuration state, ensuring that queries are fast, simple, and free of reference-resolution logic.
Public API
/// <summary>
/// Initializes a new query context starting from the given node.
/// This allows for scoping queries to a specific subsection of the configuration.
/// </summary>
/// <param name="queryRoot">The DomNode to use as the root for all paths in this query instance.</param>
public DomQuery(DomNode queryRoot);

/// <summary>
/// Finds a node at the specified path and deserializes it into the requested type T.
/// This is the primary method for data retrieval. It supports both simple types (int, string, bool)
/// and complex, deeply nested class objects. The deserialization is recursive.
/// </summary>
/// <typeparam name="T">The target type to deserialize into.</typeparam>
/// <param name="path">The relative path from the query's root node (e.g., "database/port").</param>
/// <returns>An instance of type T populated with data from the DOM.</returns>
/// <exception cref="KeyNotFoundException">Thrown if the path does not exist in the DOM tree.</exception>
/// <exception cref="JsonException">Thrown if the DOM structure cannot be deserialized into type T.</exception>
public T Get<T>(string path);

/// <summary>
/// Finds and returns a DomNode at a specified path, if it exists. This is useful for
/// inspecting the raw DOM structure or for creating a new, scoped DomQuery instance.
/// </summary>
/// <param name="path">The relative path from the query's root node.</param>
/// <returns>The found DomNode, or null if the path does not exist.</returns>
public DomNode? FindNode(string path);


Usage Scenarios
Consumer App: This is the main way a runtime application will interact with its configuration.
// Get the query interface from the main tree
var query = runtimeTree.Query();

// Get a simple value
int timeout = query.Get<int>("network/timeout");

// Get a complex, nested object
DatabaseSettings dbSettings = query.Get<DatabaseSettings>("database");


Editor App: The editor will primarily use DomTree.FindNodeByPath for its UI logic. However, it could use DomQuery for features like a "resolved value preview" that shows what a $ref link points to, or for validating that a section of the DOM can be successfully deserialized into its corresponding schema class.
2. Serialization API
Namespace: RuntimeConfig.Core.Serialization
Description
These classes handle the conversion of an in-memory DomNode tree to and from a standard JSON string format. This is essential for saving modifications and for the snapshotting workflow.
public class JsonDomSerializer
Description: Serializes a DomNode tree into a JSON string.
Public API
/// <summary>
/// Serializes the given DomNode and its descendants into a JSON string.
/// </summary>
/// <param name="root">The root node of the tree to serialize.</param>
/// <param name="indented">If true, the output JSON will be formatted with human-readable indentation.</param>
/// <returns>A JSON string representation of the DOM tree.</returns>
public string ToJson(DomNode root, bool indented = true);


Usage Scenarios
Consumer App: A consumer app would typically not use this.
Editor App: This is critical for saving changes. When the user saves a file, the editor will take the corresponding DomNode from its internal model, pass it to ToJson(), and write the resulting string to the file system.
public class JsonDomDeserializer
Description: Deserializes a JSON string into a DomNode tree.
Public API
/// <summary>
/// Deserializes a JSON string into a DomNode tree.
/// </summary>
/// <param name="json">The JSON string to parse.</param>
/// <returns>The root ObjectNode of the parsed DOM tree.</returns>
/// <exception cref="JsonException">Thrown if the input string is not valid JSON.</exception>
public ObjectNode FromJson(string json);


Usage Scenarios
Consumer App: The internal SingleJsonProvider uses this to load its file. A consumer app could also use it directly to load a static configuration snapshot as part of the StaticDomProvider workflow.
Editor App: This is used during the initial loading phase. The editor's ProjectLoader finds a .json file on disk, reads its text content, and passes that text to FromJson() to get the initial DomNode tree for that file.




RuntimeConfig.Core API Documentation (Part 4 of 6)
Schema & Validation API
This document covers the components responsible for defining, loading, and enforcing a schema on the configuration DOM. A schema allows you to define the expected structure, data types, and constraints of your configuration, enabling powerful validation and tooling.
1. Schema Definition
Namespace: RuntimeConfig.Core.Schema
Schemas are defined using standard C# classes and decorated with attributes from the library.
public class SchemaNode
Description: The in-memory representation of a single node in the schema tree. It holds all constraints and metadata for a configuration property, such as its type, whether it's required, and default values. This class is typically not instantiated directly but is generated by the SchemaLoader.
Schema Attributes
Description: These attributes are used to decorate your C# model classes to define the configuration schema.
Attributes:
[ConfigSchemaRoot(string mountPath)]: Marks a class as the root of a schema tree and specifies where in the global DOM it should be applied.
[DefaultValue(object value)]: Specifies a default value for a property.
[Range(object min, object max)]: Defines a numeric range constraint.
[Required]: Marks a property as mandatory.
[SchemaAllowedValues(params string[] values)]: Provides a list of allowed string values for a property.
[SchemaRegexPattern(string pattern)]: Defines a regular expression that a string property must match.
Usage Scenarios
Consumer App & Editor App: Both applications will typically reference a shared project (e.g., MyProject.Shared.Contracts) that contains the C# classes decorated with these attributes. This ensures both the editor and the runtime are working from the exact same schema definition.
2. Schema Loading
public class SchemaLoader
Namespace: RuntimeConfig.Core.Schema
Description
This service is responsible for discovering and parsing schema definitions from .NET assemblies. It scans assemblies for classes marked with [ConfigSchemaRoot], reads their properties and attributes, and recursively builds a complete, in-memory SchemaNode tree.
Public API
/// <summary>
/// Initializes a new instance of the SchemaLoader.
/// </summary>
public SchemaLoader();

/// <summary>
/// Gets a read-only dictionary of the loaded root schemas, keyed by their mount path.
/// </summary>
public IReadOnlyDictionary<string, SchemaNode> RootSchemas { get; }

/// <summary>
/// Gets a list of any non-blocking error messages generated during the last load operation.
/// </summary>
public IReadOnlyList<string> ErrorMessages { get; }

/// <summary>
/// Asynchronously scans the specified assemblies and builds the schema trees.
/// This method clears any previously loaded schemas.
/// </summary>
public Task LoadSchemasFromAssembliesAsync(IEnumerable<string> assemblyPaths);

/// <summary>
/// Finds the most specific SchemaNode that corresponds to a given path in the DOM tree.
/// </summary>
public SchemaNode? FindSchemaForPath(string domPath);


Usage Scenarios
Consumer App: A sophisticated consumer app might use SchemaLoader at startup to perform self-validation against its configuration, logging any errors before proceeding. A simple consumer would not use this.
Editor App: The editor will use this service extensively. After loading a project, it will call LoadSchemasFromAssembliesAsync to load all relevant schemas. It will then use FindSchemaForPath constantly to provide context-aware features to the user, such as showing default values, enabling type-specific editors, and validating user input in real-time.
3. Validation
public record ValidationIssue(...)
Namespace: RuntimeConfig.Core.Validation
Description: A simple, immutable record that represents a single validation problem found in the DOM tree.
API
public record ValidationIssue(
    string Path, 
    string Message, 
    ValidationSeverity Severity, 
    string RuleType
);


public enum ValidationSeverity
Namespace: RuntimeConfig.Core.Validation
Values: Error, Warning, Info
public class DomValidator
Namespace: RuntimeConfig.Core.Validation
Description
This service validates a DomNode tree against a corresponding SchemaNode tree. It is a stateless utility that takes the data and the schema as input and produces a list of all discrepancies.
Public API
/// <summary>
/// Validates an entire DOM tree against a corresponding schema tree and returns all issues found.
/// </summary>
/// <param name="domRoot">The root of the DomNode tree to validate.</param>
/// <param name="schemaRoot">The root of the SchemaNode tree to validate against.</param>
/// <returns>A list of all validation issues found throughout the tree.</returns>
public List<ValidationIssue> ValidateTree(DomNode domRoot, SchemaNode schemaRoot);

/// <summary>
/// Validates a single DOM node against its corresponding schema node.
/// </summary>
/// <param name="domNode">The DOM node to validate.</param>
/// <param name="schemaNode">The schema definition for the node.</param>
/// <returns>A list of validation issues specific to this node.</returns>
public List<ValidationIssue> ValidateNode(DomNode domNode, SchemaNode schemaNode);


Usage Scenarios
Consumer App: A sophisticated consumer could use this at startup to ensure its configuration is valid, throwing a fatal exception if any Error-level issues are found.
Editor App: This is a critical service for the editor. After loading or any user modification, the editor will call ValidateTree. It will then pass the resulting list of ValidationIssues to its IValidationHandler implementation, which will in turn update the "Issues" panel in the UI and highlight the invalid fields in the main grid.




### RuntimeConfig.Core API Documentation (Part 5 of 6\)

## DOM Factory & Reporting Interfaces

This document covers two important supporting areas of the library: the DomFactory for creating new DomNode instances, and the reporting interfaces that enable the library to communicate errors and validation results back to a host application in a decoupled way.

### 1\. DOM Factory

#### public class DomFactory

**Namespace**: RuntimeConfig.Core.Factories

Description  
This is a utility class responsible for all low-level creation of DomNode instances. It centralizes the logic for creating nodes from different sources, such as a schema definition or raw user input. This ensures that node creation is consistent and that complex type-deduction logic is not scattered throughout an application.  
**Public API**

/// \<summary\>  
/// Initializes a new instance of the DomFactory.  
/// \</summary\>  
public DomFactory();

/// \<summary\>  
/// Creates a DomNode with a default value based on a schema definition. This is used when  
/// materializing a property that exists in the schema but not yet in the data (e.g., adding  
/// a missing, non-required property via an editor UI).  
/// \</summary\>  
/// \<param name="schema"\>The schema defining the node to create.\</param\>  
/// \<param name="name"\>The name of the node (property name or array index).\</param\>  
/// \<param name="parent"\>The parent DomNode this new node will be attached to.\</param\>  
/// \<returns\>A new DomNode instance (ObjectNode, ArrayNode, or ValueNode) with a default value.\</returns\>  
public DomNode CreateFromSchema(SchemaNode schema, string name, DomNode? parent);

/// \<summary\>  
/// Creates a DomNode by parsing a raw string value from user input. This method intelligently  
/// attempts to parse the string as a number, boolean, object (\`{}\`), or array (\`\[\]\`) before  
/// defaulting to a JSON string.  
/// \</summary\>  
/// \<param name="name"\>The name for the new node.\</param\>  
/// \<param name="rawValue"\>The raw string input from the user.\</param\>  
/// \<param name="parent"\>The parent DomNode this new node will be attached to.\</param\>  
/// \<returns\>A new DomNode instance representing the parsed value.\</returns\>  
public DomNode CreateFromUserInput(string name, string rawValue, DomNode parent);

**Usage Scenarios**

* **Consumer App**: A consumer app would not typically use the DomFactory, as it does not create or modify the DOM.  
* **Editor App**: The editor will use this factory extensively. When a user adds a new property to the configuration tree, the editor's MainViewModel will call either CreateFromSchema (if adding a known property from the schema) or CreateFromUserInput (if adding a new, arbitrary property). This decouples the ViewModel from the complex logic of node instantiation.

### 2\. Reporting Interfaces

**Namespace**: RuntimeConfig.Core.Reporting

Description  
These interfaces define the contracts for how the library reports information back to its host application. By using interfaces, the library remains completely decoupled from any specific UI framework (like WPF's MessageBox) or logging library (like NLog or Serilog). The host application provides concrete implementations of these interfaces.

#### public interface IErrorReporter

**Description**: A contract for reporting fatal or blocking errors that prevent an operation from continuing (e.g., a file not found during loading).

public interface IErrorReporter  
{  
    /// \<summary\>  
    /// Reports a significant error to the host application.  
    /// \</summary\>  
    /// \<param name="title"\>A brief title for the error (e.g., "File Load Failed").\</param\>  
    /// \<param name="message"\>A detailed message explaining the error.\</param\>  
    void ReportError(string title, string message);  
}

#### public interface IValidationHandler

**Description**: A contract for processing the list of validation issues generated by the DomValidator.

public interface IValidationHandler  
{  
    /// \<summary\>  
    /// Handles a new set of validation issues. The implementation should typically  
    /// clear any old issues and display the new ones.  
    /// \</summary\>  
    /// \<param name="issues"\>An enumeration of all validation issues found in the tree.\</param\>  
    void HandleIssues(IEnumerable\<ValidationIssue\> issues);  
}

#### public interface IProgressReporter

**Description**: A contract for providing progress updates during long-running operations.

public interface IProgressReporter  
{  
    /// \<summary\>  
    /// Reports progress on a long-running operation.  
    /// \</summary\>  
    /// \<param name="message"\>A message describing the current step (e.g., "Loading layer 'Base'...").\</param\>  
    /// \<param name="percentage"\>A value from 0.0 to 1.0 indicating the overall progress.\</param\>  
    void ReportProgress(string message, double percentage);  
}

**Usage Scenarios**

* **Consumer App**: A simple consumer might not provide any implementations. A more sophisticated service might provide an implementation of IErrorReporter that logs errors to a file using its preferred logging framework.  
* **Editor App**: The editor will provide concrete implementations for these interfaces, likely within its MainViewModel or as separate helper classes.  
  * Its IErrorReporter will show a MessageBox to the user.  
  * Its IValidationHandler will update an ObservableCollection that is bound to the "Issues" panel in the UI.  
  * Its IProgressReporter could update a progress bar in the status bar during a lengthy project load.






  RuntimeConfig.Core API Documentation (Part 6 of 6)
Architectural Summary & Usage Examples
This final document provides a high-level overview of the RuntimeConfig.Core library's architecture and demonstrates how its components are used in practice by both a simple runtime consumer and a complex editor application.
1. Architectural Summary
The RuntimeConfig.Core library is designed around a strict Separation of Concerns. Its architecture can be understood as two distinct "worlds" built upon a shared foundation.
The Shared Foundation: At its core, the library provides a set of foundational, stateless components:
DOM Classes (DomNode, etc.): The universal language for representing configuration data.
Shared Services (LayerProcessor, DomMerger): The canonical, deterministic logic for processing and merging configuration data.
Utilities (DomFactory, JsonDomSerializer, etc.): Generic, reusable tools for working with the foundational types.
The "Runtime World": This is for applications that need to consume the final, effective configuration. It is enabled by the RuntimeDomTree and the IRuntimeDomProvider implementations. This world is designed to be simple, fast, and stateless. It abstracts away all the complexity of layers and source files, delivering a single, clean configuration tree.
The "Editor World": This is for applications that need to manage the configuration sources. The editor application uses the same shared foundation but builds its own complex, stateful logic on top of it. It orchestrates the library's services to load individual layers, track value origins, manage UI state, and provide a rich, interactive experience.
By sharing the foundational logic (especially LayerProcessor and DomMerger), we guarantee that the configuration state viewed by the editor is identical to the state consumed by the runtime application.
2. Usage Example: The Runtime Consumer Application
This example shows how a simple console application would use the library to load a cascading project and read a configuration value.
Goal: Get the final, effective configuration with minimal code and complexity.
using RuntimeConfig.Core;
using RuntimeConfig.Core.Models;
using RuntimeConfig.Core.Providers;
using RuntimeConfig.Core.Querying;

public class Program
{
    public static async Task Main(string[] args)
    {
        // 1. Define the layers for the project. In a real app, this might come
        //    from a simple project definition file.
        var layers = new List<LayerDefinition>
        {
            new LayerDefinition("Base", "path/to/config/1_base"),
            new LayerDefinition("Production", "path/to/config/2_production")
        };

        // 2. Instantiate the runtime-focused provider, giving it the layer definitions.
        var provider = new CascadingJsonProvider(layers);

        // 3. Instantiate the main RuntimeDomTree.
        var configTree = new RuntimeDomTree();

        // 4. Register the provider at a mount point (e.g., "app").
        configTree.RegisterProvider("app", provider);

        // 5. Call RefreshAsync(). This single call triggers all the library's internal logic:
        //    - The provider uses LayerProcessor to process and merge each layer.
        //    - The provider uses DomMerger to combine the layers.
        //    - The RuntimeDomTree resolves all $ref references.
        await configTree.RefreshAsync();

        // 6. Get the simple query interface and retrieve the final, effective value.
        //    The application has no knowledge of the original layers or files.
        DomQuery query = configTree.Query();
        string dbHost = query.Get<string>("app/database/host");

        Console.WriteLine($"Database host is: {dbHost}");
    }
}


3. Usage Example: The Editor Application
This example shows how the editor's MainViewModel would use the library's components to load the same project but retain all the rich detail needed for the UI.
Goal: Load all data with full context of layers, source files, and value origins for editing.
// Inside the WpfEditorGemini.ViewModels.MainViewModel class

// The editor holds instances of the library's services.
private readonly LayerProcessor _layerProcessor;
private readonly DomValidator _domValidator;
private readonly SchemaLoader _schemaLoader;

// The editor has its own stateful model for a layer.
private List<EditorCascadeLayer> _editorLayers;

public async Task LoadProject(string projectFilePath)
{
    // The editor uses its own project loading logic to get the layer definitions.
    var layerDefinitions = LoadProjectDefinitions(projectFilePath);

    _editorLayers = new List<EditorCascadeLayer>();

    // 1. Process each layer individually using the shared LayerProcessor.
    foreach (var def in layerDefinitions)
    {
        // The editor calls the same service as the runtime provider.
        LayerProcessingResult result = _layerProcessor.Process(def.BasePath);

        // 2. But unlike the runtime, the editor uses the *entire* rich result
        //    to build its own stateful model for the UI.
        var editorLayer = new EditorCascadeLayer
        {
            Name = def.Name,
            MergedDom = result.MergedRootNode,
            SourceFiles = result.LoadedSourceFiles,
            ValueOrigins = result.ValueOrigins, // CRITICAL: For knowing where to save changes
            Errors = result.Errors,
            IsDirty = false
        };
        _editorLayers.Add(editorLayer);
    }
    
    // 3. Load schemas using the library's SchemaLoader.
    await _schemaLoader.LoadSchemasFromAssembliesAsync(...);

    // 4. To display a merged preview, the editor uses the shared DomMerger
    //    on its own list of layer models.
    ObjectNode previewRoot = BuildPreviewForUI();
    
    // 5. Validate the preview tree against the loaded schema.
    var issues = _domValidator.ValidateTree(previewRoot, _schemaLoader.GetRootSchema());
    
    // 6. Update the UI with the loaded data and validation issues.
    UpdateUI(previewRoot, issues);
}

private ObjectNode BuildPreviewForUI()
{
    var previewRoot = new ObjectNode("$root", null);
    foreach(var layer in _editorLayers)
    {
        // It uses the same canonical merge logic.
        DomMerger.MergeInto(previewRoot, layer.MergedDom);
    }
    return previewRoot;
}





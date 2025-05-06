# Configuration System Class Reference

This document provides a comprehensive and detailed reference to all classes utilized in the configuration system. It outlines their purpose, design responsibilities, exposed public methods, and architectural benefits. The document reflects the most complete and up-to-date design that underpins the runtime, editing tools, viewmodels, schema reflection, and configuration data processing layers.

---

## Class Index

* DomNode (abstract)
* ObjectNode
* ArrayNode
* LeafNode
* RefNode
* DomNodeViewModel
* ObjectNodeViewModel
* ArrayNodeViewModel
* RefNodeViewModel
* EditorWorkspace
* Json5CascadeEditorContext
* FlatJsonEditorContext
* Json5SourceFile
* DomEditAction
* DomReferenceResolver
* DomValidatorService
* IErrorStatusProvider
* ISchema (interface)
* ObjectSchema, StringSchema, IntegerSchema, FloatSchema, BooleanSchema, ListSchema, DictionarySchema
* SchemaFromType
* RuntimeSchemaCatalog

---

## DomNode (abstract)

**Purpose**: Provides the foundational structure for any element in the runtime or editing DOM tree. Abstract base for all specialized node types.

**Responsibilities**:

* Store common metadata shared by all nodes, such as their name and parent reference.
* Derive the absolute path in the tree for reference and lookup.
* Enable unified tree navigation and generic handling.
* Act as the anchor point for serialization and traversal routines.

**Public Members**:

* `string Name`
* `DomNode? Parent`
* `string GetAbsolutePath()`

**Benefits**:

* Simplifies path-based access and referencing.
* Essential for building features like `$ref` resolution and UI tracing.
* Allows polymorphic traversal regardless of node type.

**Usage Example**:

```csharp
var path = someNode.GetAbsolutePath();
if (path == "config/env1/network/ip") {
    Console.WriteLine("Found matching config node");
}
```

DomNode serves as the backbone of the tree structure and provides a unified base for features such as flat path maps, validator passes, and undo support.
**Purpose**: Provides the foundational structure for any element in the runtime or editing DOM tree. Abstract base for all specialized node types.

**Responsibilities**:

* Store common metadata shared by all nodes, such as their name and parent reference.
* Derive the absolute path in the tree for reference and lookup.
* Enable unified tree navigation and generic handling.

**Public Members**:

* `string Name`
* `DomNode? Parent`
* `string GetAbsolutePath()`

**Benefits**:

* Simplifies path-based access and referencing.
* Essential for building features like `$ref` resolution and UI tracing.

---

## ObjectNode

**Purpose**: Implements dictionary-style mapping in the DOM, equivalent to JSON objects.

**Responsibilities**:

* Manage a mapping of named child nodes.
* Serve as an interior container for structured hierarchical config.

**Public Members**:

* `Dictionary<string, DomNode> Children`
* `DomNode? GetChild(string key)`
* `void AddChild(DomNode child)`

**Benefits**:

* Core to representing structured nested config content.
* Compatible with cascading and deep merge logic.

---

## ArrayNode

**Purpose**: Represents an ordered list of DOM nodes, mapping to JSON arrays.

**Responsibilities**:

* Maintain list of elements with guaranteed index-based order.
* Allow access to children using index-based paths.

**Public Members**:

* `List<DomNode> Items`

**Benefits**:

* Supports configuration structures requiring sequence or enumeration.
* Essential for structured lists, node groups, or table-like structures.

---

## LeafNode

**Purpose**: Terminal node holding a primitive value (string, int, boolean, etc.)

**Responsibilities**:

* Encapsulate a single value as a `JsonElement`
* Be editable and validate against schema metadata

**Public Members**:

* `JsonElement Value`

**Benefits**:

* Simplifies handling of scalar values across UI and runtime.
* Can participate in schema-bound editing and validation.

---

## RefNode

**Purpose**: Special node type that represents a symbolic link to another path.

**Responsibilities**:

* Store target path for `$ref` resolution
* Be expanded/resolved at runtime and/or editor preview

**Public Members**:

* `string TargetPath`

**Benefits**:

* Enables value reuse, redirection, aliasing across the config tree.
* Allows centralized updates and modularization.

---

## DomNodeViewModel

**Purpose**: UI adapter wrapping a `DomNode` with editing and interaction state.

**Responsibilities**:

* Track UI-specific data such as editability, dirty state, schema annotations
* Interface between data model and UI renderer
* Trigger and reflect updates from schema, validation, and undo history

**Public Members**:

* `DomNode Node`
* `bool IsEditable`
* `bool IsDirty`
* `ISchema? AttachedSchema`
* `void MarkDirty()`
* `void ClearDirty()`

**Benefits**:

* Makes the underlying DOM interactive, editable, and schema-aware for GUI tools.
* Enables smart display and change tracking.
* Reflects schema hints like range sliders, units, and formatting options.

**Example UI Features Powered**:

* Coloring changed nodes red
* Adding tooltips for format errors
* Inline sliders for `[Range]` values
* Read-only flags for runtime mounts

This class enables precise tracking and rich display support across interactive configuration tools.
**Purpose**: UI adapter wrapping a `DomNode` with editing and interaction state.

**Responsibilities**:

* Track UI-specific data such as editability, dirty state, schema annotations
* Interface between data model and UI renderer

**Public Members**:

* `DomNode Node`
* `bool IsEditable`
* `bool IsDirty`
* `ISchema? AttachedSchema`
* `void MarkDirty()`
* `void ClearDirty()`

**Benefits**:

* Makes the underlying DOM interactive, editable, and schema-aware for GUI tools.
* Enables smart display and change tracking.

---

## ObjectNodeViewModel / ArrayNodeViewModel

**Purpose**: Specialized viewmodels that expose collections of children for rendering.

**Responsibilities**:

* Store and provide access to child `DomNodeViewModel` instances

**Public Members**:

* `List<DomNodeViewModel> Children`

**Benefits**:

* Forms the backbone of tree view hierarchies
* Enables full UI traversal and binding

---

## RefNodeViewModel

**Purpose**: Viewmodel for `$ref` nodes that resolves and previews the target.

**Responsibilities**:

* Resolve references recursively and prevent cyclic loops
* Present resolved preview value for user inspection

**Public Members**:

* `DomNode? ResolvedTargetNode`
* `JsonElement? ResolvedPreviewValue`
* `bool HasCycleOrError`

**Benefits**:

* Enables "Go to reference" navigation in UI
* Detects and signals invalid or cyclic references

---

## EditorWorkspace

**Purpose**: Central orchestrator for all editor-side providers and DOM trees.

**Responsibilities**:

* Manage registration of mount points and cascade folders
* Build full composite DOM and its viewmodel
* Coordinate context-specific behaviors and schema injection
* Track and reload specific contexts during development

**Public Members**:

* `void RegisterContexts(...)`
* `DomNode MasterDomRoot`
* `DomNodeViewModel MasterViewModelRoot`

**Benefits**:

* Enables multi-source editing with consistent merging and tracking
* Provides access to unified live view of the editable configuration

**Example**:

```csharp
var workspace = new EditorWorkspace();
workspace.RegisterContexts(contexts, schemaCatalog);
var tree = workspace.MasterViewModelRoot;
```

EditorWorkspace is the outer shell for editing state, centralizing UI-layer synchronization and source tracking across independently mounted configuration branches.
**Purpose**: Central orchestrator for all editor-side providers and DOM trees.

**Responsibilities**:

* Manage registration of mount points
* Build full composite DOM and its viewmodel
* Coordinate context-specific behaviors and schema injection

**Public Members**:

* `void RegisterContexts(...)`
* `DomNode MasterDomRoot`
* `DomNodeViewModel MasterViewModelRoot`

**Benefits**:

* Enables multi-source editing with consistent merging and tracking
* Provides access to unified live view of the editable configuration

---

## Json5CascadeEditorContext

**Purpose**: Provider that handles multiple cascade levels of editable JSON config.

**Responsibilities**:

* Load all cascade source files and flatten their keys
* Resolve effective values by cascade level
* Apply local edits and track undo/redo stacks

**Public Members**:

* `string MountPath`
* `DomNode SubtreeRoot`
* `JsonElement? GetEffectiveValue(string path)`
* `void ApplyEdit(...)`, `Undo()`, `Redo()`

**Benefits**:

* Enables sophisticated layered editing (e.g., base/site/local)
* Powers origin tracking and live merge previews

---

## FlatJsonEditorContext

**Purpose**: Simplified non-cascading provider useful for small or test configs.

**Responsibilities**:

* Load a single JSON file
* Provide one complete `DomNode` tree

**Benefits**:

* Lightweight and ideal for test setups, plugins, or diagnostics

---

## Json5SourceFile

**Purpose**: Represents a parsed JSON file and its flattened path-value map.

**Responsibilities**:

* Load content into memory and flatten the structure
* Preserve errors, comments, and original document

**Public Members**:

* `string RelativePath`
* `Dictionary<string, JsonElement> FlatMap`
* `JsonDocument Root`

**Benefits**:

* Enables precise roundtrip editing and tracking
* Basis for cascade merging and change detection

---

## DomEditAction

**Purpose**: Records a reversible change to a path for undo/redo history.

**Responsibilities**:

* Capture path, old value, and new value

**Public Members**:

* `DomEditAction GetInverse()`

**Benefits**:

* Enables multi-level undo/redo history
* Maintains atomicity of user edits

---

## DomReferenceResolver

**Purpose**: Resolves `$ref` nodes and their final targets recursively.

**Responsibilities**:

* Walk the tree from the root
* Follow `$ref` chains and detect cycles

**Public Members**:

* `DomNode? ResolveFinalTarget(DomNode root, RefNode refNode)`

**Benefits**:

* Abstracts complex traversal and validation logic

---

## DomValidatorService

**Purpose**: Validates a DOM tree against schema constraints and basic integrity.

**Responsibilities**:

* Walk every node in a subtree
* Check required fields, types, and constraints

**Public Members**:

* `List<IErrorStatusProvider> Validate(DomNode root)`

**Benefits**:

* Foundational building block for UI validation and feedback

---

## IErrorStatusProvider

**Purpose**: Interface that exposes error diagnostics for viewmodel display.

**Responsibilities**:

* Report presence of validation issues
* Provide error message

**Public Members**:

* `bool HasError`
* `string? ErrorMessage`

**Benefits**:

* Can be implemented by any node with rule violations
* UI can render tooltips, icons, highlights based on this

---

## ISchema (interface) & Concrete Types

**Purpose**: Represents validation rules for a specific config subtree.

**Concrete Implementations**:

* `ObjectSchema`, `StringSchema`, `IntegerSchema`, `FloatSchema`, `BooleanSchema`
* `ListSchema`, `DictionarySchema`

**Optional Schema Fields**:

* `double? Min`, `Max`
* `string? Unit`
* `string? Format`
* `string[]? EnumValues`

**Benefits**:

* Drives rich UI widgets and config validators
* Declaratively models domain expectations
* Informs editor rendering without coupling to UI toolkit

**Editor Integration**:

* Range values → slider inputs
* Units → suffix labels or tooltips
* Format → input masks or regex filters
* Enum → dropdown choices

**Example**:

```csharp
public class NetworkConfig {
    [Range(1, 65535)]
    [Unit("port")]
    public int Port { get; set; }

    [Format("ipv4")]
    public string IpAddress { get; set; }
}
```

Used with `SchemaFromType`, this model gives complete introspection for schema-aware editing.
**Purpose**: Represents validation rules for a specific config subtree.

**Concrete Implementations**:

* `ObjectSchema`, `StringSchema`, `IntegerSchema`, `FloatSchema`, `BooleanSchema`
* `ListSchema`, `DictionarySchema`

**Optional Schema Fields**:

* `double? Min`, `Max`
* `string? Unit`
* `string? Format`
* `string[]? EnumValues`

**Benefits**:

* Drives rich UI widgets and config validators
* Declaratively models domain expectations

---

## SchemaFromType

**Purpose**: Uses reflection to generate a schema tree from annotated C# classes.

**Responsibilities**:

* Discover fields and types
* Apply `[Range]`, `[Unit]`, `[Format]`, `[Required]`

**Public Members**:

* `ISchema Build(Type t)`

**Benefits**:

* Single source of truth for schema and application model
* Simplifies maintenance and eliminates duplication

---

## RuntimeSchemaCatalog

**Purpose**: Dynamically discovers and loads schemas from external DLLs.

**Responsibilities**:

* Scan assemblies for `[ConfigSchema("mount")]`
* Extract classes and convert to `ISchema`

**Public Members**:

* `void LoadFromAssemblies(string[] paths)`
* `ISchema? GetSchemaForMount(string path)`

**Benefits**:

* Enables plugin/extensible model-driven configuration
* Keeps core editor decoupled from concrete config types

---

## Final Notes

This document defines the complete type system for managing configuration as structured DOM data — both at runtime and in the editor. These types are foundational for cascading, validation, schema enforcement, and interactive editing with undo, ref resolution, and live schema introspection.

**Example Workflow**:

1. User edits value of `config/env1/network/port`
2. Editor detects change and marks node as dirty
3. Validation runs: port must be in `[1, 65535]`
4. ViewModel reflects any errors or schema warnings
5. Undo stack updated; user can revert edit
6. Saving flushes only dirty paths back to source JSON

These capabilities are enabled by the precise cooperation of schema reflection, cascade tracking, and a reactive DOM model tailored to UI feedback.
This document defines the complete type system for managing configuration as structured DOM data — both at runtime and in the editor. These types are foundational for cascading, validation, schema enforcement, and interactive editing with undo, ref resolution, and live schema introspection.

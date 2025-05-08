# Configuration System Architecture

This document provides a comprehensive and in-depth description of the configuration system architecture, detailing both runtime and edit-time behavior, data models, provider infrastructure, design decisions, validation workflows, schema handling, and more. It reflects the entire design history and rationale behind each choice, and includes concrete examples to illustrate the major concepts.

---

## Table of Contents

1. Introduction and Context
2. The Master DOM Tree
3. Provider Types and Mounting
4. Cascading Configuration Principles
5. Reference Resolution with \$ref
6. Schema-Based Validation
7. Editor Architecture
8. Runtime Architecture
9. Undo/Redo and Dirty Tracking
10. Hybrid Workflows and BSON Output
11. Summary of Design Decisions
12. Example Use Cases
13. Schema-Driven Defaults and Required Fields
14. Schema Metadata Architecture

---

## 1. Introduction and Context

This configuration system is designed to support both **static structured configuration** (editable, versioned, layered) and **dynamic runtime introspection** (real-time metrics, application state snapshots). The architecture supports unified introspection across both sources, but clearly separates editable content from live data to simplify safety, validation, and UI interactions.

The system solves problems across multiple domains:

* Management of complex layered config setups across different machines and environments
* Real-time inspection of application state, health, and metrics
* Integration with static JSON5 configuration sources, including schema-bound editing
* Unified access pattern and tooling for both edit-time and runtime representations
* Resolution of symbolic references (`$ref`) in static configs

Design priorities include:

* **Robust data model**: hierarchical, typed, extensible
* **Separation of concerns**: sources/providers vs merged view
* **Full undo/redo support** for any editable field
* **Cross-platform editing** with modern UI tooling
* **Schema-based validation and hints**
* **Support for hot-reload, build-time merge, and runtime views**

This document reflects not only the final structure but also the reasoning behind it—what trade-offs were considered, how extensibility was preserved, and why schema introspection is preferred over hand-maintained validation trees.

In the following sections, each aspect will be described in detail, starting with the shared master DOM tree that acts as the backbone of the system.


## 2. The Master DOM Tree

The heart of the configuration system is a shared, unified data structure known as the **Master DOM Tree**. This tree exists in both the runtime and editor applications and provides a consistent structure for representing and accessing configuration data, regardless of where it comes from or how it is used.

### Hierarchical Representation

The tree is composed of nodes derived from a common abstract base class `DomNode`. The key node types include:

* `ObjectNode`: Represents a key-value map (analogous to a JSON object)
* `ArrayNode`: Represents an ordered list of values
* `LeafNode`: Represents a terminal value (string, number, bool, etc.)
* `RefNode`: A special node representing a symbolic `$ref` to another part of the tree

Each node tracks its name and parent, allowing construction of full absolute paths like `config/env1/network/ip`. This pathing is essential for:

* Config lookups
* Schema binding
* Reference resolution
* Dirty tracking and undo

### Immutable Logical Structure, Mutable State

The master DOM tree is **logically immutable** in that its structure is defined by the provider content and mount path definitions. However, the **values of nodes can be modified**, and these modifications are tracked at the viewmodel layer (see Section 7).

### Multi-Provider Integration

The DOM tree may be composed of multiple independent providers. Each one is mounted at a known root (e.g. `config/env1` or `metrics/live`). The mounting is non-overlapping, allowing independent teams or tools to manage isolated parts of the tree.

```
root
├── config
│   └── env1
│       └── network
│           └── ip
└── metrics
    └── live
        └── cpu
```

### Querying the DOM

Consumers access data via absolute paths:

```csharp
int cpu = dom.Get<int>("metrics/live/cpu/usage");
string ip = dom.Get<string>("config/env1/network/ip");
```

This querying supports strongly typed deserialization, and in editor mode, will also attach schema information if available.

### Ref Nodes

`RefNode`s are only allowed in editable, static config sources. They store a `$ref` string (absolute path) and are resolved recursively. In runtime, all refs are resolved and inlined, so `RefNode` does not appear in the runtime DOM.

Example:

```json
{
  "app": {
    "host": { "$ref": "config/hosts/primary/ip" }
  }
}
```

### Editing Behavior

In editor mode, changes do not directly alter the DOM node but are wrapped in a `DomNodeViewModel`, which tracks `IsDirty`, `IsEditable`, and `AttachedSchema`. This keeps runtime-safe logic isolated from UI behavior.

### Tree Construction

The DOM tree is constructed by loading providers (e.g. cascading JSON5, SQL snapshots) and assigning them to mount points. Each provider contributes a subtree that is inserted beneath a logical root, respecting both:

* Mount point path
* Internal file paths inside provider (e.g. `network.json` → `network/`)



## 3. Provider Types and Mounting

The configuration system is built on a **modular provider architecture**. Each provider is responsible for supplying a **subtree** of the DOM, typically under a clearly defined mount point. This structure supports both static (editable) and dynamic (runtime-only) data sources.

### 3.1 Provider Responsibilities

Each provider implements one or more of the following roles:
- **Load and parse configuration data**
- **Construct a corresponding subtree of `DomNode` instances**
- **Respond to reload or hot-reload triggers (if applicable)**
- **Optionally track editing metadata (if editable)**

Each provider contributes content **only under its assigned mount path**, ensuring isolation.

### 3.2 Types of Providers

#### A. Static Config Providers
These load structured config data from file sources like JSON5. Their content:
- Is editable
- Participates in cascade merging
- May include `$ref` references

Examples:
- `Json5CascadeEditorContext`: multi-folder cascade of JSON5 files
- `FlatJsonEditorContext`: flat single-file config (e.g. plugin)

#### B. Dynamic Providers
These expose runtime-only data. Common sources:
- Metrics
- Health states
- Agent status

They are:
- **Read-only**
- **Transient (in-memory)**
- Typically refreshed live

Examples:
- `LiveMetricsProvider`
- `AgentStatusProvider`

#### C. Pre-Build Static Providers
These may load snapshots of external data during the build phase. They are:
- Read-only
- Not reloadable
- Used for `$ref` resolution or snapshotting

Examples:
- SQL table export
- Shared global config server

### 3.3 Mounting Providers into the DOM

Each provider declares a mount path under the global DOM, such as:
- `config/env1`
- `metrics/live`
- `shared/global`

Providers are mounted during:
- Editor initialization (`EditorWorkspace.RegisterContexts`)
- Runtime startup (`RuntimeDomBuilder.RegisterStatic/Runtime`)

Mounting paths must be:
- Absolute
- Non-overlapping
- Stable for the session

### 3.4 Mounting Example

```csharp
var mounts = new Dictionary<string, List<string>> {
    ["config/env1"] = new List<string> { "base", "site", "local" },
    ["config/env2"] = new List<string> { "env2/base", "env2/local" },
    ["metrics/live"] = new List<string> { } // dynamic
};
```

This will produce:
```
root/
├── config/
│   ├── env1/
│   └── env2/
└── metrics/
    └── live/
```

### 3.5 Isolation and Non-Overlapping Constraint

Providers are **not allowed to overlap**. This means no two providers can both contribute to the same DOM subtree. This rule ensures:
- Consistency
- Debuggability
- Predictable reload behavior

### 3.6 Provider Lifecycle

Each provider supports a simple lifecycle:
- `Load()`: initial load from source
- `Reload()`: (if hot reload supported)
- `Dispose()`: cleanup

The editor invokes these in a controlled way, preserving mount order and context.

This design enables highly modular config systems, with strong separation between:
- Static vs dynamic data
- Build-time vs runtime concerns
- Editable vs readonly branches


## 4. Cascading Configuration Principles

The cascading configuration model allows multiple layers of configuration to override or extend each other in a structured, hierarchical way. This supports use cases like environment inheritance (e.g. base → staging → local) and localized overrides.

### 4.1 Cascade Layers

Each cascade is a **list of folders**, where each folder represents a configuration level:
- `base/`: common defaults
- `site/`: deployment-specific overrides
- `local/`: local developer or machine-specific values

These folders are defined per mount and ordered from lowest (least specific) to highest (most specific).

Example cascade registration:
```csharp
["config/env1"] = new List<string> { "base", "site", "local" }
```

### 4.2 File Discovery

Each level in the cascade may contain subfolders and multiple `.json` files. All files are loaded and interpreted as JSON5. Their relative path (from the root of that level) determines their position in the DOM.

File paths:
```
base/network/ip.json
site/network/ip.json
local/network/ip.json
```

Resulting DOM path:
```
config/env1/network/ip
```

### 4.3 Merge Logic

The merging strategy is **deep and structural**:
- Objects are merged recursively
- Arrays are replaced entirely (not merged element-wise)
- Leaf values are overridden at the topmost layer that defines them

Example:
```json
// base
{ "options": { "timeout": 10, "retries": 3 } }

// site
{ "options": { "timeout": 30 } }

// result
{ "options": { "timeout": 30, "retries": 3 } }
```

### 4.4 Source Tracking

The editor tracks **where each value came from**:
- The cascade level (folder name)
- The source file (e.g. `network/ip.json`)
- The merge origin (which layer provided a given field)

This allows UI features like:
- "Defined in base/site/local"
- "Override at higher level"
- "Remove override" suggestions

### 4.5 Editing Behavior

When editing:
- New keys are always written to the topmost (most specific) layer
- Removals occur by inserting a special delete marker or omitting the key
- Values are saved to the appropriate file corresponding to their path and cascade level

The user is shown the origin of each value and can choose to override it explicitly.

### 4.6 Folder Structure and Paths

All relative paths under each cascade level are added under the provider’s mount path.

Example:
```
cascade mount path: config/env1
folder: base/system/agent.json
→ final path: config/env1/system/agent
```

The final DOM reflects the logical config hierarchy, regardless of how many files or folders exist physically.

### 4.7 JSON5 as the Source Format

All files are parsed as JSON5, which allows for:
- Comments
- Trailing commas
- Relaxed quoting

The system uses JSON5 parsers for source load and formatting, but converts all merged content into canonical JSON for further processing and output.



## 5. Reference Resolution with $ref

Symbolic references using `$ref` enable configurations to point to values elsewhere in the DOM. This allows reuse of shared definitions, centralization of global values, and improved maintainability.

### 5.1 Reference Format

A reference node is represented as:
```json
{ "$ref": "absolute/path/to/target" }
```

The path must:
- Be absolute from the root of the master DOM
- Refer only to static data (no dynamic/live providers)
- Be resolvable at build time

Example:
```json
{ "dbHost": { "$ref": "shared/hosts/database/ip" } }
```

### 5.2 Reference Scope

References are supported only:
- In static config providers (e.g., JSON5 cascade)
- Before BSON generation
- During editing and source validation

They are **not** allowed in:
- Runtime DOMs
- Dynamic/live providers
- Already-built BSON

This enforces safety, immutability, and performance at runtime.

### 5.3 Reference Resolution

During BSON export, all `$ref` nodes are:
1. Resolved recursively to their target values
2. Replaced with a copy of the resolved value
3. Subject to cycle detection

Example input:
```json
{
  "web": {
    "host": { "$ref": "shared/hosts/web/ip" }
  }
}
```

If `shared/hosts/web/ip = "10.0.0.1"`, the result will be:
```json
{
  "web": {
    "host": "10.0.0.1"
  }
}
```

### 5.4 Cycles and Errors

The resolver detects and prevents cycles:
```json
a = { "$ref": "b" }
b = { "$ref": "a" }
```

Results in a validation error:
```
Cycle detected: a → b → a
```

Other validation errors include:
- Invalid path
- Missing target
- Reference to dynamic branch

These are shown in the editor during editing and blocked during BSON build.

### 5.5 Editor Behavior

In the editor:
- `$ref` nodes are represented using `RefNodeViewModel`
- The UI shows both:
  - Reference path
  - Resolved preview value
- Navigation allows “Go to Reference Target”
- Inline errors are shown for unresolved or invalid paths

This enables intuitive interaction and safe editing.

### 5.6 Reference Usage Examples

Use case: shared IPs across services:
```json
{
  "db": { "ip": { "$ref": "shared/hosts/database/ip" } },
  "cache": { "ip": { "$ref": "shared/hosts/redis/ip" } }
}
```

Use case: indirect port settings:
```json
{
  "service": {
    "port": { "$ref": "settings/defaultPort" }
  }
}
```

### 5.7 Final Notes

- `$ref` resolution happens only during BSON export
- Runtime sees only fully resolved values
- Editing tools preserve the `$ref` in source
- The design ensures references are safe, fast, and analyzable





## 6. Schema-Based Validation

Schema-based validation enables the editor and runtime systems to enforce structural and value constraints on the configuration data. Instead of relying on handwritten validation rules, this architecture uses C# class definitions as the canonical source of truth for data structure, types, allowed ranges, and formatting hints.

This approach ensures consistency between the application's expected config structures and what the editor validates and displays.

### 6.1 Schema Source Format

Schemas are defined using annotated C# classes. These may include:

* Basic property types (int, string, bool, etc.)
* Complex nested objects
* Collections (`List<T>`, `Dictionary<string, T>`)
* Optional metadata via attributes:

  * `[Range(min, max)]`
  * `[Unit("MB")]`
  * `[Format("hostname")]`

Example:

```csharp
public class NodeConfig {
    public string Name { get; set; }

    [Range(100, 10000)]
    [Unit("MB")]
    public int MemoryLimit { get; set; }

    [Format("hostname")]
    public string Host { get; set; }
}
```

### 6.2 Schema Discovery and Loading

A runtime component called `RuntimeSchemaCatalog` scans assemblies (DLLs/EXEs) for types annotated with `[ConfigSchema("mountPath")]`. This identifies the root path in the DOM to which a schema applies.

The schemas are then mapped by mount path and transformed into internal `ISchema` trees.

```csharp
var catalog = new RuntimeSchemaCatalog();
catalog.LoadFromAssemblies(["schemas.dll"]);
```

Each schema class becomes a tree of schema nodes:

* `ObjectSchema`: contains properties
* `IntegerSchema`, `StringSchema`, etc.
* `ListSchema` and `DictionarySchema`

These trees are attached during viewmodel construction.

### 6.3 Schema Attachment

When `EditorWorkspace` builds the viewmodel tree, it checks the mount path of each provider. If a matching schema tree is found, it attaches metadata to each corresponding `DomNodeViewModel`:

* `Range` → used for sliders or numeric limits
* `Unit` → suffix in UI
* `Format` → input filters or UI hints

### 6.4 Live Validation

A validation service walks the DOM and compares values to schema expectations:

* Missing required fields
* Type mismatches
* Values out of range
* Invalid enum or format values

Each issue becomes an `IErrorStatusProvider` with a flag and error message, which the UI can use to highlight or display tooltips.

```csharp
var errors = DomValidatorService.Validate(viewModel.Root);
```

### 6.5 Display Features in the UI

Schemas enhance the UI:

* **Numeric input fields** show sliders based on `[Range]`
* **Input suffixes** like MB, seconds, or ms based on `[Unit]`
* **Field descriptions**, tooltips, or regex validation based on `[Format]`

These help ensure that users enter correct values the first time, reducing bugs and misunderstandings.

### 6.6 Schema Reuse and Coherence

By deriving schemas from real application types, the system ensures:

* The schema matches actual application code
* There's no duplication or drift between validator and model
* Updates to the application model automatically reflect in the editor after reload

### 6.7 Limitations and Extensions

This approach does not yet support:

* Conditional constraints (e.g. "this field is required if X is true")
* Schema fragments or \$ref reuse inside schema trees (as in JSON Schema)

But it is intentionally simpler to avoid over-complexity and improve maintainability.

---

## 7. Editor Architecture

The editor is a fully interactive user-facing application that enables browsing, editing, validating, and saving structured configurations. It combines the shared DOM tree, schema metadata, and change tracking into a responsive, schema-aware editing surface.

The editor is built around a **core data model + reactive UI layer** architecture. The main components are:

* A unified master DOM tree (shared with runtime)
* Cascading and reference-resolved providers
* ViewModels per node (`DomNodeViewModel`)
* Context-specific editors (e.g. `Json5CascadeEditorContext`)
* Schema introspection with visual hints
* Per-context undo/redo stacks
* Validation overlays and dirty tracking

### 7.1 EditorWorkspace

The `EditorWorkspace` coordinates the entire editing session. It:

* Registers providers under mount points
* Merges all DOM branches
* Builds and caches the full viewmodel tree
* Tracks per-context change state

```csharp
var workspace = new EditorWorkspace();
workspace.RegisterContexts(mounts, schemaCatalog);
```

### 7.2 IMountedDomEditorContext

Each mounted editor context wraps a config provider and exposes editing interfaces:

* `Json5CascadeEditorContext` → cascaded JSON source folders
* `FlatJsonEditorContext` → single file inputs

Responsibilities:

* Load and flatten all config files
* Track dirty paths
* Store undo/redo history
* Rebuild subtrees on source file change

### 7.3 DomNodeViewModel

Each node in the DOM tree has an associated `DomNodeViewModel`:

* Holds editability flag
* Tracks `IsDirty`
* Provides schema metadata for rendering
* Triggers live validation

Each object/array also exposes `Children: List<DomNodeViewModel>`

For `$ref` nodes, the system uses `RefNodeViewModel`, which adds:

* `ResolvedTargetNode`
* `ResolvedPreviewValue`
* Reference cycle detection

### 7.4 Editing and Preview Features

When a user edits a value:

* The new value is stored in the DOM
* Marked as dirty via `MarkDirty()`
* An undo action is pushed
* The schema validator runs

The UI can show:

* Inline error indicators (from `IErrorStatusProvider`)
* Hover tooltips with schema metadata
* Reference previews for symbolic links
* Slider widgets, unit suffixes, format hints

### 7.5 Context-Aware Behavior

The editor supports **mixed mount points**:

* Editable cascaded sources
* Read-only runtime snapshots
* Database-driven prebuild sources

Only editable contexts expose editing, saving, or dirty tracking. The UI uses `IsEditable` and source annotations to disable or grey out values from read-only mounts.

### 7.6 File Writing and Save Flow

When the user saves:

* Only dirty paths are collected
* The modified `Json5SourceFile`s are regenerated with correct formatting
* References are preserved in source format
* All tracked edits are cleared from undo history

Hot reload triggers (optional) can re-export the merged DOM to BSON or notify the running system.

### 7.7 Extensibility and Testing

* Mock contexts can be injected for testing
* Additional viewmodel overlays can be added per node (e.g. per-key metadata, schema diagnostics)
* Visual themes, layout modes, and filtering (e.g. "only show dirty")

---

## 8. Runtime Architecture

The runtime architecture is designed to serve stable, pre-validated configuration data along with dynamic, live-state data to applications and management tools. Unlike the editor, which supports editing, undo/redo, and reference navigation, the runtime focuses on performance, safety, and clarity.

### 8.1 Static and Dynamic Composition

The runtime DOM is built from two types of providers:

* **Pre-built static BSON files**, created from cascaded JSON5 sources during the build process.
* **Live dynamic providers**, which inject runtime-only data such as metrics or agent health.

These are registered under fixed, non-overlapping mount points, just like in the editor.

### 8.2 Initialization and Provider Registration

At application startup:

* The system loads the static `*.bson` files for each known mount
* Dynamic providers are instantiated and bound
* The full DOM tree is assembled

```csharp
runtime.RegisterStatic("config/env1", "env1.bson");
runtime.RegisterDynamic("metrics/live", new MetricsProvider());
```

### 8.3 Runtime DOM Behavior

Key traits:

* **Read-only**: All paths are immutable at runtime
* **Reference-free**: `$ref` nodes are already resolved during BSON build
* **Schema-agnostic**: The runtime does not load schemas—values are assumed valid

This ensures the structure is stable, predictable, and fast to traverse.

### 8.4 Access Patterns

Applications access data via path-based queries:

```csharp
var cfg = runtimeDom.Get<ConfigStruct>("config/env1/system");
var metrics = runtimeDom.Get<MetricsSnapshot>("metrics/live/cpu");
```

All values are strongly typed, deserialized on access, and can be cached as needed. The generic `Get<T>(path)` mechanism supports recursive deserialization of nested structures.

### 8.5 Introspection and Monitoring

The runtime may optionally expose its DOM via an introspection API (e.g. HTTP + JSON), which allows external tools to:

* Query current config
* View live metrics
* Monitor system health

The introspection output is read-only and reflects the exact current state.

Example endpoint:

```http
GET /introspect/config/env1/system/network
```

Returns:

```json
{
  "ip": "192.168.1.10",
  "gateway": "192.168.1.1"
}
```

### 8.6 Reload Behavior

The runtime watches for BSON file changes and reloads automatically if updates are detected. This supports workflows like:

1. User edits JSON5 config
2. Editor regenerates BSON
3. Runtime reloads silently

Dynamic provider data is updated via internal push mechanisms—no reload needed.

### 8.7 Safety and Validation

Because the runtime loads only pre-validated and pre-resolved data:

* There are no syntax errors
* All `$ref` nodes have been resolved
* Schema mismatches are prevented during editing

This clean separation ensures that the application never needs to include complex validation or resolution logic.

---

## 9. Undo/Redo and Dirty Tracking

The configuration editor includes a full **undo/redo mechanism** and **fine-grained dirty tracking**, ensuring a responsive and recoverable editing experience even in large configuration hierarchies. These features are implemented per-provider and per-node, making the system modular and efficient.

### 9.1 Why Dirty Tracking Matters

* Prevents accidental overwrites by showing only what has changed
* Enables precise regeneration of JSON5 source files
* Powers the UI indicators (e.g. red highlights for dirty fields)
* Enables safe cancellation and discard workflows

Dirty tracking also supports integration with version control systems or hot-reload pipelines.

### 9.2 Edit Actions

An `DomEditAction` represents a change to a specific DOM path. It contains:

* `Path`: the absolute path being edited
* `OldValue`: the value before the edit
* `NewValue`: the updated value

Each action is reversible:

```csharp
DomEditAction inverse = action.GetInverse();
```

These actions are pushed to a per-context undo stack.

### 9.3 Undo/Redo Stacks

Each editable provider tracks two stacks:

* **Undo stack**: Most recent change first
* **Redo stack**: Rebuilds undone edits

Operations:

* **Undo**: Pop from undo, apply inverse, push to redo
* **Redo**: Pop from redo, re-apply, push to undo

### 9.4 Marking Dirty State

Each `DomNodeViewModel` has an `IsDirty` flag that is toggled whenever an edit occurs. This propagates upward so parent nodes can show a partially dirty state.

In addition to `IsDirty`, the system tracks which **source file** each dirty node came from, so the correct file can be updated during save.

### 9.5 Visual Feedback

The UI uses dirty tracking to:

* Show change markers or red highlights
* Filter view to "only dirty paths"
* Enable or disable Save / Undo / Discard buttons

### 9.6 File Save Behavior

On Save:

* Dirty nodes are grouped by source file
* Each affected `Json5SourceFile` is regenerated
* Pretty formatting and comment preservation is applied
* Undo/Redo stacks are cleared post-save

### 9.7 Practical Example

1. User changes `config/env1/system/network/ip` from `192.168.1.1` to `192.168.2.1`
2. The system records a `DomEditAction`
3. The UI marks the field red and enables Save
4. User clicks Undo → value reverts, red mark disappears
5. Redo brings back the change

### 9.8 Considerations

* Undo/Redo is in-memory only
* Does not persist between sessions
* Designed for intuitive manual usage, not for full source control

---

## 10. Hybrid Workflows and BSON Output

The system architecture is intentionally designed to support hybrid workflows, where an editable configuration source feeds into a runtime application, and both are visible to the same editor UI at different layers. This requires precise coordination between:

* JSON5 source editing
* Cascading + merging logic
* `$ref` resolution
* Build-time BSON generation
* Runtime application loading and live updates

### 10.1 Dual-Mode View in the Editor

The editor is capable of displaying **both**:

* The editable source tree (from cascade folders)
* The runtime DOM tree (loaded from application introspection)

These are shown side-by-side or as layered branches under the master DOM tree.

**Design Decision**: We decided **not to use a single editor mode** that simultaneously merges runtime + editable content into one unified, mutable tree. Instead, the editor uses clear boundaries between:

* Source mode (editable, cascade-aware, tracks dirty state)
* View mode (runtime-introspected, readonly)

### 10.2 BSON Build Output

The build step reads the merged, resolved DOM tree from static providers, then:

* Resolves all `$ref` nodes
* Removes tracking metadata
* Serializes as canonical JSON
* Compiles into compact BSON

This BSON is saved to disk and served as the source of truth for the runtime.

### 10.3 Runtime Import and Introspection

The application loads BSON files during startup, and may expose its live DOM via HTTP for introspection. The editor can connect to this live view using an embedded or external connection.

```plaintext
Editor → views → config/env1 (editable tree)
Editor → views → runtime/config/env1 (readonly view from live app)
```

If hot reload is supported, a save in the editor can trigger rebuild + reload:

1. User edits value in editor
2. JSON5 file updated, `$ref` preserved
3. Build system regenerates BSON
4. App detects file change, reloads
5. Introspection reflects updated value

### 10.4 Pre-Build Dynamic Sources

The hybrid model also supports non-editable sources like SQL configuration snapshots:

* Loaded as a DOM provider before the build
* `$ref` links to these values are allowed
* Their values are captured in the BSON

This enables referencing global or shared data during the build, while keeping the result fully resolved.

### 10.5 Benefits of the Split Approach

* Clean separation of concerns
* Runtime doesn’t carry editor logic or ref resolution
* Editor retains full schema, source, dirty/undo/tooltip capabilities
* Builds are deterministic and auditable

### 10.6 Limitations

* No on-the-fly editing of runtime state
* Live introspection must be enabled by the application
* Runtime providers cannot emit `$ref` targets (only values)

---

## 11. Summary of Design Decisions

This section summarizes the most significant design choices made during the development of the configuration system, including the rationale behind each decision and trade-offs considered.

### 11.1 Single Shared DOM Tree

**Decision**: Use a unified DOM tree structure (`DomNode`) across both runtime and editor layers.

**Why**:

* Promotes reuse of querying and traversal logic
* Enables consistent data access APIs
* Simplifies serialization and reference resolution

**Trade-off**: Required a separation of data (node content) from editing metadata (viewmodel wrappers)

---

### 11.2 Cascade-Aware Static Config Loading

**Decision**: Support cascade merging from multiple folder levels (e.g., base/site/local) using JSON5.

**Why**:

* Enables reuse and override of values across environments
* Makes config DRY and environment-specific without duplication

**Trade-off**: Required complex tracking of value origins and merge rules for objects vs arrays

---

### 11.3 `$ref` Symbolic References (Static Only)

**Decision**: Allow symbolic references via `$ref` nodes in JSON5 sources, resolved at build time.

**Why**:

* Enables centralization of shared values (e.g., IPs, ports)
* Encourages reuse, avoids copy-paste

**Trade-off**: Complexity in resolution and cycle detection; excluded from runtime DOM for simplicity

---

### 11.4 Split View in Editor

**Decision**: Do not merge runtime and source trees in a single DOM; show them side-by-side instead.

**Why**:

* Avoids user confusion between editable and non-editable content
* Keeps editing logic free of runtime-specific concerns

**Trade-off**: Required duplication of UI logic to support both editable and readonly trees

---

### 11.5 Prebuild-Only Dynamic Sources

**Decision**: Allow data from SQL or other sources to be injected into the DOM **before** BSON generation.

**Why**:

* Allows referencing central values during cascade merging
* Enables snapshotting external state into final config

**Trade-off**: These branches are read-only and cannot be edited, only viewed or used for `$ref`

---

### 11.6 Build-Time BSON Output

**Decision**: Convert merged and resolved config into BSON for use at runtime.

**Why**:

* Compact and fast-loading format
* Eliminates need for JSON parsing at runtime
* Ensures resolved values and no dangling `$ref`

**Trade-off**: Introduces a build step; runtime cannot reflect schema metadata

---

### 11.7 ViewModel Wrappers for Editing

**Decision**: Use a separate `DomNodeViewModel` to track editable state, schema, and errors.

**Why**:

* Keeps core model clean and portable
* Avoids runtime bloat from editor-only features

**Trade-off**: Slightly more complex UI data flow (must use viewmodels everywhere)

---

### 11.8 Undo/Redo and Dirty Path Tracking

**Decision**: Implement full undo/redo stacks and per-path dirty tracking per provider.

**Why**:

* Makes editor safe and reversible
* Enables minimal saves and accurate file regeneration

**Trade-off**: Requires memory tracking of changes, and accurate flattening of values to files

---

### 11.9 Schema-From-C# Design

**Decision**: Derive schema from annotated C# types instead of using JSON Schema.

**Why**:

* Ensures type safety and alignment with application code
* Simplifies tooling
* Enables reuse of attributes like `[Range]`, `[Unit]`, etc.

**Trade-off**: Less expressive than JSON Schema for conditional validation or reuse

---

### 11.10 Mount Point Isolation

**Decision**: Require all providers (config, live, db) to mount at isolated, non-overlapping DOM paths.

**Why**:

* Prevents collisions and ambiguity
* Enables modular tooling and lifecycle per provider

**Trade-off**: Requires developers to design their config structure to allow clear separation

---

These decisions collectively support a robust, maintainable, high-performance configuration system tailored to hybrid static + runtime workflows.

---

## 12. Example Use Cases

### 12.1 Multi-Environment Configuration

**Scenario**: A system needs separate configurations for production, staging, and development, with shared base values.

**Approach**:

* Use three cascade folders: `base/`, `staging/`, and `local/`
* Mount under `config/env1`, `config/env2`, etc.
* Only override differences in higher-level folders

**Example**:

```plaintext
base/system/network.json      → provides default IP, DNS
staging/system/network.json   → overrides DNS for staging
local/system/network.json     → overrides IP for local testing
```

Editor shows:

* Effective value and source layer for each field
* Option to override or remove a value at any layer

---

### 12.2 Referencing Shared Host Data

**Scenario**: Multiple services reference a centralized list of host IPs.

**Approach**:

* A static file or SQL snapshot provides host info under `shared/hosts`
* Use `$ref` nodes in service configs like:

```json
{
  "db": {
    "ip": { "$ref": "shared/hosts/database/ip" }
  }
}
```

**Result**:

* Value is resolved at build time
* All references inline correct IPs in the output BSON
* Editor allows navigation and inline preview

---

### 12.3 Live Monitoring of Metrics

**Scenario**: The running application exposes CPU, memory, and health status live.

**Approach**:

* Register `RuntimeMetricsProvider` at `metrics/live`
* Expose values like:

```json
{
  "cpu": { "usage": 42 },
  "memory": { "available": 128 }
}
```

**Viewer Mode**:

* Editor connects to app’s `/introspect/metrics/live`
* Displays live metrics with auto-refresh
* No editing available for this branch

---

### 12.4 Complex Editing Workflow

**Scenario**: A user modifies a service config, undoes changes, redoes them, and saves.

**Steps**:

1. Edit `config/env1/services/auth/port` from 5000 → 5100
2. Editor marks it dirty
3. User hits Undo → 5000 restored
4. User hits Redo → 5100 reapplied
5. Save: only this field written to `local/services/auth.json`
6. App reloads config and picks up new port

**Value**: Full traceability, recoverability, and selective file writes

---

### 12.5 Schema-Driven UI with Units

**Scenario**: Admin needs to configure memory limits with proper units and bounds

**Schema**:

```csharp
[Range(128, 65536)]
[Unit("MB")]
public int MaxMemory { get; set; }
```

**Editor Behavior**:

* Slider shown from 128–65536
* Value displayed as "1024 MB"
* Tooltips explain range and unit
* Invalid values flagged on entry

---

These examples demonstrate the versatility and composability of the configuration system. By combining static structure, rich metadata, and introspective tools, it enables workflows that are precise, auditable, and user-friendly.



## 13. Schema-Driven Defaults and Required Fields

This section defines the behavior of the configuration system regarding required fields and default values in schema definitions. These capabilities ensure that all required fields are explicitly present in the runtime DOM and that defaults are reliably applied during validation and export.

---

### 13.1 Declaring Required and Default Fields

Schema definitions support metadata on each field indicating:
- Whether the field is **required**
- Whether the field has a **default value**

Each property in an `ObjectSchemaNode` is represented by a `SchemaProperty`, which includes:

- `Schema`: the data schema (e.g., `LeafSchemaNode`, `ObjectSchemaNode`)
- `IsRequired`: whether the field is mandatory (default = false) - if it must be present in the sources.
- `DefaultValue`: an optional default value to apply if not set from the source.

The schema source classes in C# may declare this metadata using:
- Non-nullable fields (e.g., `int Timeout;`) → treated as required
- `[Required]` attribute → explicit requirement
- `[DefaultValue(...)]` attribute → declares default
- Field initializers (e.g., `= 30`) → treated as default value

If a property has a `DefaultValue` defined, it is required to appear in the final runtime tree, having default value if not set in the source.

---

### 13.2 Editor and Validator Behavior

When loading or editing configurations:
- Required fields are flagged if missing.
- Fields with default values are not flagged, but the editor should display the default in the UI as a preview or hint.
- Editor should offer `Reset to default` operation.
- Schema validators must:
  - Raise an error if a required field is missing.
  - Accept absent fields that have defaults defined.

---

### 13.3 Default Injection at Export Time

Before BSON export, the system performs a **default materialization pass** over the merged and resolved DOM tree.

This process:
- Walks the DOM against its schema tree
- For each field:
  - If the field is present → no action
  - If missing:
    - If a `DefaultValue` exists → a new `LeafNode` is created and inserted
    - If marked `IsRequired` and absent → validation error is raised

This ensures the runtime DOM contains:
- All required fields
- All fields with defaults, whether or not they were specified in source

No required/default fields may be omitted from the runtime DOM after export.

---

### 13.4 Summary

- Schema metadata includes `IsRequired` and `DefaultValue` per field
- Editor validates required fields and uses defaults for hints and for `reset to default` operation
- BSON export inserts defaults into the DOM tree
- The runtime sees a fully materialized, self-sufficient configuration tree



## 14. Schema Metadata Architecture

This section describes the internal structure and responsibility split of schema metadata used for configuration validation and UI rendering.

---

### 14.1 Two Layers of Schema Information

The configuration system distinguishes **two separate layers of schema metadata**, each serving a different role:

1. **Type-level Schema Metadata**  
   Describes the structure and constraints of a value *regardless of where it is used*.  
   Represented by the abstract base class `SchemaNode` and its implementations.

2. **Object Field Metadata (Schema Properties)**  
   Describes how a field is declared and used *within a specific object*.  
   Represented by the `SchemaProperty` class.

This separation ensures clear semantics and proper reuse of value schemas.

---

### 14.2 Type-Level Schema: `SchemaNode`

This layer captures intrinsic data structure and constraints. Key types include:

- `LeafSchemaNode`: primitive value schema (e.g., int, string, bool)
- `ObjectSchemaNode`: named fields mapped to schema properties
- `ArraySchemaNode`: ordered list of values with homogeneous type

All schema nodes inherit from `SchemaNode`, which provides common metadata:

- `Min` / `Max` → numeric range constraints (leaf-only)
- `Format` → semantic hint for strings (e.g., `hostname`, `ipv4`)
- `AllowedValues` → enum-style allowed value set (leaf-only)
- `RegexPattern` → optional regular expression for string validation

These fields define what makes a value **valid**, independently of whether it’s required or has a default.

---

### 14.3 Field-Level Metadata: `SchemaProperty`

Every named field in an object is represented by a `SchemaProperty`, which contains:

- `Schema`: a `SchemaNode` representing the value type and constraints
- `IsRequired`: whether this field must appear in the source config
- `DefaultValue`: a fallback value to inject if missing
- `Unit`: optional measurement unit (e.g., `MB`, `seconds`)
- `Description`: optional documentation or tooltip

This metadata is **specific to how a field is declared within an object**, even if the underlying type schema is reused.

For example:

```csharp
public class NodeConfig
{
    [Required]
    [Unit("MB")]
    [Range(128, 65536)]
    [Pattern("^\\d+$")]
    public int MemoryLimit { get; set; } = 1024;
}
```

Produces:

- A `LeafSchemaNode` with `Min=128`, `Max=65536`, `RegexPattern="^\\d+$"`
- A `SchemaProperty` with `IsRequired=true`, `Unit="MB"`, `DefaultValue=1024`

---

### 14.4 Why This Separation Matters

- **Reuse**: the same `LeafSchemaNode` can be reused in different fields with different required/default rules.
- **Validation Logic**: type-level rules govern value structure, while property-level rules determine presence and defaults.
- **UI Clarity**: field-level units and descriptions enable friendly forms without polluting the type schema.

---

### 14.5 Summary

| Layer                | Structure        | Purpose                                              |
| -------------------- | ---------------- | ---------------------------------------------------- |
| Type-level schema    | `SchemaNode`     | What kind of value is allowed                        |
| Field-level metadata | `SchemaProperty` | Whether a field is required, defaulted, or annotated |

This model enables precise validation, powerful UI hints, and robust default injection support before export.
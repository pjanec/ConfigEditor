# Configuration System Architecture

This document provides a comprehensive and in-depth description of the configuration system architecture, detailing both runtime and edit-time behavior, data models, provider infrastructure, design decisions, validation workflows, schema handling, and more. It reflects the entire design history and rationale behind each choice, and includes concrete examples to illustrate the major concepts.

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
- `CascadeJsonContext`: multi-folder cascade of JSON files
- `SingleJsonContext`: flat single-file config (e.g. plugin)

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
- In static config providers (e.g., JSON cascade)
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





## 8. Runtime Architecture

The runtime architecture is designed to serve stable, pre-validated configuration data along with dynamic, live-state data to applications and management tools. Unlike the editor, which supports editing, undo/redo, and reference navigation, the runtime focuses on performance, safety, and clarity.

### 8.1 Static and Dynamic Composition

The runtime DOM is built from two types of providers:

* **Pre-built static BSON files**, created from cascaded JSON sources during the build process.
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



---

## 10. Hybrid Workflows and BSON Output

The system architecture is intentionally designed to support hybrid workflows, where an editable configuration source feeds into a runtime application, and both are visible to the same editor UI at different layers. This requires precise coordination between:

* JSON5 source editing
* Cascading + merging logic
* `$ref` resolution
* Build-time BSON generation
* Runtime application loading and live updates

* 

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






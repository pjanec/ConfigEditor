# ✅ Config Lifecycle and Storage Specification

## 1. Overview

This document describes the architecture, storage structure, and processing workflow for handling application configurations in a modular, referenceable, schema-driven, and runtime-optimized way.

---

## 2. High-Level Architecture

* **Raw Configs**: Human-editable JSON5 documents, optionally with comments, potentially containing `$ref` references.
* **Schemas**: Independently stored schema definitions, versioned, and referenced by configs.
* **Resolved Snapshots**: Fully resolved, `$ref`-free, runtime-optimized JSON or BSON documents.
* **Central Config Provider**: Server-side component managing dependency analysis, validation, resolution, and delivery.

---

## 3. Cascade Concept

Configurations are built using a **multi-layered cascading model**, allowing reusable definitions to be overridden or extended in increasingly specific layers.

* **CascadeDefinitions** define the **ordered sequence of layers**.
* **Layers** may include:

  * **System-wide defaults** (e.g., `/system/global`)
  * **Region-specific overrides** (e.g., `/system/region/eu`)
  * **Application family defaults** (e.g., `/apps/myAppType1/default`)
  * **Application instance specifics** (e.g., `/apps/myAppType1/myAppInstance1`)

Both **shared config fragments** and **app-specific configs** participate in this model, allowing comprehensive and flexible config composition.

---

## 4. Storage Structure

### 4.1 Config Namespaces

* **Mountable to Base DOM Tree**:

  * Example Mount Points:

    * `/system/settings/network`
    * `/apps/myAppType1/myAppInstance1`

### 4.2 Document Collections

#### 4.2.1 Config Fragments Collection

* Stores reusable config fragments.
* Example ID: `system/settings/network`

#### 4.2.2 App Configs Collection

* Stores root configs for apps, containing `$ref` references.
* Example ID: `apps/myAppType1/myAppInstance1/config`

#### 4.2.3 Resolved Snapshots Collection

* Stores resolved, runtime-optimized configs.
* Example ID: `apps/myAppType1/myAppInstance1/resolved`

#### 4.2.4 Schemas Collection

* Stores JSON schema definitions.
* Example IDs:

  * `schemas/myAppFamily1/myAppType1/v1`
  * `schemas/system/network/v1`

#### 4.2.5 Cascade Definitions Collection

* **Purpose**: Defines the ordered sequence of layers to build the final config for a given app or context.
* **Collection Name**: `CascadeDefinitions`
* **Document Example**:

```json
{
  "_id": "apps/myAppType1/myAppInstance1/cascade",
  "description": "Cascade definition for myAppInstance1",
  "layers": [
    "system/global",
    "system/region/eu",
    "apps/myAppType1/default",
    "apps/myAppType1/myAppInstance1"
  ]
}
```

* **Usage**:

  * Loaded by the resolution process to determine layer sequence.
  * Supports reuse across multiple instances.
  * Serves as the authoritative source for merging order.

---

## 5. Reference Syntax

* Always absolute from root.
* Example:

  ```json
  { "$ref": "system/settings/network" }
  ```

---

## 6. Formats

* **Raw Configs**: JSON5 with comments.
* **Schemas**: JSON Schema or custom format (to be defined).
* **Resolved Snapshots**: JSON or BSON (app-configurable).

---

## 7. Processing Workflow

### 7.1 Resolution Trigger Logic

1. Load root config with `$ref`.
2. Recursively collect all referenced IDs.
3. Compare `lastModified` of all involved documents.
4. Rebuild if:

   * No snapshot exists.
   * Any dependency is newer than snapshot.

### 7.2 Rebuild Process

1. Fully resolve all `$ref` recursively.
2. Validate the resolved tree.
3. Store snapshot if changed.

### 7.3 Validation Points

* Client-side UI.
* Raw config save to DB.
* Resolved snapshot generation.

---

## 8. Delivery Methods

* **HTTP JSON API**
* **Static JSON file export**
* **Supports multiple output formats (JSON, BSON)**
* **Includes schema version metadata**

---

## 9. Versioning Strategy

* **DB stores only the latest snapshot.**
* **DB always exports the latest state to a defined folder structure. Git saves snapshots of the latest state as commits, maintaining full history in `/configs` and `/schemas` folders.**

---

## 9.1 Schema Retention Policy

To ensure long-term auditability, rebuild capability, and validation integrity, the system must retain all schema versions while keeping resolved snapshots minimal.

### Retention Rules

| Data Type              | Retention Policy                                     |
| ---------------------- | ---------------------------------------------------- |
| **Raw Configs**        | Keep latest only (full history maintained in Git).   |
| **Schemas**            | Retain **all published versions permanently**.       |
| **Resolved Snapshots** | Keep latest only (or rebuild on-demand when needed). |

### Benefits

* Supports **rebuilding resolved snapshots for any historical schema version**.
* Enables **validation tracking across schema versions**.
* Minimizes **storage requirements** while retaining **maximum auditability**.
* Ensures **apps can request data validated against a specific schema version**.

### Rebuild Behavior Example

1. Load raw config data.
2. Select desired historical schema version.
3. Resolve all `$ref` dependencies.
4. Validate against the selected schema version.
5. Produce or compare the resolved result.

Snapshots include metadata like:

```json
{
  "_id": "apps/myAppType1/myAppInstance1/resolved",
  "schemaRef": "schemas/myAppType1/v2",
  "validatedWithSchemaVersion": "v2",
  "validationStatus": "Valid",
  "resolvedAt": "2025-05-13T16:50:00Z",
  "data": { ... }
}
```

Apps may optionally request resolved configs **targeted at a specific schema version**, triggering on-demand rebuilds if needed.

---

## 10. Open Topics for Future Design

1. Schema Evolution Handling.
2. Automatic Schema Compatibility Checking.
3. Detailed API Contract Definition.
4. Export/Import Format Specification for Git Integration.

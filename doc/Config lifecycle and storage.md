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
    { "id": "system/global", "name": "System Global Defaults" },
    { "id": "system/region/eu", "name": "EU Region Overrides" },
    { "id": "apps/myAppType1/default", "name": "App Type Default Settings" },
    { "id": "apps/myAppType1/myAppInstance1", "name": "My App Instance Settings" }
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

## 5. Schema Export, Import, and Versioning

### 5.1 Purpose

The configuration schema system supports **exporting** and **importing** schema definitions in **portable JSON format**.
This allows:

* External schema **documentation and review**.
* **Version tracking** and **compatibility checks**.
* **Runtime schema loading without reflection**.

---

### 5.2 Export Strategy

The schema is **exported as a hierarchical JSON structure** that mirrors the **object graph** of the configuration model.

* **SchemaNodes** define **types** (object, array, leaf, enum).
* **SchemaProperties** define **field-level metadata** (required, default, description, etc.).

This format is **self-contained** and **human-readable**, suitable for inspection and tooling.

---

### 5.3 Example Export Format

```json
{
  "Type": "Object",
  "Properties": [
    {
      "Name": "Verbosity",
      "IsRequired": true,
      "DefaultValue": "Medium",
      "Unit": "Level",
      "Description": "Sets the verbosity level",
      "Schema": {
        "Type": "Leaf",
        "Min": 0,
        "Max": 100,
        "AllowedValues": ["Low", "Medium", "High"],
        "Format": "level"
      }
    },
    {
      "Name": "OutputPath",
      "IsRequired": false,
      "Description": "Optional output file path",
      "Schema": {
        "Type": "Leaf",
        "Format": "file-path"
      }
    },
    {
      "Name": "Targets",
      "IsRequired": false,
      "Description": "List of output targets",
      "Schema": {
        "Type": "Array",
        "ItemSchema": {
          "Type": "Object",
          "Properties": [
            {
              "Name": "TargetName",
              "IsRequired": true,
              "Schema": {
                "Type": "Leaf"
              }
            }
          ]
        }
      }
    }
  ]
}
```

---

### 5.4 Import Strategy

The **importer** reconstructs the schema tree from JSON, making it available for:

* **Runtime validation**
* **Editor metadata**
* **Version comparison**

The **imported schema tree** is fully functional without requiring runtime reflection.

---

### 5.5 On-Demand Flattening for Version Comparison

While the **hierarchical format** is ideal for human and tooling consumption, **flattening** can be performed **on-demand** to produce:

* **Path-based maps** of schema nodes and properties.
* **Stable identifiers** for change detection.

Example flattened map:

| Path                      | Type | IsRequired | AllowedValues                |
| ------------------------- | ---- | ---------- | ---------------------------- |
| `Verbosity`               | Leaf | true       | \[ "Low", "Medium", "High" ] |
| `OutputPath`              | Leaf | false      | -                            |
| `Targets/Item/TargetName` | Leaf | true       | -                            |

This enables:

* **Schema diffing**
* **Breaking change detection**
* **Compatibility reporting**

---

### 5.6 Change Detection Rules

| **Change**                        | **Compatibility Impact** |
| --------------------------------- | ------------------------ |
| Added Property                    | Non-breaking             |
| Removed Property                  | Breaking                 |
| Property Made Required            | Breaking                 |
| Property Made Optional            | Non-breaking             |
| Default Value Changed             | Non-breaking             |
| Enum Value Added                  | Non-breaking             |
| Enum Value Removed                | Breaking                 |
| Range Narrowed (min/max stricter) | Breaking                 |
| Range Relaxed (min/max looser)    | Non-breaking             |

---

### 5.7 Meta-Schema Definition

The following meta-schema describes the allowed structure of the exported schema itself:

```json
{
  "Type": "Object",
  "Properties": [
    {
      "Name": "Type",
      "IsRequired": true,
      "Schema": { "Type": "Leaf", "AllowedValues": ["Object", "Array", "Leaf"] }
    },
    {
      "Name": "Properties",
      "IsRequired": false,
      "Schema": {
        "Type": "Array",
        "ItemSchema": {
          "Type": "Object",
          "Properties": [
            { "Name": "Name", "IsRequired": true, "Schema": { "Type": "Leaf" } },
            { "Name": "IsRequired", "IsRequired": true, "Schema": { "Type": "Leaf" } },
            { "Name": "DefaultValue", "IsRequired": false, "Schema": { "Type": "Leaf" } },
            { "Name": "Unit", "IsRequired": false, "Schema": { "Type": "Leaf" } },
            { "Name": "Description", "IsRequired": false, "Schema": { "Type": "Leaf" } },
            { "Name": "Schema", "IsRequired": true, "Schema": { "$ref": "#/" } }
          ]
        }
      }
    },
    {
      "Name": "ItemSchema",
      "IsRequired": false,
      "Schema": { "$ref": "#/" }
    },
    {
      "Name": "Min",
      "IsRequired": false,
      "Schema": { "Type": "Leaf" }
    },
    {
      "Name": "Max",
      "IsRequired": false,
      "Schema": { "Type": "Leaf" }
    },
    {
      "Name": "AllowedValues",
      "IsRequired": false,
      "Schema": {
        "Type": "Array",
        "ItemSchema": { "Type": "Leaf" }
      }
    },
    {
      "Name": "Format",
      "IsRequired": false,
      "Schema": { "Type": "Leaf" }
    }
  ]
}
```

This meta-schema is recursive, allowing nested structures and reflecting the hierarchical nature of the exported schema.

---

### 5.8 Summary

* **Primary Export**: Hierarchical, human-readable JSON.
* **Import**: Reconstructs full schema tree without reflection.
* **Flattening**: On-demand for version comparison.
* **Version Comparison**: Detects breaking and non-breaking changes.
* **Meta-Schema**: Describes the structure of the exported schema JSON.

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

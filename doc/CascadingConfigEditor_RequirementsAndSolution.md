# Cascading Configuration Editor: Requirements & Solution Principles

This document summarizes the requirements and agreed-upon solution principles for extending the `WpfEditorGemini` with cascading configuration capabilities, drawing inspiration from `ConfigEditor` and `doc/sysarch.md`.

## 1. Core Goal

To enhance `WpfEditorGemini` to support viewing and editing of layered JSON configurations where multiple layers of configuration files cascade and merge, allowing for environment-specific overrides and structured configuration management. The editor will retain its existing schema-awareness and validation features, adapting them to the layered context.

## 2. Operational Modes

The editor will support two primary operational modes:

*   **Single JSON File Mode:** (Existing functionality) For opening, editing, and saving individual JSON files. Cascade-specific UI elements will be hidden.
*   **Cascade Project Mode:** For working with a defined set of cascading layers. Cascade-specific UI elements will be active.

The editor will determine the mode based on user action (File > Open vs. File > Open Cascade Project) or command-line arguments (passing a `.json` file vs. a `cascade_project.jsonc` file).

## 3. Cascade Project Definition

*   A special JSON configuration file (e.g., `cascade_project.jsonc` or `layers.config.json`) will define the cascade project.
*   This file will list each layer, specifying:
    *   `name`: A user-friendly name for the layer (e.g., "Base", "Site Overrides").
    *   `folderPath`: The path to the directory containing the JSON files for this layer.
    *   `isReadOnly` (optional): A boolean indicating if the layer's content can be edited.
*   The order of layers in this file defines the cascade precedence (lowest to highest).

## 4. Layer Representation and Loading

*   **Internal Representation:** A `CascadeLayer` class will represent each layer in memory. It will contain:
    *   Its definition (name, folder path, index, read-only status).
    *   A list of `SourceFileInfo` objects, each representing a JSON file within the layer's folder (including its relative path and parsed `DomNode OriginalContentRoot`).
    *   A single `DomNode LayerConfigRootNode`, which is the result of an **intra-layer merge** of all its `SourceFileInfo.OriginalContentRoot`s. This merge follows defined rules (e.g., alphabetical by relative file path) and standard JSON merge logic (objects deep-merge, arrays replace).
    *   An `IntraLayerValueOrigins` map (path -> relative file path) to track the source file within the layer for each piece of data in `LayerConfigRootNode`.
    *   An `IsDirty` flag.
*   **Loading:**
    *   `MainViewModel` will load the `cascade_project.jsonc`.
    *   For each defined layer, it will scan the `folderPath`, parse all JSON files, perform the intra-layer merge to populate `LayerConfigRootNode`, and build `IntraLayerValueOrigins`.

## 5. Merged Display View

*   When in "Merged Result" view mode, an `ICascadedDomDisplayMerger` service will be responsible for:
    *   Taking a list of `CascadeLayer` objects (up to the `SelectedEditorLayerIndex`).
    *   Performing an **inter-layer merge** of their `LayerConfigRootNode`s (objects deep-merge, arrays replace).
    *   Producing a new, temporary `DomNode` tree for display. Nodes in this tree will have paths relative to its root.
    *   Populating origin tracking maps in `MainViewModel`:
        *   `_displayValueOriginLayerIndex` (path -> layer index of final value).
        *   `_displayValueOverrideSources` (path -> list of all layers defining/overriding, including file path within layer).

## 6. User Interface for Cascading

*   **Layer Selection:** A `ComboBox` will allow the user to select the `SelectedEditorLayer`. This layer is the target for all edits.
*   **View Mode Toggle:** A `CheckBox` will switch between:
    *   "Show Selected Layer Only": Displays the `SelectedEditorLayer.LayerConfigRootNode`.
    *   "Show Merged Result": Displays the output of the `CascadedDomDisplayMerger` (up to and including the `SelectedEditorLayer`).
*   **Origin Information:**
    *   A new "Origin Layer" column in the DataGrid will show the name of the layer from which the displayed effective value originates.
    *   A context submenu ("Override Sources") will list all layers (and the specific file within that layer) that define or override the value for the selected row. Clicking an item in this submenu will switch the `SelectedEditorLayer` to that layer.
*   **Reset to Undefined:** A context menu item "Reset to undefined (in this layer)" will remove the corresponding node/property from the `SelectedEditorLayer.LayerConfigRootNode`, effectively allowing values from lower layers to become visible or removing the property if it was unique to that layer.

## 7. Editing and Saving

*   **Editing Target:** All edits are applied to the `SelectedEditorLayer.LayerConfigRootNode`.
    *   If editing an inherited value in the merged view, the property/value is first created in the `SelectedEditorLayer.LayerConfigRootNode` (creating an override).
*   **Saving Strategy:**
    *   When saving a dirty `CascadeLayer`:
        1.  The editor will attempt to identify the original `SourceFileInfo.FilePath` within the layer for each modified/new piece of data in `LayerConfigRootNode` (using `IntraLayerValueOrigins`).
        2.  If a new node/property has no direct origin file within the layer, the editor will look for a file with a corresponding path structure in *lower cascade layers*. If found, a new file with a similar relative path will be created in the *current layer's folder* to store this new data (including creating subdirectories if needed).
        3.  If no structural template is found in lower layers for new data, it will be saved to a designated default/catch-all file within the current layer.
        4.  Only files that have changed (or new files) will be written to disk.
*   **Undo/Redo:** Operations will be scoped to the `SelectedEditorLayer.LayerConfigRootNode`.

## 8. Schema and Validation

*   **Schema Application:** The global schema definition remains. Schema mapping (`_domToSchemaMap`) will be performed against the currently displayed DOM tree (either the selected layer's tree or the inter-layer merged tree).
*   **Validation:** Validation will run against this currently displayed DOM tree.
    *   Schema violations will be flagged on the effective values.
    *   The "Origin Layer" column will help users identify which layer introduced data that violates the schema.
*   **Creating Missing Fields (Schema-Driven):** Adding a schema-defined field (not present in the current DOM) will create it within the `SelectedEditorLayer.LayerConfigRootNode`.
*   **Future Enhancement:** A separate "Validate All Layers" feature could be added to check each layer's `LayerConfigRootNode` independently and report all errors in an issue log, regardless of overrides.

## 9. DOM Node Adaptation (`WpfEditorGemini.DomNode`)

*   The existing `WpfEditorGemini.DomNode` class (with readonly `Path` and `Name` for array items set at construction) will be used.
*   When merging layers or files to create new trees (`LayerConfigRootNode`, `mergedDisplayRootNode`), new `DomNode` instances will be created/cloned, ensuring their `Path` is correct relative to their new tree's root.
*   When arrays within a specific `CascadeLayer.LayerConfigRootNode` are modified, affected item `DomNode`s within that tree may need to be recreated to ensure their `Name` (index) and `Path` are correctly re-initialized.

## 10. General Principles

*   Inspired by `ConfigEditor` and `doc/sysarch.md` where applicable.
*   Leverage existing `WpfEditorGemini` DOM, schema, parsing, and validation services, adapting them for the layered context.
*   Prioritize clarity for the user in understanding where data comes from and where edits are applied.
*   Phased implementation to manage complexity.
```

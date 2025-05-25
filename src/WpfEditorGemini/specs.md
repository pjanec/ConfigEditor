# JSON Configuration Editor \- Specification Document

## 1\. Overview

* This document describes the specifications for a C\# WPF-based editor for JSON configuration files.  
* The editor will provide a user-friendly interface for viewing, editing, and validating JSON data based on a defined schema.  
* The editor is designed to handle JSON files of moderate size (under 1MB); loading larger files may result in performance degradation but will not be rejected outright.

## 2\. Functional Requirements

### 2.1. JSON Loading and DOM Representation

* The editor shall load a JSON file from the file system.  
* The JSON shall be parsed and represented in memory as a Document Object Model (DOM) tree.  
* The DOM tree shall be built using the following C\# classes:  
  * `DomNode` (abstract base class):  
    * `Name` (string): The name of the node (property name or array index as a string, e.g., "0", "1").  
    * `Parent` (DomNode): Reference to the parent node.  
  * `ObjectNode` (inherits from `DomNode`):  
    * `Children` (Dictionary\&lt;string, DomNode\>): Dictionary of child nodes.  
    * Represents a JSON object.  
  * `ArrayNode` (inherits from `DomNode`):  
    * `Items` (List\&lt;DomNode\>): List of array elements.  
    * Represents a JSON array.  
  * `ValueNode` (inherits from `DomNode`):  
    * `Value` (JsonElement): The value of the node. When updated, the `ValueNode` instance remains the same, but its `Value` property references a newly created `JsonElement`.  
* Error Handling during Loading/Parsing: If the loaded JSON file is malformed, errors should be reported to the user (specific mechanism not detailed but implied).

### 2.2. Schema Definition and Handling (JSON-less Approach)

* The schema shall be derived directly from a set of C\# model classes at runtime by reading specified assemblies. These classes and their attributes serve as the source of truth for the schema.  
* Schema Loading:  
  * The editor will scan C\# assemblies found in paths configured in `editor_config.json` (see section 4.1).  
  * It will look for classes annotated with a `[ConfigSchemaAttribute(string mountPath, Type schemaClassType)]`.  
  * The `mountPath` string is a full path in the DOM tree (e.g., "section/subsection"), using a forward slash as a separator, with no wildcards.  
  * A specific `MountPath` can only be defined once across all scanned assemblies. If an overlap (duplicate `MountPath`) is detected during schema loading, an error shall be reported to the user.  
* Schema Representation (In-Memory): The schema will be represented in memory using `SchemaNode` objects.  
  * `SchemaNode` Class (Conceptual):  
    * `Name` (string): Name of the property or "\*" for array items.  
    * `ClrType` (`System.Type`): The actual C\# type for the schema node. Used for registering custom renderers/editors.  
    * `IsRequired` (bool): Derived from C\# non-nullable reference types and value types not explicitly marked as nullable.  
    * `IsReadOnly` (bool): Derived from `[System.ComponentModel.ReadOnly(true)]` attribute on a C\# property.  
    * `DefaultValue` (object?): Derived from C\# property initializers (e.g., `int x = 7`). For complex types, this would be their C\# type default (e.g., empty object, empty array) unless initialized.  
    * `Min` (double?): From `[System.ComponentModel.DataAnnotations.RangeAttribute(double min, double max)]`.  
    * `Max` (double?): From `[System.ComponentModel.DataAnnotations.RangeAttribute(double min, double max)]`.  
    * `RegexPattern` (string?): From a custom `[SchemaRegexPatternAttribute(string pattern)]` on a C\# property.  
    * `AllowedValues` (List\&lt;string\>?): For string types, derived from a custom `[SchemaAllowedValues("a", "b")]` attribute. For C\# enum types, this list will be populated with the enum member names. Comparisons are case-insensitive.  
    * `EnumValues` (List\&lt;string\>?): (Covered by `AllowedValues`).  
    * `IsEnumFlags` (bool): If the `ClrType` is a C\# enum marked with `[System.FlagsAttribute]`.  
    * `Properties` (Dictionary\&lt;string, SchemaNode\>?): For object types, key is property name.  
    * `AdditionalPropertiesSchema` (`SchemaNode`?): For dictionary types (e.g., `Dictionary<string, T>`), this holds the schema for `T`, generated recursively.  
    * `AllowAdditionalProperties` (bool): True if `Properties` is null or if backed by a dictionary type allowing arbitrary keys.  
    * `ItemSchema` (`SchemaNode`?): For array types, schema for array items.  
    * `MountPath` (string?): Only for top-level `SchemaNode` instances derived from `[ConfigSchemaAttribute]`.  
    * `NodeType` (`SchemaNodeType` enum: `Value`, `Object`, `Array`): A getter property to easily determine the kind of schema node.  
* `DomNode` to `SchemaNode` Mapping: A `Dictionary<DomNode, SchemaNode>` (or a similar data structure) shall maintain a direct mapping between each `DomNode` instance in the loaded DOM tree and its corresponding `SchemaNode` instance (if one exists).  
  * This dictionary is populated during or immediately after DOM tree construction.  
  * When a new `DomNode` is added, the editor shall automatically try to map a `SchemaNode` to it.  
  * If a `DomNode` is removed, its corresponding mapping entry is also removed.  
* `FindSchemaNode(DomNode domNode)` Implementation:  
  * Primarily performs a direct lookup in the `Dictionary<DomNode, SchemaNode>`.  
  * To populate this, the editor finds the most specific (longest matching) `MountPath` from a loaded root `SchemaNode` that is an ancestor of or equal to the `DomNode`'s path. Then, it navigates within that resolved root `SchemaNode` using property names to find the specific `SchemaNode` for the `DomNode`.  
* Schema Fallback Behavior:  
  * If a `DomNode` has a corresponding `SchemaNode`: Editing and validation proceed according to rules in `SchemaNode`.  
  * If a `DomNode` does not have a corresponding `SchemaNode` (un-schematized):  
    * Editable if it's a `ValueNode`.  
    * Editing based on `JsonElement.ValueKind`.  
    * Numbers: Type deduced from textual representation; if no fractional part, saved as integer. Otherwise, edited as `double`.  
    * Booleans: Default to `CheckBox` editor.  
    * `null` values are allowed.  
    * No advanced validation (Min/Max, Regex, AllowedValues) or specific `ClrType` support beyond basic primitive parsing.  
    * Default editor (e.g., `TextBox`) used, with basic parsing based on `JsonValueKind`.  
* Null Value Handling:  
  * `null` values are allowed in the JSON according to the JSON standard, for both schematized and un-schematized nodes.  
  * Each node (schematized or not) shall provide a context menu option: "Reset to null".  
  * When a `null`\-valued schematized node gets edited, editing shall start with the default non-null value for its `ClrType` (C\# type default, e.g., 0, false, empty string), unless `SchemaNode.DefaultValue` specifies otherwise. If the edit is cancelled, the value reverts to `null`.  
  * When a `null`\-valued un-schematized node gets edited, a dialog allowing the user to set the JSON type and value shall be shown (similar to the Insert feature for no-schema nodes).

### 2.3. DataGrid Display

* The DOM tree shall be displayed in a flat, two-column `DataGrid`.

* Each row in the `DataGrid` shall represent a `DomNode`.

* Display of DOM vs. DOM \+ Schema Tree:

  * A user-selectable option (e.g., a toggle button on a toolbar) shall allow displaying either:  
    * Only the `DomNode` tree (nodes present in the loaded JSON).  
    * The `DomNode` tree combined with schema-only nodes (nodes defined in the schema but not present in the current DOM).  
  * When showing the combined view, schema-only nodes (that can be added by editing them) are visually differentiated (e.g., grayed out) from nodes present in the DOM.  
  * If a node is present in the DOM, it uses its usual color, regardless of whether its value is the same as a schema default.  
* 2.3.1. Left Column (Node Name)

  * Displays the `Name` of the `DomNode`.  
  * Indentation: Implemented using padding. Each depth level adds a fixed padding equivalent to the approximate width of three 'x' letters of the same font used for showing node names.  
  * For `ArrayNode` elements, the index shall be included in the name (e.g., "\[0\]").  
* 2.3.2. Right Column (Value)

  * Displays the value of `ValueNode`s.  
  * Value Alignment: All value cells in the "Value" column shall be left-aligned. Values across all rows must be vertically aligned.  
  * Value Rendering: Defined by a customizable value renderer.  
    * Renderers registered for the `System.Type` of the value (from `SchemaNode.ClrType`).  
    * The `DomNode`'s `Value` property is the data source.  
    * Default renderers for common primitive data types (numbers, strings, enum value labels, checkbox for booleans) use left alignment.  
    * If no specific renderer for `ClrType`, `JsonElement.ValueKind` selects a default renderer.  
    * Two types of rendering modes:  
      * In-place renderer (right 'value' column cell):  
        * Displays value for primitive value nodes.  
        * Displays fixed text for object nodes (e.g., "\[Object\]") and array nodes (e.g., "\[3 items\]"), unless a custom renderer overrides this.  
      * Side panel renderer: Typically for object nodes, showing content in a custom way (e.g., a custom form).  
* 2.3.3. Expand/Collapse Functionality

  * Rows representing `ObjectNode`s and `ArrayNode`s shall be expandable/collapsible.  
  * Dynamic Row Addition: On expansion, child nodes are dynamically added as sub-rows into the `DataGrid`'s `ItemsSource`.  
  * View Model Property: The view model for each `DataGrid` row (`DataGridRowItem`) shall include an `IsExpanded` boolean property.  
  * The view model for the entire flat table (`ObservableCollection<DataGridRowItem>`) is the `DataGrid`'s `ItemsSource`.

### 2.4. In-Place Editing

* `ValueNode`s shall be editable in-place. When `ValueNode.Value` is updated, a new `JsonElement` is created.

* Edit Mode Activation: Double-clicking a `ValueNode`'s value or pressing Enter on the row.

* Editor Selection:

  * Dynamically selected based on the `System.Type` in `SchemaNode.ClrType`.  
  * Default editors for primitive types. Custom editors can be registered.  
  * Custom editors can specify if they require a modal window via a property of the editor class.  
  * Two types of editing modes: In-place or Side-panel (modal).  
  * `DomNode` is the data source.  

* Edit Confirmation: Pressing Enter or losing focus confirms. Validated against schema. If successful, `ValueNode.Value` updated.

* Edit Cancellation: Pressing Esc cancels. `ValueNode.Value` not modified. If editing started from a `null` schematized value (which began with a C\# type default), it reverts to `null`.

  2.4.1. Validation Feedback

  * Failure Notification (In-Place Editor): Failing value cell visually marked (e.g., red border).  
  * Failure Notification (Modal Editor): Red error status line or similar in the modal.  
  * Partial Validation Success (JSON Compatibility):  
    * If value is compatible with `JsonValueKind` (e.g., "123" for Number), it's saved to `ValueNode.Value`.  
    * If schema-level validation (Min/Max, Regex, AllowedValues) still fails, a "failed validation flag" is set in `DataGridRowItem` (or `DomNode`'s viewmodel), controlling visual marking.  
  * Incompatible Value (JSON Parsing Failure):  
    * If value isn't convertible to `JsonValueKind` (e.g., "abc" for Number), editing continues.  
    * Focus returned to in-place edit field.  
    * Modal editor: Cannot confirm edit until all values are at least valid JSON primitives for their `JsonValueKind`.  

* 2.4.2. Editing Object and Array Nodes

  * Not directly editable in their "Value" column by default.  
  * Specialized Editors for Complex Types: An `ObjectNode` or `ArrayNode` can have a special editor registered for its `System.Type`. If it `RequiresModal`, a button launches the modal editor.  
  * The `ClrType` for an `ArrayNode` for custom editor registration is `List<T>` (e.g., `List<string>`), where `T` is `System.Type` from `ItemSchema.ClrType`.  
  * Modal editors handle highlighting internal fields based on the "validation failed" flag on `DomNode`s.  

* 2.4.3. Array Item Editing

  * A placeholder node ("Add item" in gray) shown at the end of every array item list.  
    * If a filter is active and an array is displayed, its "Add item" placeholder is always visible if the array itself is visible.  
  * Entering edit mode (Enter/double-click) on placeholder:  
    * Adds new array item to DOM with its default value.  
      * For schematized arrays, default value comes from C\# type defaults. For complex types (objects/arrays) not explicitly set in schema, defaults are empty object/array. If an object has mandatory properties (as per schema), these are included with their default values.  
    * Starts in-place editing the new item.  
  * If tab navigation moves to placeholder, automatically adds new item and enters edit mode.

### 2.5. Filtering

* Filtering based on node names.  
* When filter applied:  
  * Nodes whose names contain the filter text (case-insensitive) are displayed.  
  * All ancestor nodes of matching nodes are displayed.  
  * Node currently being edited is always displayed.  
* "Show Just Invalid Nodes" Option: Checkbox/button to filter for nodes with "failed validation flag" set, plus their ancestors. Replaces any other active filter. When deselected, previous filter is not restored (filter is cleared).

### 2.6. Search

* Search by node name. Includes:  
  * Search Text Input.  
  * Find Next/Previous buttons.  
  * Matching nodes visually highlighted.  
  * Filtered out matches are skipped. Highlight flag kept independently.  
  * If a match is in a collapsed node, it (and its ancestors) should be automatically expanded.

### 2.7. Performance

* Optimized for JSON files up to 1MB. UI operations responsive. Loading larger files may degrade performance.

### 2.8. Data Persistence and State Management

* Saving Changes: Save entire DOM tree to JSON file. Un-schematized data is part of the DOM and gets serialized.  
* File Operations:  
  * New File: Clears current content (prompts to save if unsaved) and starts with a new empty object `{}` as the DOM root. Menu item "File \> New".  
  * Save As: Should be used on the first save of a new document or via "File \> Save As" menu item.  
  * External Modification: If an open file is modified externally, a notification is shown with an option to reload.  
  * Revert to Last Saved State: A "File \> Revert" menu item discards all in-memory changes for the current file, reloads it from its disk path, and clears the undo/redo stack.  
* Pre-Save Validation: Validation run on entire DOM tree.  
* Save Warning: If validation issues, warn user but allow saving.  
* Post-Load Validation: Full validation run after loading.  
* Visual Validation Feedback (Post-Load/Save): Nodes failing validation have red borders/markers.  
* Unsaved Changes Indicator: Asterisk in title bar or similar. Set after any successful edit that changes DOM. Reset after save/load. Cleared if undo brings state to saved version. Set if redo re-applies change. Editing a "gray" addable schema node marks document as dirty.  
* Exit Prompt: Ask to save if unsaved changes before closing.

### 2.9. Keyboard Navigation and Interaction (Browse Mode)

* Row Selection: Full row selectable.

* Up/Down arrow keys: Move selected row.

* PageUp/PageDown: Move view one page.

* Left/Right arrow keys: Collapse/expand selected `ObjectNode` or `ArrayNode`.

* Deletion: Delete key deletes selected item(s). A general warning dialog is shown before any node deletion. If `ObjectNode` or `ArrayNode` deleted, all children implicitly deleted. Undo is supported.

* Insertion (Insert Key):

  * Inserts new item with default value above current row.  
  * For Arrays:  
    * Item type according to schema. If no schema, then same type as selected item. If no items, ask user for JSON value type (String, Number, Boolean, Object, Array).  
    * Default value from schema or C\# type default. For un-schematized, natural JSON type default.  
    * If `Insert` on array's placeholder node: DOM node built with all mandatory sub-nodes (if object) set to their default values.  
  * For ObjectNodes:  
    * If a property within an `ObjectNode` is selected: shows modal dialog for new property name, JSON data type, and value (for primitives) to be inserted at the same level as the selected property (i.e., as a sibling).  
    * If an `ObjectNode` itself is selected (not one of its properties):  
      * If it's the root node (no parent): adds a new property to it (modal dialog for name, type, value).  
      * If its parent is an `ObjectNode`: inserts a new property into the parent object, at the same level as the selected `ObjectNode` (modal dialog).  
      * If its parent is an `ArrayNode`: inserts a *new array item* (sibling to selected `ObjectNode`) into the parent `ArrayNode`, above the `SelectedObjectNode`. Type follows array item insertion logic.  
  * For Root Array: If the selected node is a root array, `Insert` adds a new array item to it.  

* Clipboard Operations (Keyboard):

  * `Ctrl+C` or `Ctrl+Insert`: Copy selected item(s) value to clipboard as JSON string. Entire subtree for Object/ArrayNodes.  
  * `Ctrl+V` or `Shift+Insert`: Paste JSON from clipboard as new array item(s) ABOVE selected item. (See 2.11).  

* Multi-selection: `Shift+Up/Down` or `Ctrl+Mouse Left Click`.

  2.9.1. Tab Navigation

  * Browse Mode Tab: `Tab` jumps to next row like Arrow Down, no edit mode.  
  * Editing Mode Tab: If in-place editing, `Tab` confirms edit, jumps to next row on same level, starts editing if supported. If not, or at last item, next row selected in Browse mode.

### 2.10. Read-Only Nodes

* `DomNode`s can be read-only based on `[System.ComponentModel.ReadOnly(true)]` attribute in schema.  
* Editor ignores edit mode activation for read-only nodes; jumps to Browse mode. Copy operations allowed; modifications (Delete, Insert via context menu) presumably disallowed.

### 2.11. Clipboard Operations (Detailed)

* Copy to Clipboard: Selected items copied as JSON string. If multiple, a JSON array. For `ObjectNode`/`ArrayNode`, entire subtree is copied.  
* Insert from Clipboard Operation:  
  * Pasting in Browse mode only defined for array nodes. Does not work elsewhere (silent ignore or visual cue).  
  * If JSON array in clipboard, multiple items inserted.  
  * `null` values from clipboard are pasted if part of the JSON structure (e.g. `{"key": null}` or `[1, null, 2]`).  
  * Pasting is above currently selected array item row.  
  * Schematized Arrays: All pasted items must be convertible to array item `System.Type` from schema.  
  * Un-schematized Arrays: Any valid JSON can be pasted; DOM sub-branch constructed.  
  * For no-schema DOM nodes (if pasting into an un-schematized array):  
    * JSON array in clipboard: adds new items.  
    * JSON object/primitive in clipboard: adds as a new item.  
  * Internal Handling: Pasted JSON string parsed into new `JsonDocument`; `DomNode`s created recursively.

### 2.12. Handling Missing DOM Tree Nodes (Schema-Defined, but Not Present)

* If DOM tree misses branches/properties defined in schema (mandatory or defaultable), editor supports adding them.  
* These "addable" nodes are displayed in the flat table (when "DOM \+ Schema" view is active) in a distinct color (e.g., gray) with DEFAULT values (from C\# property initializers, C\# type defaults, or schema attributes).  
* Editing a "gray" placeholder node: DOM tree populated with missing intermediate nodes (using defaults), then editing begins. This marks the document as dirty.

### 2.13. Visual Cues for Un-Schematized Nodes

* `DomNode`s not covered by schema (no corresponding `SchemaNode`) rendered in a different color (e.g., dark blue).

### 2.14. Undo/Redo Functionality

* All operations changing DOM (value change, delete, insert) recorded to undo stack. Separate redo stack.  
* `Ctrl+Z` (Undo), `Ctrl+Y` (Redo).  
* If undo returns to saved state, unsaved flag cleared. Redo sets unsaved flag.  
* Edit commits (Enter/focus loss) are the granularity for undo.

### 2.15. Context Menu

* Available on `DataGrid` cells. Dynamic items; all selected items must support the operation.  
* Specific Context Menu Items:  
  * Copy: Copies selected node(s) value to clipboard as JSON string (entire subtree for objects/arrays).  
  * Paste: Pastes JSON from clipboard (behavior as in 2.11, only for arrays).  
  * Reset to null: Sets the node's value to `null`. Available for all nodes.  
  * Insert:  
    * "Insert new item ABOVE": (Arrays) Inserts new array item above selected one (like Insert key).  
    * "Insert new item BELOW": (Arrays) Inserts new array item below selected one. On placeholder, creates item at its place. In-place edit starts.  
    * "Insert new sub-node": (ObjectNodes) Adds new property. Modal dialog for property name, JSON data type, value.  
      * If parent `ObjectNode` is schematized but "closed" (no additional properties allowed by schema): this option is still available. Adding the property makes the parent `ObjectNode` invalid (validation error: unexpected property) and the new sub-node is un-schematized (dark blue).  
      * If parent `ObjectNode` schema allows additional properties via `Dictionary<string, T>` where `T` is a concrete C\# class: the new sub-node will be validated against the schema for `T` (derived from `SchemaNode.AdditionalPropertiesSchema`).  
  * Delete: Deletes selected node(s) (like Delete key, with general warning).

## 3\. UI Design

* WPF `DataGrid` as primary UI.  
* Standard WPF controls for editing. Custom editors for complex types.  
* Visual cues (indentation, highlighting, error markers, distinct colors).  
* Focus Management:  
  1. After In-Place Edit Confirmation (Enter/Tab): Enter: focus row in browse. Tab: next editable cell/row, start edit or select in browse.  
  2. After In-Place Edit Cancellation (Esc/Focus Loss): Focus row in browse.  
  3. After Node Deletion: Select node after deleted; if last, select new last; if empty, focus DataGrid/filter.  
  4. After Node Insertion (Key/Context Menu): Focus new row, value cell in edit mode if `ValueNode`.  
  5. After Adding Array Item (placeholder): Focus new item's value cell, edit mode.  
  6. After Expand/Collapse: Keep focus on same row.  
  7. After Search: Focus found node's row, scrolled into view, selected.  
  8. After Filter Apply/Clear: Focus filter box or first visible row in DataGrid.  
  9. Modal Dialogs: Open: focus first interactive. OK/Confirm/Cancel: focus launching cell/row or select relevant node.

## 4\. Technology Stack & Configuration

* C\#  
* WPF (.NET)  
* `System.Text.Json` (for JSON parsing/serialization)  
* `System.ComponentModel` attributes (e.g., `ReadOnlyAttribute`, `RangeAttribute`) for schema definition.  
* Custom attributes (e.g., `ConfigSchemaAttribute`, `SchemaRegexPatternAttribute`) for schema definition.

### 4.1. Editor Configuration (`editor_config.json`)

* Stored in `editor_config.json` in editor’s ‘config’ folder. No UI for editing this file.

Example structure:  
 JSON  
    {  
      "windowSettings": {  
        "width": 1024,  
        "height": 768  
        // Potentially last position  
      },  
      "recentFiles": \[  
        "C:/path/to/recent1.json",  
        "C:/path/to/recent2.json"  
      \],  
      "schemaProcessing": { // For JSON-less schema approach  
        "assemblyScanFolders": \[ // Paths to folders containing assemblies with C\# schema classes  
          "./custom\_schemas",     // Relative to editor binary  
          "D:/shared/company\_schemas" // Absolute path  
        \]  
      },  
      "displaySettings": {  
        "showSchemaNodes": true // The toggle for DOM vs DOM+Schema view  
      }  
    }


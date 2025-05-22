# JSON Configuration Editor - Specification Document

## 1. Overview

* This document describes the specifications for a C# WPF-based editor for JSON configuration files.
* The editor will provide a user-friendly interface for viewing, editing, and validating JSON data based on a defined schema.
* The editor is designed to handle JSON files of moderate size (under 1MB).

## 2. Functional Requirements

### 2.1. JSON Loading and DOM Representation

* The editor shall load a JSON file from the file system.
* The JSON shall be parsed and represented in memory as a Document Object Model (DOM) tree.
* The DOM tree shall be built using the following C# classes:
    * `DomNode` (abstract base class):
        * `Name` (string): The name of the node (property name or array index).
        * `Parent` (DomNode): Reference to the parent node.
    * `ObjectNode` (inherits from `DomNode`):
        * `Children` (Dictionary<string, DomNode>): Dictionary of child nodes.
        * Represents a JSON object.
    * `ArrayNode` (inherits from `DomNode`):
        * `Items` (List<DomNode>): List of array elements.
        * Represents a JSON array.
    * `ValueNode` (inherits from `DomNode`):
        * `Value` (JsonElement): The value of the node.
        * Represents a JSON primitive value.

### 2.2. Schema Definition and Handling

* The schema shall be derived from a set of **C# model classes** that define the structure and data types of the JSON. These classes will serve as the source of truth for the schema.
* **Schema Instantiation:** For specific branches of the DOM tree (concrete paths), a top-level C# class will be designated. The schema for that branch will then be inferred from the properties and nested classes of that C# class.
* **`DomNode` to `SchemaNode` Mapping:** A `Dictionary<DomNode, SchemaNode>` (or a similar data structure) shall maintain a direct mapping between each `DomNode` instance in the loaded DOM tree and its corresponding `SchemaNode` instance (if one exists). This dictionary will be populated during or immediately after the DOM tree construction. When a new `DomNode` is added, the editor shall automatically try to map a `SchemaNode` to it and update this mapping. If a `DomNode` is removed, its corresponding mapping entry should also be removed.
* **`FindSchemaNode` Implementation:** The `FindSchemaNode(DomNode domNode)` method will primarily perform a direct lookup in this `Dictionary<DomNode, SchemaNode>`.
* **Schema Fallback Behavior:**
    * If a `DomNode` has a corresponding `SchemaNode` in the mapping dictionary, editing and validation shall proceed according to the rules defined in that `SchemaNode` (including `ClrType`, `Min`, `Max`, `RegexPattern`, `AllowedValues`, etc.).
    * If a `DomNode` does **not** have a corresponding `SchemaNode` in the mapping dictionary (i.e., it represents an "un-schematized" part of the JSON):
        * It shall still be editable if it's a `ValueNode`.
        * Editing will be based solely on the `JsonElement.ValueKind` of the `ValueNode`.
        * Numbers will be edited as `double`s.
        * Booleans will default to a `CheckBox` editor.
        * `null` values are allowed as a node value only in the case of un-schematized nodes.
        * No advanced validation (Min/Max, Regex, AllowedValues) or specific `ClrType` support (beyond basic primitive parsing) will be applied.
        * The default editor (e.g., `TextBox`) will be used, with basic parsing attempts based on `JsonValueKind`.

### 2.3. DataGrid Display

* The DOM tree shall be displayed in a flat, two-column `DataGrid`.
* Each row in the `DataGrid` shall represent a `DomNode`.

    #### 2.3.1. Left Column (Node Name)

    * Displays the `Name` of the `DomNode`.
    * **Indentation:** Indentation shall be implemented using padding. Each depth level shall add a fixed padding equivalent to the approximate width of three 'x' letters to the left of the node name.
    * For `ArrayNode` elements, the index shall be included in the name (e.g., "[0]").

    #### 2.3.2. Right Column (Value)

    * Displays the value of `ValueNode`s.
    * **Value Alignment:** All value cells in the "Value" column shall be left-aligned. The values across all rows must be vertically aligned to the same visual line on the screen, creating a clean tabular appearance.
    * **Value Rendering:** The way a value is rendered shall be defined by a customizable value renderer.
        * Renderers shall be registered for the CLR type of the value.
        * The `DomNode`'s `Value` property shall serve as the data source for the renderer.
        * Default renderers for common primitive data types (numbers, strings, enum value labels, checkbox for booleans) shall use left alignment for the content itself.
        * If no specific renderer is registered for a given `ClrType`, the JSON `ValueKind` of the `JsonElement` shall be used to select a default renderer (e.g., a generic text representation for unknown types).
        * **Two types of rendering modes:**
            * **In-place renderer** in the right 'value' column cell of the flat grid:
                * Displays the value itself for primitive value nodes.
                * Displays empty or specialized content for object nodes (e.g., "[Object]").
                * Displays the number of items for array nodes (e.g., "[3 items]").
            * **Side panel renderer:** Typically for object nodes, showing the object node content in a custom way (e.g., a custom form next to the grid).

    #### 2.3.3. Expand/Collapse Functionality

    * Rows representing `ObjectNode`s and `ArrayNode`s shall be expandable/collapsible.
    * **Dynamic Row Addition:** On expansion, child nodes (properties for `ObjectNode`s, items for `ArrayNode`s) shall be dynamically added as sub-rows directly into the `DataGrid`'s `ItemsSource`, maintaining the correct hierarchical order.
    * **View Model Property:** The view model class for each `DataGrid` row (`DataGridRowItem`) shall include an `IsExpanded` boolean property to track the expanded/collapsed state of the node it represents. This property will control the visibility of its children in the flat `ItemsSource`.
    * The view model class for the entire flat table (`DataGridItems` collection, likely an `ObservableCollection<DataGridRowItem>`) shall define the data source for the `DataGrid` rows.

### 2.4. In-Place Editing

* `ValueNode`s shall be editable in-place within the `DataGrid`. When a `ValueNode.Value` (`JsonElement`) is updated, the `DomNode` instance itself shall remain the same, but its `Value` property will reference a newly created `JsonElement` to reflect the change.
* **Edit Mode Activation:**
    * Double-clicking on a `ValueNode`'s value or pressing the Enter key when the row is selected shall activate edit mode.
* **Editor Selection:**
    * The editor control shall be dynamically selected based on the CLR type defined in the `SchemaNode`.
    * Default editors shall be provided for primitive types (string, int, double, bool, enum).
    * Custom editors can be registered for specific CLR types.
    * Custom editors can specify if they require a modal window for editing.
    * **Two types of editing modes:**
        * **In-place** (within the `DataGrid` cell).
        * **Side-panel (modal)** (in a separate dialog or panel).
    * The `DomNode` shall be the data source for the editor.
* **Edit Confirmation:**
    * Pressing the Enter key within the editor shall confirm the edit.
    * The edited value shall be validated against the schema.
    * If validation is successful, the `ValueNode`'s `Value` shall be updated.
* **Edit Cancellation:**
    * Pressing the Esc key or losing focus from the editor shall cancel the edit.
    * The `ValueNode`'s `Value` shall not be modified.

    #### 2.4.1. Validation Feedback

    * **Failure Notification (In-Place Editor):** If validation fails, the failing value cell in the `DataGrid` shall be visually marked (e.g., with a red border around the cell) to indicate the error.
    * **Failure Notification (Modal Editor):** In the case of a modal editor, a red error status line (or similar prominent visual indicator) shall be shown within the modal window to display validation feedback.
    * **Partial Validation Success (JSON Compatibility):**
        * If the value entered is compatible with the `JsonValueKind` of the `DomNode` (i.e., it can be parsed into a valid JSON primitive of that kind, e.g., "123" for a `Number`, "true" for a `Boolean`), it shall be saved to the `ValueNode`'s `JsonElement`.
        * However, if schema-level validation (e.g., Min/Max constraints, Regex pattern, AllowedValues) still fails, a **failed validation flag** shall be set in the `DataGridRowItem` (or the `DomNode`'s viewmodel) representing that node. This flag will control the visual marking of the cell.
    * **Incompatible Value (JSON Parsing Failure):**
        * If the value entered is **not** convertible to the `JsonValueKind` of the node (e.g., typing "abc" into a `JsonValueKind.Number` field), the editing process shall continue.
        * Focus shall be returned back to the in-place edit field to allow the user to correct the input immediately.
        * In the case of a modal editor, it shall **not** be possible to confirm the edit (e.g., the "OK" button will be disabled or show a blocking error) until all values within the modal are at least saveable as valid JSON primitives (i.e., compatible with their respective `JsonValueKind`).
    * **Schema-defined Nullability:** For nodes covered by the schema, `null` is never a valid value.

    #### 2.4.2. Editing Object and Array Nodes

    * `ObjectNode`s and `ArrayNode`s shall **not** be directly editable in their "Value" column by default. Their content is edited by expanding them and modifying their child nodes.
    * **Specialized Editors for Complex Types:** An `ObjectNode` or `ArrayNode` can have a special editor registered for its specific `ClrType`. If such a custom editor is registered and `RequiresModal` is true, a button or similar control will be displayed in the "Value" column. Clicking this control will launch the modal editor for the entire `ObjectNode` or `ArrayNode` instance.
    * **`ArrayNode` `ClrType` Definition:** For the purpose of custom editor registration and type inference, the `ClrType` for an `ArrayNode` shall always be represented as a generic `List<T>` (e.g., `List<string>`, `List<MyCustomObject>`), where `T` is the `ClrType` of the array's `ItemSchema`. This allows for unambiguous assignment of array-specific custom editors.
    * The DOM validation of the node (and potential sub-nodes) edited by the modal editor is handled on a per-node level based on schema-defined validation, in the same way as used for in-place editing.
    * Modal editors shall handle the highlighting of their internal edit fields bound to the DOM nodes that failed validation, using the "validation failed" flag on the DOM node.

    #### 2.4.3. Array Item Editing

    * The editor shall show a **placeholder node** (displaying "Add item" in gray color) at the end of every array item list.
    * Entering edit mode (pressing Enter or double-clicking) on this placeholder node shall:
        * Add a new array item to the DOM tree with its default value (as defined by `ItemSchema`).
        * Start in-place editing the newly added item (if in-place editing is supported for its type).
    * If the tab navigation moves to this placeholder node, it shall automatically add a new item and enter edit mode on it.

### 2.5. Filtering

* The editor shall provide a filtering mechanism to show a subset of the DOM tree in the `DataGrid`.
* The filtering shall be based on node names.
* When a filter is applied:
    * Nodes whose names contain the filter text shall be displayed.
    * All ancestor nodes (up to the root) of matching nodes shall also be displayed to maintain context.
    * The node currently being edited shall always be displayed, regardless of the filter.
* **"Show Just Invalid Nodes" Option:** There shall be an option (e.g., a checkbox or button) to filter the DOM tree to display only:
    * Nodes that have failed validation (indicated by the "failed validation flag").
    * All parent nodes up to the root, to allow the user to orient where the failed node belongs in the structure. This filter shall replace any other active filter. When this filter is active, search results shall only be highlighted within the set of visible invalid nodes (and their ancestors).

### 2.6. Search

* The editor shall provide a search function to find nodes by name.
* The search function shall include:
    * Search Text Input: A `TextBox` for entering the search text.
    * Find Next: A button to navigate to the next matching node.
    * Find Previous: A button to navigate to the previous matching node.
    * Search Results Highlighting: Matching nodes shall be visually highlighted in the `DataGrid`.
    * If jumping over search matches, the ones filtered out should be skipped. The search highlight flag should be kept independently of the filter. If a node is currently collapsed, but a search matches one of its hidden children, it should be automatically expanded.

### 2.7. Performance

* The editor shall be optimized for JSON files up to 1MB in size.
* UI operations (loading, editing, filtering, searching) shall be responsive.

### 2.8. Data Persistence and State Management

* **Saving Changes:** The editor shall be able to save the entire DOM tree back to a JSON file.
* **Pre-Save Validation:** Before saving, a validation step shall be run on the entire DOM tree.
* **Save Warning:** If there are any validation issues (nodes with the "failed validation flag" set), the editor should warn the user but still allow the saving operation to proceed.
* **Post-Load Validation:** The same validation mechanism shall be run immediately after loading the DOM tree from a JSON file.
* **Visual Validation Feedback (Post-Load/Save):** Nodes failing validation (after loading or if saved despite warnings) shall have red borders or similar visual marking.
* **Unsaved Changes Indicator:** An indicator (e.g., asterisk in title bar, changed button appearance) shall be present to show that there are unsaved changes. The unsaved flag is set after any successful edit. The unsaved flag is reset after saving and also after loading. If an undo operation brings the state back to a previously saved version, the unsaved flag should be cleared. Redoing an operation that re-applies a change will set the unsaved flag.
* **Exit Prompt:** The editor should ask for saving before closing if there are unsaved changes.

### 2.9. Keyboard Navigation and Interaction (Browse Mode)

* **Row Selection:** The full row should be selectable in Browse mode.
* Up/Down arrow keys shall move the selected row up/down.
* PageUp/PageDown keys shall move the view one page up/down.
* Left/Right arrow keys shall collapse/expand the currently selected node (if it's an `ObjectNode` or `ArrayNode`).
* **Deletion:** The Delete key shall delete the currently selected item(s). If multiple nodes are selected, all get deleted. Deletion happens on the DOM tree. Deleting of a required property should be possible, just the user should be warned and should confirm first.
* **Insertion:** The Insert key shall insert a new item with its default value above the current row.
    * **For Arrays:**
        * Item type according to the schema. If no schema is defined, then the same type as the selected item. If no items are in the array yet, the editor should ask about the item type (natural Json value types).
        * Default value according to the schema. If not defined, then a natural default for the CLR data type (if schema is defined). If no schema, a natural default for the JSON value type.
    * **For Schema-covered items (when using Insert key on a "gray" placeholder node):**
        * The DOM node should be built with all mandatory subnodes set to their default value.
    * **For no-schema non-array nodes (when using Insert key):**
        * If standing on a property within an `ObjectNode`, a new property should be created. The editor should show a modal dialog selecting the property name, property JSON data type (int, bool, string, object), and property value (for primitives).
* **Clipboard Operations (Keyboard):**
    * `Ctrl+C` or `Ctrl+Insert` shall copy the currently selected item(s) value to the clipboard as a JSON string.
    * `Ctrl+V` or `Shift+Insert` shall paste the JSON from the clipboard as a new array item(s) ABOVE the currently selected item. (See "Insert from Clipboard" operation below).
* **Multi-selection:** `Shift+Up/Down arrow` or `Ctrl+Mouse Left Click` shall enable multi-selection of rows.

    #### 2.9.1. Tab Navigation

    * **Browse Mode Tab:** If in Browse mode, `Tab` should jump to the next row just like pressing an arrow down key. It should not enter edit mode.
    * **Editing Mode Tab:** If in in-place editing mode (the right cell 'value' column is focused), `Tab` shall:
        * Confirm the edit.
        * Jump to the next row on the same level (e.g., next property of the object, next item of the array).
        * Start editing it (only if in-place edit is supported).
        * If no in-place edit is possible or if at the last property of an object node/last item of an array node, the next row shall be selected in Browse mode.

### 2.10. Read-Only Nodes

* Some `DomNode`s may be marked as read-only. This read-only flag shall be read from the schema.
* The editor should simply ignore editing mode for such nodes – never enter the editing, but jump to Browse mode.

### 2.11. Clipboard Operations (Detailed)

* **Copy to Clipboard:**
    * Currently selected items are copied to the clipboard as a JSON string.
    * If multiple items selected, a JSON array of items is stored.
* **Insert from Clipboard Operation:**
    * Pasting in Browse mode is only defined for array nodes. It should not work elsewhere.
    * If multiple items were put to the clipboard, there is the JSON array string stored in the clipboard.
    * Single selected item can result in either a primitive value JSON string or a JSON object JSON string in the clipboard, depending on the selected node type.
    * Inserting should only work if there is a valid JSON token string in the clipboard that can be converted to the data type of the item as defined in the schema.
    * When the item nodes are covered by the schema, all the array items must share the same CLR type. The paste operation should check if the clipboard content is convertible to the array item CLR type.
    * When the array is not covered by the schema, any JSON can be pasted as the array item and the DOM sub-branch should be constructed accordingly.
    * If a JSON array is found in the clipboard, multiple items are inserted.
    * `null` values should never be inserted.
    * Pasting is always above the currently selected array item row. The row order follows the order of items in the DOM tree so the index to insert at is clear.
    * **For no-schema DOM nodes:**
        * If a JSON array is in the clipboard, it should only work inside an array node, adding new items there.
        * If a JSON object is in the clipboard, it should create a new full DOM tree branch.
    * **Internal Handling:** When JSON is pasted, its string content will be parsed into a new `JsonDocument` (and its `RootElement`). `DomNode` instances for the pasted content will then be created, and their `ValueNode.Value` properties will be assigned new `JsonElement`s reflecting the parsed data. This process will recursively construct the necessary DOM sub-tree for objects and arrays.

### 2.12. Handling Missing DOM Tree Nodes (Schema-Defined, but Not Present)

* If the DOM tree is missing some branches or properties that are defined in the schema tree (i.e., mandatory or defaultable schema nodes not present in the loaded JSON), the editor shall support adding them.
* These "addable" nodes (defined in the schema tree but missing in the DOM tree) shall be displayed in the flat table together with existing DOM tree nodes but in a distinct color (e.g., gray) and with **DEFAULT values** (either schema defined or deduced from CLR type or JSON type if no CLR type available).
* If the user attempts to edit the value of such a "gray" placeholder node, the DOM tree shall be populated with the missing intermediate nodes (using their default values from the schema) as required, and the editing of the selected node shall begin as usual.

### 2.13. Visual Cues for Un-Schematized Nodes

* `DomNode`s that are not covered by the schema (un-schematized nodes) shall be rendered in a different color (e.g., dark blue) to visually distinguish them.

### 2.14. Undo/Redo Functionality

* All operations like changing node value, deleting a node, or inserting a new node should be recorded to an undo stack.
* Any change in the DOM tree triggers an undoable operation.
* There is a separate redo stack.
* `Ctrl+Z` and `Ctrl+Y` shortcuts shall be supported for Undo and Redo respectively.
* If an undo operation brings the state back to a previously saved version, the unsaved flag should be cleared. Redoing an operation that re-applies a change will set the unsaved flag.

### 2.15. Context Menu

* A context menu shall be available on cells in the `DataGrid`.
* The context menu items will be dynamic based on the node type and schema rules. All selected items must support the operation in order for the context menu item to be offered for multi-selected rows.
* **Specific Context Menu Items:**
    * **Copy:** Copies the selected node(s) value to clipboard as JSON string.
    * **Paste:** Pastes JSON from clipboard. (Behavior as defined in 2.11).
    * **Insert:**
        * **"Insert new item ABOVE"**: Inserts a new array item above the selected one (behavior as for Insert key).
        * **"Insert new item BELOW"**: Inserts a new array item below the selected one. When an array's placeholder node is selected, a new item is created at its place. In-place edit mode starts immediately (if available).
        * **"Insert new sub-node"**: Available for `ObjectNode`s. Adds a new property to the selected object node. The editor shall show a modal dialog for selecting the property name, property JSON data type (int, bool, string, object), and property value (for primitives).
    * **Delete:** Deletes the selected node(s). (Behavior as for Delete key).

## 3. UI Design

* A WPF `DataGrid` shall be the primary UI element for displaying and editing the JSON data.
* Standard WPF controls (TextBox, Button, CheckBox, ComboBox) shall be used for editing primitive values.
* Custom editor controls may be used for complex CLR types.
* Visual cues (indentation, highlighting, error markers, distinct colors for un-schematized nodes) shall be used to enhance usability.

## 4. Technology Stack

* C#
* WPF (.NET)
* `System.Text.Json` (for JSON parsing)

## 5. Future Considerations

* Schema validation against standard schema formats (e.g., JSON Schema).

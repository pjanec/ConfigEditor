# DOM Tree Editor UX

# Original reqs

Two columns. In left colum a hierarchy dom unfoldable tree (each line one node), right column shows the node value. Pluggable value renderers for custom node value clr type. When the right column cell is clicked, it changes into an edit field filling the cell, editing the node value. The edit field is smart, selecting different edit controls based on the clr data type of the dom node value. Text field for texts and numeric types, dropdown with text values for enums, checkbox for booleans etc. Pluggable custom edit controls for custom types. For array nodes, ability to add, remove and insert new item of clr type as specified in the schema. Sample dom tree to fill the table. Pluggable renderers and editor controls for primitive types.

Keyboard navigation is mandatory. cursor up/down moves selection row up/down. Numpad plus/minus unfolds/folds node. enter or click activates edit mode for a row (not always, see below for more details). Once in edit mode, esc cancels it without changing node value, enter confirms and updates the node and exits edit mode. Whole table starts unfolded, folding is not persistent, kept only until the app restarts. While in edit mode, table navigation/folding is disabled. The edit mode of a row showing primitive type should start automatically on enter and validate and apply on leave. For text fields on enter all the text should be initially selected so that when typing starts, it overwrites previous value.

For primitive types no enter key now needed to activate edit mode, automatic on get focus. Esc returns back the original unmodified value captured on getting focus. Validate and apply on lost focus. Some edit controls might capture the row navigation keys while in edit mode (like enum dropdown) so some confirmation key or lost focus event is needed to exit edit mode and validate and apply. Those also require some edit mode activation event (not just “got focus”) to allow for smooth row navigation over them if the user does not want to change value. Plain single line text fields or checkboxes should not block the row navigation. Pls suggest other ui best practices for pleasant keyboard control.

Row height is primarily controlled by the node value renderer \- can be multiple lines. Row is always at least one text line high. Node names in the left column should wrap to be always displayed in full (all characters of the string). Schema provides concrete clr type for each dom element. 

Edit mode entering and update on value change should be the responsibility of the editor control per type. For example text/numeric text controls should wait for Enter key to apply change when in editing mode (when showing blinking cursor). bool checkbox should update immediately if clicked. Enum dropdown should open on click (or enter key) and update on value selected by click or by pressing enter (while key up/down moves the selection within the dropdown). Editor control is also responsible for validation. Invalid value should be indicated by reddish indicator (underlining or edit field background etc.)

Live filter the node names by string (contains, case insensitive).

No multiple simultaneous edits. Selecting another node should not be possible until the edit of the currently selected node is finished.

The JsonElement in the ValueNode is always a json primitive type. CLR type for the ObjectNode is a c\# class that can be deserialized from the subnodes.

Array Editing: Numbered items. Delete key should delete selected. Insert key should insert empty default above selected. Copy/paste with multiselect. Paste inserts above selection. selectable non-editable pseudo item placeholder shown at the end of list to support inserting new stuff at end.

# **✅ Unified Keyboard and Editing Specification (WPF DOM Table Editor)**

## **1\. Row Navigation and Selection**

### **Single Select**

* **Click** or **Arrow Up/Down** selects a row **without entering edit mode**.

### **Multi-Select**

* **Shift \+ Arrow Up/Down** expands selection from the active row.

* **Ctrl \+ Click** toggles additional selections without clearing others.

* **Shift \+ Click** extends selection range from active row to clicked row.

---

## **2\. Edit Mode Activation**

### **General Rule**

* **Edit mode never starts on selection alone**.

### **Activation Methods**

* **Double-Click** on a selected row.

* **Press Enter** on a selected row.

---

## **3\. Edit Mode Behavior Per Node Type**

### **Single-Line Text and CheckBox**

* **Inline editing** starts **immediately after activation** (Enter/Double-Click).

* **Value applied** on **LostFocus** or **Enter**.

* **Escape** cancels and restores original value.

* **Arrow keys** navigate rows (do not block navigation).

### **ComboBox / Dropdown / Complex Editors**

* **Activation required** (Enter/Double-Click).

* **Blocks navigation** when open.

* **Value applied** on **selection change**, **LostFocus**, or **Enter**.

* **Escape** cancels edit without applying changes.

### **Array Items (New Rule)**

* **Always require explicit activation** (Enter/Double-Click).

* **If editor exists for item type**:

  * Use **editor view only**, **hide subnodes**.

* **If no editor exists**:

  * Show **expandable subnodes** inline as normal object nodes.

---

## **4\. Array Manipulation**

* **Delete Key**: Remove all selected items.

* **Insert Key**: Insert new item(s) above selection.

* **Ctrl+C / Ctrl+V**: Copy/Paste selected items (insert above selection).

* **Pseudo-item at end** for adding new items by click or Enter.

---

## **5\. Filter Behavior**

* **Live filter on node names only**, partial, case-insensitive.

* **Esc** clears filter if filter box is focused.

---

## **6\. Visual Feedback and UX**

* **Highlight current selection**.

* **Indicate edit mode activation** visually (border, background change).

* **Tooltip or hover detail support** from renderers.

* **Optional keyboard legend** explaining controls.

---

# **✅ Formal Specification: Live DOM Node Tree Filter**

##  **Filtering Goals**

* **Show only matching nodes**, but still **preserve the context** (parent hierarchy) so the user understands where the match is located.

* **Expand parents** automatically if their child matches.

* **Allow clearing the filter quickly** (Esc or Clear button).

## **1\. Filter Scope**

* **Partial Matching on Node Name Only**

  * The filter applies **only to node names** (the `Name` property of each `DomNode`).

  * **Values are not searched**.

  * **Partial match** is sufficient (case-insensitive substring match).

## **2\. Matching Behavior**

* A node **matches** if its `Name` contains the filter text (case-insensitive).

## **3\. Context Preservation**

* **Parent Hierarchy Visibility**

  * If a child node matches, **all its ancestor nodes are shown and expanded**, even if the ancestors themselves do not match.

## **4\. Automatic Expansion**

* **Expand all parent branches** leading to matching nodes.

* **Do not expand non-matching sibling branches**.

## **5\. Clear Filter Behavior**

* **Collapse all expanded branches** when the filter is cleared (reset to initial collapsed state).

## **6\. User Feedback**

* Show **"No matches found"** if the filter results in **no visible nodes**.

## **7\. Edit Mode Protection**

* **Do not hide or collapse nodes that are currently being edited**, even if they do not match the filter.

## **8\. Performance Optimization**

* **Short-circuit tree traversal** by stopping exploration of branches that cannot contain matches.

* **Debounce filter input** if performance becomes an issue with large trees (optional, implement if needed).

## **9\. Keyboard Shortcut**

* **Escape** clears the filter if the filter box is focused and not empty.

---

# **✅ Formal Specification: Pluggable Node Value Renderers and Editors**

## **1\. Pluggable Components Overview**

* **Renderers** and **Editors** are **distinct** and **independently pluggable**.

* Both can be **registered per CLR type** or **fallback to a default implementation**.

---

## **2\. Value Renderer**

### **Responsibilities**

* **Render read-only view** of the node’s value in the value cell.

* **Optionally show a hover detail window** or tooltip for additional information.

### **Activation**

* Always active **when the node is not being edited**.

### **Advanced Behavior**

* May show **rich previews** like JSON summaries, formatted HTML, etc.

* May **block tree expansion** when paired with an editor (see Rule 5).

---

## **3\. Value Editor**

### **Responsibilities**

* **Provide edit interface** for the node’s value.

* **Support both inline and modal editing**.

### **Activation**

* Automatically activated **on focus** or **explicitly (click/enter)** depending on control type.

### **Modal Behavior**

* May **launch a modal window** instead of inlining into the table cell.

### **Commit Behavior**

* Validate and apply **on LostFocus**, **Enter**, or **confirmation in modal**.

* Cancel **on Escape** or **cancel button in modal**.

---

## **4\. Non-Leaf (Object) Node Support**

### **Behavior When Both Renderer and Editor Exist**

* **Do not show child nodes** in the tree.

* The **renderer/editor takes full responsibility** for presenting and editing the node.

### **Behavior When Only Renderer Exists**

* **Renderer shows a preview**, **children remain visible and expandable**.

### **Behavior When No Renderer/Editor Exists**

* **Show children** as standard tree nodes.

---

## **6\. Integration with Node Rendering Logic**

* **If Editor Exists for Node:**

  * **Block tree expansion** and use editor \+ renderer for display/editing.

* **If Only Renderer Exists:**

  * **Show renderer preview and expand children.**

* **If Neither Exists:**

  * **Show children as usual.**

---

# **✅ Best Practices for Practical Array Node Editing**

## **1\. Consistent Item Type Handling**

* **All items share the same CLR type**, derived from the **array node’s schema**.

* Editors should **reuse the editor registered for the item type**, no need to re-resolve per item.

---

## **2\. User-Friendly Controls**

* **Add**:

  * Button to **append a new default item** (default(T)) at the end.

  * Optional **Insert Above/Below** buttons next to each item.

* **Remove**:

  * Button to **remove individual items**, possibly with **confirmation** for non-trivial deletions.

* **Reorder**:

  * **Move Up/Down** buttons or **drag & drop** to change order.

---

## **3\. Inline vs. Modal Editing**

* **Inline editing** works well for **simple types** (string, int, bool).

* **Modal editing** is preferred for **complex objects** to avoid clutter and make room for structured forms.

---

## **4\. Visual Hierarchy**

* **Indent array items slightly** from the array node label.

* **Number or label each item** (e.g., "Item 1", "Item 2") for orientation.

---

## **5\. Batch Operations (Optional)**

* **Clear All**, **Duplicate All**, **Paste from Clipboard** options for advanced users.

* **Multi-Select \+ Bulk Remove** (optional if UX budget allows).

---

## **6\. Keyboard Navigation**

* **Arrow keys** navigate between items.

* **Enter** starts inline edit for the selected item.

* **Ctrl+Up/Down** or **Alt+Up/Down** for moving items.

---

## **7\. Undo-Friendly Design**

* **Treat array operations (add, remove, reorder) as atomic actions** to support undo/redo in the future.

---

## **8\. Performance Considerations**

* **Virtualize rendering** if the array grows large.

* **Lazy-load item editors** only when visible.

---

## **9\. Feedback and Validation**

* Provide **immediate feedback** on invalid items.

* Optionally highlight **invalid item boundaries**.

---

## **10\. Compact but Expandable View**

* Show **collapsed summary views** (e.g., "3 items")  
   with **expand/collapse toggle** for managing large arrays.

---

## **✅ Example Layout**

`Users (Array)`  
 `├─ 1 [ Username: "Alice"  IsActive: true  ] [▲][▼][✎][✖]`  
 `├─ 2 [ Username: "Bob"    IsActive: false ] [▲][▼][✎][✖]`  
 `└─ [+ Add New User]`

---

# **✅ Practical Array Node Editing**

## **1\. Array Item Representation**

* **Numbered Display**

  * Each item is displayed as **"N"**, where **N** is its 1-based position in the array.

## **2\. Selectable Items with Multi-Select Support**

* **Single or Multi-Selection** of array items.

* **Selection** visually indicated (e.g., highlight).

## **3\. Keyboard-Based Manipulation**

* **Delete Key**

  * **Deletes all selected items**.

  * **Optional confirmation** for multi-delete.

* **Insert Key**

  * **Inserts new default(T) item(s) above the first selected item**.

* **Ctrl+C / Ctrl+V (Copy/Paste)**

  * **Copies selected items** as deep clones to clipboard.

  * **Paste** inserts the copied items **above the first selected item**.

## **4\. Pseudo-Item Placeholder**

* A **non-editable placeholder row at the end** labeled **"\[+ Add New Item\]"**.

* Clicking or pressing **Enter on the placeholder** inserts a **default(T)** item **at the end of the list**.

## **5\. Reordering (Optional Extension)**

* **Move Up/Down buttons** or **drag-and-drop** (not mandatory for initial implementation).

## **6\. Visual Hierarchy**

* **Indent array items** slightly under the array node label.

* **Highlight invalid items** with red background if validation fails.

## **7\. Batch Operations**

* **Multi-select Delete, Insert, Copy, Paste** as described.

* Paste can insert **multiple copied items** at once.

## **8\. Undo/Redo Readiness**

* Treat **delete, insert, reorder, and paste** as **atomic actions** for future undo/redo support.

## **9\. Consistency with Tree Editing Behavior**

* Array item editing **follows the same keyboard and validation rules** as other nodes (see earlier keyboard control spec).

# **✅ Revised Array Item Editing and Visualization Specification**

## **1\. Edit Activation for Array Items**

* **Edit mode does not start automatically** when selecting or navigating.

* **Edit mode starts explicitly by:**

  * **Double-clicking** the item.

  * **Pressing Enter** on the selected item.

---

## **2\. Editor Assignment Check**

* **If an editor is assigned for the array item CLR type:**

  * **Show only the editor** (inline or modal) **when activated**.

  * **Do not show underlying subnodes** unless the editor chooses to reveal them.

* **If no editor is assigned for the array item CLR type:**

  * **Show the item as expandable**, displaying its **subnodes** in the tree as usual.

---

## **3\. Practical Outcome Example**

| Array Item Type | Editor Registered? | Behavior |
| ----- | ----- | ----- |
| ComplexObject | **Yes** | Requires **explicit activation**, hides children |
| ComplexObject | **No** | **Expandable**, shows subnodes inline |
| Primitive (e.g., string, int) | **Yes/No** | Same rules apply, but **activation required** |

---

# **✅ Refined Multi-Selection and Edit Mode Behavior**

## **1\. Selection Model**

### **Single Select**

* **Click** on an item **selects it** (clears previous selection).

### **Multi-Select with Shift \+ Arrows**

* **Shift \+ Arrow Up/Down** expands the selection **up or down** from the **current active item**.

### **Mouse-Driven Multi-Select (Explorer-Like)**

* **Ctrl \+ Click**: Toggles selection of the clicked item without clearing others.

* **Shift \+ Click**: Selects **range from active to clicked**.

* **Plain Click**: Selects the clicked item and **clears others**.

---

## **3\. Formalized Edit Activation Rule**

| Action | Behavior |
| ----- | ----- |
| **Click or Arrow Navigation** | **Selects only, no edit** |
| **Shift \+ Arrow** | **Multi-selects, no edit** |
| **Ctrl \+ Click / Shift \+ Click** | **Multi-selects, no edit** |
| **Double Click or Enter** | **Activates Edit Mode on active** |

---

## **4\. Summary of User-Friendly Behavior**

* **Selection and editing are decoupled**.

* **Double click or Enter required to edit**.

* **Explorer-like multi-selection supported**.

* **No accidental edit when navigating or selecting**.

---


# DOM Tree Editor UX

# Original reqs

C\# WPF .net 8

Two columns. In the left column a hierarchy dom unfoldable tree (each line one node), the right column shows the node value. Pluggable value renderers for custom node value clr type. When the right column cell is clicked, it changes into an edit field filling the cell, editing the node value. The edit field is smart, selecting different edit controls based on the clr data type of the dom node value. Text field for texts and numeric types, dropdown with text values for enums, checkbox for booleans etc. Pluggable custom edit controls for custom types. For array nodes, ability to add, remove and insert new item of clr type as specified in the schema. Sample dom tree to fill the table. Pluggable renderers and editor controls for primitive types.

Keyboard navigation is mandatory. cursor up/down moves selection row up/down. Numpad plus/minus unfolds/folds node. enter or click activates edit mode for a row (not always, see below for more details). Once in edit mode, esc cancels it without changing node value, enter confirms and updates the node and exits edit mode. Whole table starts unfolded, folding is not persistent, kept only until the app restarts. While in edit mode, table navigation/folding is disabled. The edit mode of a row showing primitive type should start automatically on enter and validate and apply on leave. For text fields on enter all the text should be initially selected so that when typing starts, it overwrites previous value.

For primitive types no enter key now needed to activate edit mode, automatic on get focus. Esc returns back the original unmodified value captured on getting focus. Validate and apply on lost focus. Some edit controls might capture the row navigation keys while in edit mode (like enum dropdown) so some confirmation key or lost focus event is needed to exit edit mode and validate and apply. Those also require some edit mode activation event (not just “got focus”) to allow for smooth row navigation over them if the user does not want to change value. Plain single line text fields or checkboxes should not block the row navigation. Pls suggest other ui best practices for pleasant keyboard control.

Row height is primarily controlled by the node value renderer \- can be multiple lines. Row is always at least one text line high. Node names in the left column should wrap to be always displayed in full (all characters of the string). Schema provides concrete clr type for each dom element. 

Edit mode entering and update on value change should be the responsibility of the editor control per type. For example text/numeric text controls should wait for Enter key to apply change when in editing mode (when showing blinking cursor). bool checkbox should update immediately if clicked. Enum dropdown should open on click (or enter key) and update on value selected by click or by pressing enter (while key up/down moves the selection within the dropdown). Editor control is also responsible for validation. Invalid value should be indicated by a reddish indicator (underlining or edit field background etc.)

Live filter the node names by string (contains, case insensitive).

No multiple simultaneous edits. Selecting another node should not be possible until the edit of the currently selected node is finished.

The JsonElement in the ValueNode is always a json primitive type. CLR type for the ObjectNode is a c\# class that can be deserialized from the subnodes.

Array Editing: Numbered items. Delete key should delete selected. Insert key should insert empty default above selected. Copy/paste with multiselect. Paste inserts above selection. selectable non-editable pseudo item placeholder shown at the end of list to support inserting new stuff at end.

# 

# **✅ Executive Summary: WPF-Based Hierarchical DOM Table Editor**

## **🧩 What It Is**

A **WPF desktop user interface component** that allows **interactive browsing, filtering, rendering, and editing** of **hierarchical configuration data (DOM tree)**.  
 It is designed to **manage real-world JSON-like structured data**, supporting **complex objects, arrays, and primitive values**.

---

## **🏗️ Core Features**

1. **Hierarchical Tree-Table Layout**

   * Two columns: **Node Name** and **Node Value**.

   * Expandable/collapsible **tree structure** with support for objects and arrays.

2. **Advanced Editing Capabilities**

   * **Pluggable type-specific editors** (text, number, bool, enum, object, array).

   * **Validation with error feedback**.

   * **Modal or inline editing** based on control type.

3. **Array Item Management**

   * **Numbered display**, **Add/Delete/Insert**, **Multi-selection**.

   * **Copy/Paste** between arrays **of the same item type only**.

   * **Pseudo-item at end** for quick appending.

4. **Powerful Keyboard Navigation**

   * **Arrow navigation**, **multi-select**, **edit activation by Enter/Double-Click**.

   * **Copy/Delete/Insert keyboard shortcuts**.

   * **Filter clearing by Escape**.

5. **Live Filtering**

   * **Instant search** on **node names** with **context-preserving expansion**.

6. **Separation of Concerns**

   * **DomTableEditorViewModel** for global state.

   * **DomNodeViewModel** per node with cached CLR type.

   * **ArrayItemCollectionViewModel** for array-specific logic.

   * **ClipboardViewModel** for cross-array copy/paste control.

7. **Customizable Renderers and Editors**

   * **Independent rendering and editing services** for all types.

   * **Hover details** and **summary previews** optional.

---

# **✅ Suggested DOM Editor Layout**

`┌────────────────────────────────────────────────────────────┐`  
`│  Toolbar (Filter Box, Clipboard Buttons, Optional Legend)   │`  
`├────────────────────────────────────────────────────────────┤`  
`│  Tree/Table View                                           │`  
`│  ┌──────────────────────────────────────────────────────┐ │`  
`│  │ Node Name      |  Node Value (Rendered or Editor)     │ │`  
`│  │------------------------------------------------------│ │`  
`│  │ Root           |  {Object Summary or Expand Button}   │ │`  
`│  │ ├─ Hostname    |  "localhost" (Editable)              │ │`  
`│  │ ├─ Port        |  8080 (Editable)                     │ │`  
`│  │ └─ Users       |  [Array Summary or Expand Button]    │ │`  
`│  │    ├─ Item 1   |  {Object Summary or Expand Button}   │ │`  
`│  │    └─ [+ Add]  |  (Pseudo Item for Inserting New)     │ │`  
`│  └──────────────────────────────────────────────────────┘ │`  
`├────────────────────────────────────────────────────────────┤`  
`│  Validation/Error Message Panel (Optional)                 │`  
`└────────────────────────────────────────────────────────────┘`

---

## **✅ Functional Areas**

### **1\. Toolbar**

* **Filter Box**:

  * Live search on node names.

  * Esc clears the filter.

* **Clipboard Buttons**:

  * Copy, Paste (only active on valid targets).

* **Optional Legend or Help Button**

---

### **2\. Main Table View**

* **Tree/Table Hybrid**:

  * Node hierarchy with expansion buttons.

  * Two columns:

    * **Node Name** (wraps to fit, expandable hierarchy).

    * **Node Value** (rendered or editable).

* **Renderer or Editor shown inline or in modal**, based on the node's type and activation.

---

### **3\. Array Editing Features**

* **Numbered Items**

* **Add/Remove/Insert Buttons**

* **Pseudo-item at the end to add new entries**

---

### **4\. Validation/Error Feedback Panel**

* **Optional bottom panel** showing:

  * Validation messages.

  * Errors or warnings about current edit.

  * Clipboard operation restrictions (optional).

---

## **✅ Navigation and Focus**

* **Arrow keys** navigate between rows.

* **Enter or Double-Click** starts editing.

* **Tab/Shift+Tab** move between editable fields.

* **Esc cancels edit or clears filter**.

---

## **✅ Screen Scaling and Responsiveness**

* **Tree/Table resizes** with window.

* **Toolbar stays at the top**.

* **Error panel stays at the bottom**, collapsible or inline.

# Example DOM editor layout

\<Window x:Class="DomEditorApp.MainWindow"  
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"  
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"  
        Title="DOM Table Editor" Height="600" Width="800"\>

    \<DockPanel\>

        \<\!-- Toolbar \--\>  
        \<StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="5"\>  
            \<TextBox Width="200" Margin="0,0,10,0"   
                     PlaceholderText="Filter..."  
                     Text="{Binding FilterViewModel.FilterText, UpdateSourceTrigger=PropertyChanged}"/\>  
            \<Button Content="Copy" Command="{Binding CopyArrayItemCommand}" Margin="0,0,5,0"/\>  
            \<Button Content="Paste" Command="{Binding PasteArrayItemCommand}"/\>  
        \</StackPanel\>

        \<\!-- Main Table / Tree View \--\>  
        \<Grid\>  
            \<Grid.Resources\>  
                \<\!-- You can define your DataTemplates here \--\>  
            \</Grid.Resources\>

            \<TreeView ItemsSource="{Binding FilteredViewModels}"\>  
                \<TreeView.ItemTemplate\>  
                    \<HierarchicalDataTemplate ItemsSource="{Binding Children}"\>  
                        \<Grid\>  
                            \<Grid.ColumnDefinitions\>  
                                \<ColumnDefinition Width="2\*"/\>  
                                \<ColumnDefinition Width="3\*"/\>  
                            \</Grid.ColumnDefinitions\>

                            \<\!-- Node Name \--\>  
                            \<TextBlock Grid.Column="0" Text="{Binding DomNode.Name}" TextWrapping="Wrap"/\>

                            \<\!-- Node Value \--\>  
                            \<ContentControl Grid.Column="1"   
                                            Content="{Binding}"   
                                            ContentTemplateSelector="{StaticResource EditorTemplateSelector}"/\>

                        \</Grid\>  
                    \</HierarchicalDataTemplate\>  
                \</TreeView.ItemTemplate\>  
            \</TreeView\>  
        \</Grid\>

        \<\!-- Validation / Error Panel \--\>  
        \<TextBlock DockPanel.Dock="Bottom"   
                   Text="{Binding ValidationMessage}"   
                   Foreground="Red"   
                   Margin="5"  
                   Visibility="{Binding HasValidationMessage, Converter={StaticResource BoolToVisibilityConverter}}"/\>

    \</DockPanel\>

\</Window\>

# **DOM tree node classes**

    public abstract class DomNode  
    {  
        public string Name; { get; }  
        public DomNode? Parent { get; set; }  
    }

    public class ObjectNode : DomNode  
    {  
        public Dictionary\<string, DomNode\> Children { get; } \= new();  
    }

    public class ArrayNode : DomNode  
    {  
        public List\<DomNode\> Items { get; } \= new();  
    }

    public class ValueNode : DomNode  
    {  
        public JsonElement Value { get; set; }  
    }

# 

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

## **✅ Purpose**

* Centralize **all state management**:

  * DOM data

  * ViewModel caching

  * Selection and editing state

  * Filter state

  * UI expansion state

---

## **✅ Core Responsibilities**

1. **Hold the Root DOM Node**

2. **Manage ViewModels for All Nodes**

3. **Track Current Selection and Editing**

4. **Manage Filtering**

5. **Coordinate Table Rendering State**

# Node Value Renderer and Editor

public interface INodeValueRenderer

{

   FrameworkElement BuildRendererView(DomNode node);

   FrameworkElement? BuildHoverDetailsView(DomNode node);

}

​

public interface INodeValueEditor

{

   bool IsModal { get; }

   FrameworkElement BuildEditorView(DomNode node);

   bool TryGetEditedValue(out object newValue);

   void CancelEdit();

   void ConfirmEdit();

}

​

## DomNodeViewModel

​

public class DomNodeViewModel : INotifyPropertyChanged  
{  
   // Reference to the wrapped DOM node  
   public DomNode DomNode { get; }  
​  
   // Whether the node is currently selected  
   public bool IsSelected { get; set; }  
​  
   // Whether the node is expanded in the tree  
   public bool IsExpanded { get; set; }  
​  
   // Whether the node is being edited  
   public bool IsEditing { get; set; }  
​  
   // Whether the node is currently visible (after filtering)  
   public bool IsVisible { get; set; } \= true;  
​  
   // Temporary editable value  
   public object? EditingValue { get; set; }  
​  
   // Whether current editing value is invalid  
   public bool HasValidationError { get; set; }  
​  
   // Optional error message for validation  
   public string? ValidationErrorMessage { get; set; }  
​  
   // Active editor instance, if editing  
   public INodeValueEditor? EditorInstance { get; set; }  
​  
   // Renderer instance, if needed  
   public INodeValueRenderer? RendererInstance { get; set; }  
​  
   // Child ViewModels (for objects/arrays)  
   public List\<DomNodeViewModel\> Children { get; } \= new();  
​  
   // Cached CLR type of the node's value  
   public Type ValueClrType { get; }  // Cached on ViewModel creation  
​  
​  
/\*  
​  
Purpose  
\* Centralize all state management:  
\* DOM data  
\* ViewModel caching  
\* Selection and editing state  
\* Filter state  
\* UI expansion state  
​  
Core Responsibilities  
\* Hold the Root DOM Node  
\* Manage ViewModels for All Nodes  
\* Track Current Selection and Editing  
\* Manage Filtering  
\* Coordinate Table Rendering State

Behavioral Notes  
\* ViewModels are created on initialization, using the schema resolver to set ValueClrType.  
\* Filter recomputes the FilteredViewModels and auto-expands relevant parents.  
\* Selection state updates SelectedNodes and ensures only one ActiveEditor at a time.  
\* Clear() resets all state when the DOM is replaced.

\*/  
​  
   /// \<summary\>  
   /// Manages global editor state including node viewmodels, selection, filtering, and editing.  
   /// \</summary\>  
   public class DomTableEditorViewModel  
   {  
       /// \<summary\>  
       /// Root DOM node of the loaded configuration.  
       /// \</summary\>  
       public ObjectNode RootNode { get; private set; }  
​  
       /// \<summary\>  
       /// Cache of viewmodels for all nodes in the DOM tree.  
       /// \</summary\>  
       public Dictionary\<DomNode, DomNodeViewModel\> NodeViewModels { get; private set; } \= new();  
​  
       /// \<summary\>  
       /// Currently selected node(s) in the editor.  
       /// \</summary\>  
       public List\<DomNodeViewModel\> SelectedNodes { get; private set; } \= new();  
​  
       /// \<summary\>  
       /// Currently active editor, if any.  
       /// \</summary\>  
       public INodeValueEditor? ActiveEditor { get; private set; }  
​  
       /// \<summary\>  
       /// Current live filter text.  
       /// \</summary\>  
       public string FilterText { get; private set; } \= string.Empty;  
​  
       /// \<summary\>  
       /// List of nodes visible after filtering.  
       /// \</summary\>  
       public List\<DomNodeViewModel\> FilteredViewModels { get; private set; } \= new();  
​  
       /// \<summary\>  
       /// Provides schema-based CLR type resolution.  
       /// \</summary\>  
       public DomSchemaTypeResolver SchemaTypeResolver { get; private set; }  
​  
       /// \<summary\>  
       /// Initializes the viewmodel with the provided DOM root and schema resolver.  
       /// \</summary\>  
       public void Initialize(ObjectNode root, DomSchemaTypeResolver schemaResolver);  
​  
       /// \<summary\>  
       /// Clears the current editor state and viewmodel cache.  
       /// \</summary\>  
       public void Clear();  
​  
       /// \<summary\>  
       /// Selects a single node.  
       /// \</summary\>  
       public void SelectSingle(DomNodeViewModel node);  
​  
       /// \<summary\>  
       /// Selects a range of nodes from start to end.  
       /// \</summary\>  
       public void SelectRange(DomNodeViewModel start, DomNodeViewModel end);  
​  
       /// \<summary\>  
       /// Toggles selection state for a node.  
       /// \</summary\>  
       public void ToggleSelection(DomNodeViewModel node);  
​  
       /// \<summary\>  
       /// Begins editing the specified node.  
       /// \</summary\>  
       public void BeginEdit(DomNodeViewModel node);  
​  
       /// \<summary\>  
       /// Confirms the current edit, applying changes.  
       /// \</summary\>  
       public void ConfirmEdit();  
​  
       /// \<summary\>  
       /// Cancels the current edit, reverting changes.  
       /// \</summary\>  
       public void CancelEdit();  
​  
       /// \<summary\>  
       /// Applies a live filter to the node tree.  
       /// \</summary\>  
       public void ApplyFilter(string filterText);  
​  
       /// \<summary\>  
       /// Clears the active filter and resets visibility.  
       /// \</summary\>  
       public void ClearFilter();  
​  
       /// \<summary\>  
       /// Retrieves the viewmodel associated with a given DOM node.  
       /// \</summary\>  
       public DomNodeViewModel GetViewModelForNode(DomNode node);  
   }

​

# **✅ User Interaction Model for DOM Table Editor**

## **1\. Interaction Groups**

### **1.1 Tree Navigation**

* **Arrow Up/Down**: Move selection **up/down** the visible, filtered node list.  
    
* **Shift \+ Arrow Up/Down**: Extend selection **up/down** (multi-select).

### **1.2 Node Selection**

* **Click on Node Row**:  
    
  * **Single select** that node.  
      
  * **No edit starts automatically**.


* **Ctrl \+ Click**: **Toggle multi-select** on node.  
    
* **Shift \+ Click**: **Range select** from active to clicked.

### **1.3 Editing Activation**

* **Enter or Double-Click on Selected Node**:  
    
  * **Starts editor** if one exists for the node’s type.

---

## **2\. Editing Behavior**

### **2.1 For Primitive or Inline Editable Types (TextBox, Checkbox)**

* **Starts editing inline** after activation.  
    
* **Applies on LostFocus or Enter**.  
    
* **Cancels on Escape**.

### **2.2 For Complex or Modal Editors (ComboBox, Dialog)**

* **Starts modal or dropdown** on activation.  
    
* **Applies on Selection Change, LostFocus, or Confirm Button**.  
    
* **Cancels on Escape or Cancel Button**.

### **2.3 Editing Lifecycle**

* **BeginEdit(node)**: Prepares the editor instance, stores initial value.  
    
* **ConfirmEdit()**: Validates and commits changes.  
    
* **CancelEdit()**: Restores the original value.

---

## **3\. Array Editing**

### **3.1 Item Manipulation**

* **Delete Key**: Deletes selected items.  
    
* **Insert Key**: Inserts default item(s) above selection.  
    
* **Ctrl+C / Ctrl+V**: Copies and pastes selected items above selection.  
    
* **Pseudo-Item at End**: Inserts at end on click or Enter.

### **3.2 Editing Activation**

* **Requires Enter or Double-Click** (never starts on navigation alone).  
    
* **Expands children** if no editor is assigned for the array item type.

---

## **4\. Filtering Interaction**

### **4.1 Filter Application**

* **Live updates** as the user types in the filter box.  
    
* **Matches node names** (partial, case-insensitive).  
    
* **Expands parents** of matching nodes to show context.

### **4.2 Clear Filter**

* **Esc clears filter** when filter box is focused.  
    
* **Collapses tree** back to default state.

---

## **5\. Visual Feedback**

* **Highlight** for current selection.  
    
* **Expansion indicator** for expandable nodes.  
    
* **Edit mode indicator** (e.g., border or background change).  
    
* **Validation error indicator** (e.g., red border or tooltip).

---

## **6\. User Guidance**

* **Optional legend** explaining:  
    
  * Arrow Keys \= Navigate  
      
  * Enter/Double-Click \= Edit  
      
  * Esc \= Cancel or Clear Filter  
      
  * Ctrl+Click / Shift+Click \= Multi-Select  
      
  * Delete/Insert/Ctrl+C/Ctrl+V \= Array Editing

# **✅ Formal Copy/Paste Specification for Array Node Items**

## **1\. Copy Behavior**

### **Scope**

* **Applies only to array items.**

### **Rules**

* Only **currently selected items** in the same array can be copied.  
    
* Only allowed if **all selected items share the same CLR type**.

### **Clipboard Content**

* **Deep clones** of the selected array items.  
    
* **Clipboard remembers the CLR type** of the copied items for validation on paste.

---

## **2\. Paste Behavior**

### **Scope**

* **Paste target must be an array**.  
    
* **Paste allowed only if:**  
    
  * The **target array’s item type matches** the clipboard’s item type.  
      
  * The clipboard contains **only one type** of item.

### **Paste Target Position**

* **Inserts items above the first selected item** if selection exists in the array.  
    
* **Otherwise inserts at the end**.

### **Invalid Paste Handling**

* **Paste is disabled** or **has no effect** if:  
    
  * The target is **not an array**.  
      
  * The target array’s **item type does not match** the clipboard’s item type.  
      
  * The clipboard **contains mixed types**.

# Numeric Value Editor

public class NumericValueEditor : INodeValueEditor  
public class NumericEditorViewModel : INotifyPropertyChanged, IDataErrorInfo  
\<DataTemplate x:Key="NumericEditorTemplate"\>  
`BoolToBrushConverter` converts validation state to red border or default.

# 

# 

# **✅ Behavior Description**

* **UI Control**:

  * **Plain editable `TextBox`** with **freely typed text**.

* **Conversion**:

  * Converts text to **int, double, etc.** based on **`ValueClrType`**.

* **Validation**:

  * **Red border or error tooltip** if invalid.

* **Apply**:

  * On **LostFocus** or **Enter** if valid.

* **Cancel**:

  * On **Escape**, restores original value.

## **✅ Where Node Navigation Logic Should Be Stored**

### **📌 DomTableEditorViewModel**

* **Owns the selection state.**

**Provides APIs to change selection**, for example:

 csharp  
CopyEdit  
`public void MoveSelectionUp();`  
`public void MoveSelectionDown();`  
`public void SelectSingle(DomNodeViewModel node);`  
`public void SelectRange(DomNodeViewModel start, DomNodeViewModel end);`  
`public void ToggleSelection(DomNodeViewModel node);`

* 

### **📌 UI Layer Responsibilities**

* **Handles key and mouse events** (Arrow Up/Down, Click, Shift+Click, etc.).

* **Calls ViewModel APIs** to update selection.

* **Updates visual state** (highlighting selected rows).

**Example Flow:**

* User presses **Arrow Down** → **UI calls MoveSelectionDown() on DomTableEditorViewModel** → **ViewModel updates selection**.

---

## **✅ Where Array Editing Logic Should Be Stored**

### **📌 DomTableEditorViewModel**

* **Coordinates which array is active** and **tracks selected array items**.

### **📌 Dedicated Array Manipulation Service**

Example:

csharp  
CopyEdit  
`public static class DomArrayEditingService`  
`{`  
    `public static void DeleteSelectedItems(DomArrayNode arrayNode, List<DomNodeViewModel> selectedItems);`  
    `public static void InsertDefaultItemAboveSelection(DomArrayNode arrayNode, List<DomNodeViewModel> selectedItems, Type itemType);`  
    `public static void PasteItemsAboveSelection(DomArrayNode arrayNode, List<DomNodeViewModel> selectedItems, DomEditorClipboard clipboard);`  
`}`

### **📌 UI Layer Responsibilities**

* **Listens for Delete, Insert, Ctrl+C, Ctrl+V key events**.

* **Calls methods on DomArrayEditingService** passing the active array and selection.

## **✅ Example Interaction Flow for Array Editing**

1. User **presses Delete** while an array is selected.

2. UI **detects the key** and **calls DeleteSelectedItems()** on `DomArrayEditingService`.

3. Service **removes the items** and notifies **ViewModel** to refresh state.

# **✅ Recommended MVVM Commands**

| Command | Purpose |
| ----- | ----- |
| **NavigateUpCommand** | Move selection up |
| **NavigateDownCommand** | Move selection down |
| **BeginEditCommand** | Start editing the selected node |
| **ConfirmEditCommand** | Apply the current edit |
| **CancelEditCommand** | Cancel the current edit |
| **DeleteArrayItemCommand** | Delete selected array item(s) |
| **InsertArrayItemCommand** | Insert new default array item above selection |
| **CopyArrayItemCommand** | Copy selected array item(s) to clipboard |
| **PasteArrayItemCommand** | Paste clipboard items above selection in compatible array |
| **ClearFilterCommand** | Clear active filter |
| **ApplyFilterCommand** | Apply live filter based on entered filter text |

# **✅ Complete ViewModel Set**

| ViewModel | Responsibility |
| ----- | ----- |
| **DomTableEditorViewModel** | Manages **global editor state**, filtering, selection, editing lifecycle, clipboard management |
| **DomNodeViewModel** | Wraps a **single DomNode**, holds UI state like IsSelected, IsExpanded, IsEditing, ValueClrType |
| **ArrayItemCollectionViewModel** | Manages **multi-selection and item manipulation** within a single array (optional separation for cleaner array logic) |
| **NumericEditorViewModel** | Provides **validation and live value binding** for numeric text editing |
| **FilterViewModel** | Manages **live filter text** and triggers **filtering logic** in the table editor |
| **ClipboardViewModel** | Tracks **copied array items** and their **item type**, used for validating Paste actions |

---

## **✅ Detailed ViewModel Descriptions**

### **🧱 1\. DomTableEditorViewModel**

* Root state manager for:

  * DOM root reference

  * Node viewmodel cache

  * Global selection state

  * Filter state

  * Active editor

  * Clipboard content

  * Command implementations

---

### **🧱 2\. DomNodeViewModel**

* Wraps a **single DomNode** and holds:

  * Node reference

  * Cached `ValueClrType`

  * IsSelected, IsExpanded, IsEditing

  * EditingValue

  * Validation state

  * Child viewmodels (for objects/arrays)

---

### **🧱 3\. ArrayItemCollectionViewModel (optional, but recommended)**

* Wraps **an ArrayNode** and provides:

  * List of **item viewmodels**

  * **Multi-selection state**

  * **Array-specific commands** like Add, Remove, Copy, Paste

---

### **🧱 4\. NumericEditorViewModel**

* Wraps **a numeric input session** with:

  * String **InputText**

  * Target **CLR type**

  * **Validation error messages**

  * **Parsed value retrieval**

---

### **🧱 5\. FilterViewModel**

* Holds **filter text state**.

* Raises events or triggers filtering **in DomTableEditorViewModel**.

---

### **🧱 6\. ClipboardViewModel**

* Tracks **currently copied array items**.

* Tracks their **common CLR type**.

* Provides **paste validation** methods.

---

## **✅ Relationship Hierarchy Diagram**

swift  
CopyEdit  
`[ DomTableEditorViewModel ]`    
 `├─ NodeViewModels : Dictionary<DomNode, DomNodeViewModel>`  
 `├─ SelectedNodes : List<DomNodeViewModel>`  
 `├─ ActiveEditor : INodeValueEditor?`  
 `├─ FilterViewModel`  
 `├─ ClipboardViewModel`  
 `└─ ArrayItemCollectionViewModel (if selected node is ArrayNode)`  
      `├─ ItemViewModels : List<DomNodeViewModel>`  
      `├─ SelectedItems : List<DomNodeViewModel>`  
      `└─ Array Manipulation Commands`

---

## **✅ Detailed Relationships and Usage Flow**

### **1\. DomTableEditorViewModel**

* **Central controller** for:

  * **Navigation**, **Selection**, **Editing**, **Filtering**, **Clipboard**.

* Holds **NodeViewModels cache** to map raw DomNodes to their UI state.

---

### **2\. DomNodeViewModel**

* **Per-node state wrapper**.

* Linked to:

  * **Parent** DomNodeViewModel (if any).

  * **Children** DomNodeViewModels (for ObjectNode or ArrayNode).

---

### **3\. ArrayItemCollectionViewModel**

* **Created on demand** when **an ArrayNode is selected**.

* Manages **multi-selection and manipulation** of **array items only**.

* Delegates to **ClipboardViewModel** for copy/paste validation.

---

### **4\. FilterViewModel**

* **Holds filter text**.

* Notifies **DomTableEditorViewModel** to reapply filtering.

---

### **5\. ClipboardViewModel**

* **Tracks copied array items** and their **item CLR type**.

* Provides **CanPasteInto(ArrayNode)** validation method.

---

### **6\. Editor-Specific ViewModels (like NumericEditorViewModel)**

* Created **per editing session**.

* Managed by **ActiveEditor** in **DomTableEditorViewModel**.

* Provides **live binding and validation**.

---

## **✅ Interaction Example: Copy-Paste in Array**

1. **User selects array items** → **ArrayItemCollectionViewModel updates selection**.

2. **User presses Ctrl+C** → **ClipboardViewModel stores copied items**.

3. **User selects target array**.

4. **UI checks ClipboardViewModel.CanPasteInto(targetArray)**.

5. **User presses Ctrl+V** → **ArrayItemCollectionViewModel pastes items if valid**.

## **✅ Required DataTemplate Types**

### **1\. Renderer Templates (Read-Only View)**

| Template Name | Use Case |
| ----- | ----- |
| **PrimitiveRendererTemplate** | Displays strings, numbers, bools, enums as plain text |
| **ObjectSummaryRendererTemplate** | Optional summary view for complex objects (e.g. "User: Alice") |
| **ArraySummaryRendererTemplate** | Optional summary view for array nodes (e.g. "3 items") |
| **DefaultRendererTemplate** | Fallback when no specific renderer is defined |

---

### **2\. Editor Templates (Editable View)**

| Template Name | Use Case |
| ----- | ----- |
| **StringEditorTemplate** | Single-line text editing |
| **NumericEditorTemplate** | Validated number editing (int, double) |
| **BooleanEditorTemplate** | CheckBox for bool values |
| **EnumEditorTemplate** | ComboBox for enum values |
| **ObjectEditorTemplate** | Inline or modal editor for complex objects |
| **ArrayEditorTemplate** | Inline editor for array manipulation |
| **DefaultEditorTemplate** | Fallback when no specific editor is defined |

---

## **✅ Additional Utility Templates**

| Template Name | Use Case |
| ----- | ----- |
| **ValidationErrorTemplate** | Inline validation message or styling |
| **HoverDetailsTemplate** | Preview shown when hovering over value |

## **ViewModel Construction Timing**

### **🟢 Eager Construction**

* **Builds the entire tree of DomNodeViewModels upfront** when:

  * A **new DOM is loaded**, or

  * The **filter is cleared** and the tree needs a full refresh.

## **ViewModel Lifetime Management**

* **DomNodeViewModels live as long as their DOM nodes are valid.**

* **ViewModels are cached in DomTableEditorViewModel.NodeViewModels dictionary**.

* **Cache is cleared** when:

  * A **new DOM** is loaded.

  * The **editor is reset or cleared**.

---

## **3\. Child ViewModel Population**

* **ObjectNodeViewModel.Children** and **ArrayNodeViewModel.Children**

  * **Populated together with parent** during eager build

## **Selection and Expansion State Persistence**

* **Selection** and **expansion states** live **inside the ViewModels**, not the DOM.

* Preserved **until cache is cleared** or **ViewModel is rebuilt**.

---

## **5\. Filter and ViewModel Reuse**

* **Filtering never rebuilds ViewModels**, it **toggles their visibility state**.

* **ViewModels are reused** across filtering cycles.

# **✅ Editor Lifecycle Management Specification**

## **1\. Editor Activation**

### **🔹 Trigger Conditions**

* Editors are **not activated automatically** on focus or navigation.

* Editor activation is triggered by:

  * **Enter key** on selected node

  * **Double-click** on selected row

  * **Click on value cell** (only for simple inline editors)

### **🔹 Activation Flow**

* The editor is **looked up by CLR type** via `DomEditorRegistry`.

* The resolved `INodeValueEditor` instance is:

  * **Stored as `ActiveEditor`** in `DomTableEditorViewModel`

  * Also optionally **cached** in the corresponding `DomNodeViewModel`

---

## **2\. Editor Ownership and Location**

| Editor State | Owned By |
| ----- | ----- |
| **Editor instance** | `DomTableEditorViewModel.ActiveEditor` (global singleton) |
| **Per-node edit flag** | `DomNodeViewModel.IsEditing` |
| **Editing value** | `DomNodeViewModel.EditingValue` or bound editor viewmodel |
| **Editor control** | Injected via `ContentTemplateSelector` into the right-hand cell |

---

## **3\. Edit Lifecycle Flow**

### **🔹 Begin Edit**

1. User activates edit.

2. ViewModel calls:

   * `DomEditorRegistry.GetEditor(type)`

   * `editor.BeginEdit(node)`

3. UI replaces renderer with `editor.BuildEditorView()`.

### **🔹 While Editing**

* The editor holds a **temporary input buffer**, not written to the node yet.

* Value is **validated live** via editor-specific ViewModel or binding.

### **🔹 Confirm Edit**

* Triggered by:

  * Lost focus

  * Enter key

  * Modal dialog confirmation

* Editor calls:

  * `TryGetEditedValue(out var newValue)`

  * If valid, updates the **DOM node** and clears `IsEditing`.

### **🔹 Cancel Edit**

* Triggered by:

  * Escape key

  * Cancel button in modal

* Discards input buffer.

* Resets view back to read-only renderer.

---

## **4\. Modal Editors**

* Modal editors **do not render inside the value cell**.

* Instead, they open:

  * A WPF **dialog window**, or

  * An embedded **overlay control** (if desired).

* Modal result is passed back through:

  * `ConfirmEdit()` or `CancelEdit()`

---

## **5\. Constraints**

* **Only one editor active at a time** (`ActiveEditor` is singleton).

* ViewModel must **refuse navigation or selection changes** while editing is active.

* **Editor deactivation clears `ActiveEditor` and `IsEditing`** flags.

## **Editor Template Switching (in View)**

xml  
CopyEdit  
`<ContentControl Content="{Binding}"`  
                `ContentTemplateSelector="{StaticResource EditorTemplateSelector}" />`

* The selector switches between:

  * Renderer (read-only) when not editing

  * Editor template (text box, dropdown, modal trigger) when `IsEditing = true`

# **✅ Error Message Propagation Specification**

## **1\. Where Validation Happens**

* **Inside the INodeValueEditor implementation**

  * Example: `NumericEditorViewModel` validating numeric input.

* **Exposed via properties like**:

  * `HasValidationError`

  * `ValidationErrorMessage`

---

## **2\. Where Errors Are Stored**

* **Per-Node Errors**:

  * **Stored in `DomNodeViewModel`:**

    * `HasValidationError`

    * `ValidationErrorMessage`

* **Global Validation Summary (Optional)**:

  * **Stored in `DomTableEditorViewModel`** if you want to show a summary panel:

    * `ValidationMessage`

    * `HasValidationMessage`

---

## **3\. Error Display in UI**

### **✅ Inline Error Styling**

* **Red border or background** on the editor control.

* Optional **tooltip** with error message.

### **✅ Error Panel (Optional)**

* **Bottom panel or message bar** shows the **latest or first validation error globally**.

---

## **4\. Error Propagation Flow**

1. **Editor detects invalid input**.

2. **Editor updates ViewModel** with:

   * `HasValidationError = true`

   * `ValidationErrorMessage = "Invalid number format"`

3. **View binds to these properties** to:

   * **Apply red styling**

   * **Show tooltip or inline message**

4. **Optional: ViewModel also updates global message** in `DomTableEditorViewModel`.

---

## **5\. Styling Example (Red Border on Error)**

`<TextBox Text="{Binding InputText, UpdateSourceTrigger=PropertyChanged, ValidatesOnDataErrors=True}"`  
         `BorderBrush="{Binding HasValidationError, Converter={StaticResource BoolToBrushConverter}}"`  
         `ToolTip="{Binding ValidationErrorMessage}"`  
         `BorderThickness="1" />`

# **✅ Template Selection Strategy**

## **1\. Why a Template Selector?**

* Allows **binding to a single ContentControl** while dynamically choosing:

  * A **renderer** (read-only view).

  * An **editor** (editable view).

---

## **2\. Selection Triggers**

* **Renderer Mode**:

  * **When IsEditing \== false**, select a **RendererTemplate**.

* **Editor Mode**:

  * **When IsEditing \== true**, select an **EditorTemplate**.

---

## **3\. Template Selector API Example**

csharp  
CopyEdit  
`public class EditorTemplateSelector : DataTemplateSelector`  
`{`  
    `public override DataTemplate SelectTemplate(object item, DependencyObject container)`  
    `{`  
        `if (item is DomNodeViewModel vm)`  
        `{`  
            `if (vm.IsEditing)`  
                `return GetEditorTemplate(vm.ValueClrType);`

            `return GetRendererTemplate(vm.ValueClrType);`  
        `}`

        `return base.SelectTemplate(item, container);`  
    `}`

    `private DataTemplate GetEditorTemplate(Type clrType)`  
    `{`  
        `// Example lookup: "NumericEditorTemplate", "BooleanEditorTemplate"`  
        `var key = clrType.Name + "EditorTemplate";`  
        `return Application.Current.Resources[key] as DataTemplate;`  
    `}`

    `private DataTemplate GetRendererTemplate(Type clrType)`  
    `{`  
        `var key = clrType.Name + "RendererTemplate";`  
        `return Application.Current.Resources[key] as DataTemplate;`  
    `}`  
`}`

---

## **4\. Template Registration in XAML**

xml  
CopyEdit  
`<Window.Resources>`  
    `<DataTemplate x:Key="StringEditorTemplate">...</DataTemplate>`  
    `<DataTemplate x:Key="NumericEditorTemplate">...</DataTemplate>`  
    `<DataTemplate x:Key="BooleanEditorTemplate">...</DataTemplate>`

    `<DataTemplate x:Key="StringRendererTemplate">...</DataTemplate>`  
    `<DataTemplate x:Key="NumericRendererTemplate">...</DataTemplate>`  
    `<DataTemplate x:Key="BooleanRendererTemplate">...</DataTemplate>`

    `<local:EditorTemplateSelector x:Key="EditorTemplateSelector" />`  
`</Window.Resources>`

---

## **5\. Template Usage Example**

xml  
CopyEdit  
`<ContentControl Content="{Binding}"`   
                `ContentTemplateSelector="{StaticResource EditorTemplateSelector}" />`

---

## **6\. Fallback Handling**

* Template selector should **fall back to a DefaultTemplate** if no specific template is registered.

# **✅ Modal Editor Hosting Specification**

## **1\. Why Modal Editors Are Needed**

* **Complex object editors** (e.g., structured forms).

* **Multi-field editors** that don’t fit into a single table cell.

* Editors that require **user confirmation or cancellation**.

---

## **2\. Modal Triggering**

### **✅ Trigger Events**

* **Enter** or **Double-Click** on a node that has a **modal editor registered**.

### **✅ Editor Declaration**

* `INodeValueEditor.IsModal == true`

  * Determines whether the editor **uses modal hosting** or **inline rendering**.

---

## **3\. Modal Hosting Mechanism**

### **✅ Recommended: Dialog Window Hosting**

* **Opens a WPF Window** or **ContentDialog** when editing starts.

* Example API:

csharp  
CopyEdit  
`public interface INodeValueEditor`  
`{`  
    `bool IsModal { get; }`  
    `FrameworkElement BuildEditorView(DomNode node);`  
    `bool TryGetEditedValue(out object newValue);`  
    `void CancelEdit();`  
    `void ConfirmEdit();`  
`}`

### **✅ Host Logic Example**

csharp  
CopyEdit  
`if (editor.IsModal)`  
`{`  
    `var editorView = editor.BuildEditorView(node);`  
    `var dialog = new ModalEditorWindow { Content = editorView };`  
    `if (dialog.ShowDialog() == true)`  
        `editor.ConfirmEdit();`  
    `else`  
        `editor.CancelEdit();`  
`}`

---

## **4\. Modal Window Example**

xml  
CopyEdit  
`<Window x:Class="DomEditorApp.ModalEditorWindow"`  
        `Title="Edit Node" Height="400" Width="600">`  
    `<Grid>`  
        `<ContentPresenter x:Name="EditorContent" />`  
        `<StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Bottom">`  
            `<Button Content="OK" Click="OnConfirm"/>`  
            `<Button Content="Cancel" Click="OnCancel"/>`  
        `</StackPanel>`  
    `</Grid>`  
`</Window>`

---

## **5\. Lifecycle Flow**

1. **BeginEdit()** detects modal editor.

2. **Modal window is opened** with editor view.

3. **User confirms or cancels**.

4. **ConfirmEdit()** or **CancelEdit()** is called.

5. Editor closes and control returns to the table.

---

## **6\. Best Practices**

* **Trap keyboard focus** in modal.

* **Disable background UI** while modal is open.

* **Size window appropriately** based on editor content.

* **Use standard OK/Cancel buttons**.

# **✅ Undo/Redo Readiness Specification**

## **1\. Why This Matters**

* Provides **reversibility** for user mistakes.

* Enables **power features** like step-wise rollback and history tracking.

---

## **2\. When Undo/Redo Should Be Recorded**

### **✅ Atomic Actions to Record**

* **Editing Values**

  * Confirmed changes only (not while typing).

* **Array Operations**

  * Add, Remove, Insert, Paste, Reorder.

* **Structural Changes**

  * Adding/removing nodes (if supported in future).

---

## **3\. How to Structure Change Recording**

| Change Data to Record |
| ----- |
| Reference to the **DomNode** affected |
| **Old Value** before change |
| **New Value** after change |
| **Operation Type** (Edit, Add, Remove, Paste) |

Example Change Record:

`public record DomChange(DomNode Node, object? OldValue, object? NewValue, string OperationType);`

---

## **4\. Where to Store History**

* **DomTableEditorViewModel** maintains two **stacks**:

  * `UndoStack<List<DomChange>>`

  * `RedoStack<List<DomChange>>`

---

## **5\. Undo/Redo Command API**

`public ICommand UndoCommand { get; }`  
`public ICommand RedoCommand { get; }`

`public void Undo();`  
`public void Redo();`

---

## **6\. Applying Changes**

* **Apply change by reverting node value to `OldValue`.**

* **Apply redo by setting node value to `NewValue`.**

* **Trigger re-render or validation as needed.**

---

## **7\. Example Flow**

1. **User edits a value.**

2. **Change is pushed** onto `UndoStack`.

3. **User presses Undo.**

4. **Change is reverted**, and **pushed to RedoStack**.

5. **User presses Redo.**

6. **Change is reapplied**, and **pushed back to UndoStack**.

---

## **8\. Multi-Change Batching**

* Support **grouping multiple changes** (e.g., multi-item paste or delete).

---

## **9\. UI Integration**

* Enable/Disable **Undo/Redo buttons** or **menu items** based on stack state.

---

## **10\. Performance Considerations**

* **Limit stack depth** (e.g., last 100 actions).

* **Discard oldest history** when exceeding limit.

# **✅ UI State Persistence Specification**

## **1\. Why This Matters**

* Provides **a smoother user experience** by **remembering user context** when reopening the editor.

* Supports **session continuity**.

---

## **2\. UI State Elements Worth Persisting**

| State Element | Purpose |
| ----- | ----- |
| **Expanded/Collapsed Nodes** | Restore **tree structure visibility** |
| **Filter Text** | Restore **last applied search** |
| **Selection State** | Restore **selected node(s)** |
| **Last Edit Position** | Restore **active editing context** (optional) |

---

## **3\. Persistence Scope**

### **🟢 Session-Scoped**

* **Remembers state while the application runs**.

* **Resets on application exit or DOM reload**.

---

## **4\. How to Capture State**

### **✅ State Snapshot Example**

`public record DomEditorUiState(`  
    `List<string> ExpandedNodePaths,`  
    `string FilterText,`  
    `List<string> SelectedNodePaths`  
`);`

* **Node Paths** uniquely identify nodes (e.g., `Root/Settings/Port`).

---

## **5\. Save/Restore Methods Example**

`public DomEditorUiState CaptureUiState();`  
`public void RestoreUiState(DomEditorUiState state);`

---

## **6\. Storage Options**

* **In-Memory** for session-only persistence.

  

---

## **7\. When to Capture and Apply State**

* **Capture on Close**, DOM Unload, or User Action (e.g., "Save View State").

* **Apply on Open** or DOM Reload.

---

# **✅ Data Import/Export API Specification**

## **1\. Purpose**

* Allow external **loading** of editable data (e.g., JSON, BSON).

* Allow **saving** of edited data back to **file or memory**.

---

## **2\. Import API**

### **✅ Required Inputs**

* **Serialized Content** (String)

* **Content Type** (JSON5 etc.)

* **Schema Type Resolver** (DomSchemaTypeResolver)

### **✅ Example API**

`ObjectNode LoadDomFromJson(string jsonContent);`

---

## **3\. Integration with Editor**

`void InitializeEditor(ObjectNode root, DomSchemaTypeResolver resolver);`

Initializes **DomTableEditorViewModel** and **ViewModel cache**.

---

## **4\. Export API**

### **✅ Export Methods**

* Export **current DOM state** as JSON5

`string ExportDomToJson(ObjectNode root);`

---

---

## **6\. Usage Example**

### **🟢 Load Workflow**

`var root = LoadDomFromJson(fileContent);`

`editor.InitializeEditor(root, mySchemaResolver);`

### **🟢 Save Workflow**

`var updatedJson = ExportDomToJson(editor.RootNode);`

`File.WriteAllText(filePath, updatedJson);`

---

## **7\. File I/O vs In-Memory Separation**

* **Editor works on `ObjectNode`**, not on file paths.

* **Loading/Saving from/to files** should be **handled externally**.

---

* 


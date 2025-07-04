# **Design Document: Cascading Configuration Editor**

## **1\. Introduction**

This document outlines the design and behavior of a major extension to the JSON editor: the introduction of a **Cascade Project Mode**. The primary goal is to transform the editor from a single-file tool into a powerful system for managing complex, layered configurations, such as those used for different application environments (e.g., base, staging, production).

This mode allows users to view, edit, and understand how multiple layers of JSON files combine to produce a final, effective configuration, while providing intelligent tools to manage these overrides.

## **2\. Core Concepts**

At the heart of the new functionality are a few core concepts that define how layered configurations are loaded, merged, and displayed.

### **2.1. The Cascade Project**

A "Cascade Project" is a collection of configuration layers defined in a single project file (e.g., `my-project.cascade.jsonc`). This file is the entry point for the editor's cascade mode and specifies the name of each layer and the location of its corresponding folder. The order of layers in this file is critical, as it defines the override precedence from lowest to highest.

### **2.2. The Cascade Layer**

A "Cascade Layer" is a logical grouping of configuration settings. In practice, it corresponds to a folder containing one or more `.json` files. For a layer to be valid, files within it **must not** define the same configuration property at the same path. For example, `a.json` and `b.json` within the same layer cannot both define `network.port`. This rule ensures that each piece of data within a layer has a single, unambiguous source file.

### **2.3. The Schema Defaults Layer (Layer 0\)**

Conceptually, the absolute base of every cascade is "Layer 0," which is not a folder but is generated directly from the **default values defined in the schema**. This layer is inherently read-only and ensures that the configuration starts with a complete, valid set of default values. These defaults are only visible if they are not overridden by any subsequent file-based layer.

### **2.4. Merging Concepts**

The editor performs two types of merging to create its views:

* **Intra-Layer Merge:** When loading, all JSON files within a single layer's folder are merged into one unified configuration tree for that layer. This creates a complete picture of the settings defined *only at that level*.  
* **Inter-Layer Merge:** This happens when displaying the "Merged Result." The editor takes the configuration trees from multiple layers (starting with the Schema Defaults Layer) and merges them in order of precedence. Higher-priority layers override lower-priority ones.  
  * **Object Merging:** Objects are deep-merged.  
  * **Array Merging:** An array from a higher-priority layer completely **replaces** any array at the same path from a lower-priority layer.

## **3\. Operational Modes**

The editor will operate in one of two distinct modes, determined by what the user chooses to open.

| **Aspect / Feature** | **Single JSON File Mode** | **Cascade Project Mode** | | **How to Start** | User selects **`File > Open`** and chooses a single `.json` file. | User selects **`File > Open Cascade Project`** and chooses a project definition file (`*.cascade.jsonc`). | | **UI Controls** | Standard UI. All cascade-specific controls are hidden. | Advanced UI is enabled, showing the Layer Selector, View Mode Toggle, and "Origin Layer" column. | | **Data Representation** | A direct, WYSIWYG view of one document. | A dynamic, contextual view of an entire configuration system, showing either a single layer or a merged result of multiple layers. | | **Saving** | `File > Save` overwrites the single open `.json` file. | `File > Save` uses an interactive workflow and smart heuristics to save changes back to the appropriate source files within the active layer's folder, preserving the original file structure. | | **Primary Goal** | Quick editing of individual configuration files. | Managing and understanding complex, multi-environment configurations where inheritance and overrides are key. |

## **4\. User Interface and Interaction**

The Cascade Project Mode introduces several new UI components to manage the layers.

### **4.1. Main UI Controls**

* **Layer Selector (ComboBox):** Located in the main toolbar, this dropdown allows the user to select the **active editing layer**. All edits, additions, and deletions will be applied to this selected layer.  
* **View Mode (CheckBox):** A checkbox that toggles the main grid's display between two views:  
  * **`Show Selected Layer Only`:** A focused view that shows only the configuration values defined in the files of the currently selected layer. This is useful for authoring overrides without distraction.  
  * **`Show Merged Result`:** The primary view that shows the final, effective configuration. It is the result of merging all layers from the Schema Defaults up to and including the layer selected in the Layer Selector.  
* **Origin Layer Column:** A new column in the main grid, only visible in "Merged Result" view. For each row, it displays the name of the layer that provided the final, winning value for that property. This makes it immediately clear where any given value is coming from.

### **4.2. Issues & Search Results Panel**

A new panel will be added to the bottom of the editor. This panel serves two purposes:

* **Issues:** It lists all validation errors and loading warnings. This includes schema violations (e.g., wrong data type), structural problems (e.g., overlapping property definitions), and results from on-demand Integrity Checks. Each issue is a clickable link that navigates the user to the exact location of the problem.  
* **"Search All Layers" Results:** When a project-wide search is performed, this panel displays every single match found across all files in all layers, providing a comprehensive audit trail for any configuration property.

## **5\. Key Feature Behavior**

### **5.1. Editing and Saving**

* **Editing:** All edits are applied to the currently **selected editor layer**. When in "Merged Result" view, if a user modifies an inherited value, an override is automatically created in the active layer. User actions that result in multiple, rapid value changes (e.g., typing in a text field) will be grouped into a single operation on the undo stack.  
* **Assigning New Nodes to Files:** When a user creates a new **top-level configuration section** (e.g., `/security`) that isn't housed in any existing file within the active layer, they are prompted **once** with a dialog to assign it to a source file. The editor suggests a file path based on heuristics, but the user has final control. All subsequent properties added under that same section (e.g., `/security/token`) are **automatically assigned to the same file** without further prompts. This assignment is stored in-memory and only written to disk upon a "Save" action.  
* **Saving:** When saving a layer, the editor uses the in-memory origin map to reconstruct and write only the files that have changed. It will create new files and folders as needed based on the user's assignments.

### **5.2. Copy/Paste Functionality**

The editor will support standard copy/paste operations for nodes within the grid, available via the main Edit menu, context menu, and keyboard shortcuts (`Ctrl+C`, `Ctrl+V`).

* **Copy:** When a user copies a selected node, a **deep clone** of that node and all its children is created and stored in an internal application clipboard.  
* **Paste:** The paste behavior is context-aware and always applies the change to the **currently active editor layer**.  
  * **Pasting on an Array Node:** The node from the clipboard is added as a new element to the end of the target array.  
  * **Pasting on an Object Node:**  
    1. A dialog will appear prompting the user for the **property name** for the new node, defaulting to the name of the node that was copied.  
    2. If a property with that name already exists, the dialog will ask the user to confirm: **"A property named '...' already exists. Do you want to replace it?"** with "Replace" and "Cancel" options.  
    3. The operation (add or replace) is then performed on the active layer's data model.  
* **Undo/Redo:** All paste operations, including a replace (which is an atomic remove-then-add operation), are fully undoable.

### **5.3. Context Menu Actions**

The row context menu is enhanced with cascade-aware commands:

* **`Override Sources` (Submenu):** This menu dynamically lists **all layers** in the project. It uses markers to indicate:  
  * Which layers define a value for the selected property (✔).  
  * Which layer provides the final, **effective** value (✔ **Bold**).  
  * Clicking any layer name in this list instantly switches the active editing layer.  
* **`Reset to undefined`:** This command removes the selected property **from the current editor layer only**. This effectively deletes the override, allowing a value from a lower-priority layer (or the schema default) to become visible.  
* **`Move Node to Different File...`:** This command allows the user to change the source file for a configuration section. This action is **layer-specific**: it only affects the file assignment for the selected node within the currently active editor layer, preventing accidental project-wide changes.

### **5.4. Schema and Reference Handling**

* **Schema Validation:** Validation is context-aware to provide helpful feedback and avoid noise:  
  * **In "Merged Result" View:** All schema rules are fully enforced. A "missing required field" is a critical error, as the final configuration is incomplete. This provides a true health check of the system.  
  * **In "Selected Layer Only" View:** The "missing required field" rule is **ignored**. This is by design, as an override layer is expected to be sparse. Only rules for data quality (type, range, pattern) are enforced on the values that are actually present.  
* **`$ref` Resolution:** References are always resolved against the final, **effective `mergedDisplayRootNode`**. This ensures that references always point to the winning override value, providing the most intuitive and powerful behavior.

### **5.5. Search and Filter**

* **Filter:** The "Filter by name" tool acts as a "Search and Reveal" feature. It searches all nodes in the current view and automatically expands the tree to reveal any matches and their parents, hiding all non-matching nodes.  
* **Standard Search (`Ctrl+F`):** This search operates on the **currently visible view** and will automatically expand the tree to reveal found items. In "Merged Result" mode, it will only find the final, effective values (the overrides). It answers the question, "What is the active value for this property?"  
* **"Search All Layers" (Project-Wide Search):** This is a separate, powerful auditing tool. It searches every file in every layer and lists all matches in the results panel. It answers the question, "Where is this property defined anywhere in my entire project, including masked values?"

### **5.6. Undo and Redo**

Undo/Redo operations are transactional and layer-aware. When a user undoes an action, the editor reverts the change in the specific layer where the edit was originally made, regardless of which view the user is currently in. The UI then refreshes to show the consequence of this data change.

## **6\. Project Auditing: The Integrity Check**

To help users maintain clean, consistent, and understandable projects, the editor provides an on-demand auditing tool.

### **6.1. Workflow**

1. **Initiation:** The user selects **`Tools > Run Integrity Check...`** from the main menu.  
2. **Configuration:** A dialog appears, allowing the user to select which checks to perform. Their selections are saved for future sessions.  
3. **Execution:** The system runs the selected checks against the entire project in the background.  
4. **Reporting:** Results are reported as "Warning" or "Info" level messages in the **Issues Panel**. Each issue is a clickable link that navigates the user to the relevant location.

### **6.2. Available Checks**

* **File Path Consistency Across Layers (Default: On):** The primary check. It warns if a configuration section (e.g., `/database`) is stored in files with different relative paths across different layers (e.g., in `db.json` in one layer but `data.json` in another).  
* **Overlapping Definitions Within a Layer (Default: On):** Verifies that no two files within the same layer define the same property, a rule enforced on load but re-runnable here.  
* **Schema Compliance for All Layers (Default: On):** Validates each layer's individual content against the schema, which can find errors that are masked or hidden in the final merged view.  
* **Property Name Casing Mismatches (Default: Off):** An optional check to find potential typos, warning if `network.timeout` and `network.timeOut` exist in different layers.  
* **Empty Source Files or Folders (Default: Off):** A cleanup check that can find and report empty `.json` files or empty layer directories.

# Cascade loading process

### **The Loading and Processing Pipeline**

The entire process begins when the user opens a cascade project file and ends with the editor UI being fully populated and ready for interaction.

#### **Step 1: Project Definition Loading**

The process starts when the user selects a cascade project file (e.g., `my-project.cascade.jsonc`). This file is a simple JSON object that defines the layers.

* **Action:** The `MainViewModel` reads and parses this project file.

**Input:** A JSON file like this:  
JSON  
{  
  "layers": \[  
    { "name": "Base", "folderPath": "config/base" },  
    { "name": "SiteA", "folderPath": "config/siteA" },  
    { "name": "Development", "folderPath": "config/development" }  
  \]  
}

*   
* **Output:** An in-memory, ordered list of layer definitions. The order (0: Base, 1: SiteA, 2: Development) defines the override precedence.

#### **Step 2: Iterating and Processing Each Layer**

The `MainViewModel` iterates through the layer definitions from the project file, from the lowest priority (Base) to the highest (Development). For each definition, it performs the following sub-steps to create a single, comprehensive `CascadeLayer` object.

A `CascadeLayer` object will hold all information about a single layer: its name, path, index, its complete configuration tree, and the origins of its data.

#### **Step 3: File Discovery and Parsing (Within a Single Layer)**

For each layer, the application looks inside its specified folder.

* **Action:** The system recursively scans the layer's `folderPath` for all `*.json` files.  
* **Processing:** For each file found, it uses the existing `IJsonDomParser` from `WpfEditorGemini` to parse the JSON content into a `DomNode` tree.  
* **Output:** A list of `SourceFileInfo` objects for the layer. Each object contains:  
  * The full file path.  
  * The relative path within the layer's folder (e.g., `network/firewall.json`).  
  * The root  
     `DomNode` of the parsed JSON content.

#### **Step 4: Intra-Layer Merge & Origin Tracking**

This is the most critical step in processing a single layer. All the individual `DomNode` trees from the layer's files are merged into one unified tree representing the entire layer.

* **Action:** A new service, let's call it `IntraLayerMerger`, processes the list of `SourceFileInfo` objects.  
* **Initialization:**  
  1. It creates a new, empty `ObjectNode` which will become the layer's single, unified `LayerConfigRootNode`.  
  2. It creates an empty origin map for the layer: `IntraLayerValueOrigins`, which is a `Dictionary<string, string>` that maps a DOM path to the relative file path it came from.  
* **Processing:**  
  1. The list of `SourceFileInfo` objects is sorted alphabetically by relative path. This ensures a deterministic process.  
  2. The service iterates through each source file's `DomNode` tree and performs a deep merge into the `LayerConfigRootNode`.  
  3. **Origin Tracking:** As each property and value is merged, its path is recorded in the `IntraLayerValueOrigins` map. For example, when merging a `timeout: 30` property from `network/settings.json` into the path `/network/timeout`, the map entry `{ "/network/timeout": "network/settings.json" }` is created.  
  4. **Error Handling:** During the merge, if a property is about to be written to a path that already exists in the `LayerConfigRootNode`, it signifies an overlapping definition. An error is logged in the new "Issues Panel", noting the conflict between the incoming file and the file that originally defined the property (retrieved from the `IntraLayerValueOrigins` map).  
* **Output:** A single, fully populated `CascadeLayer` object containing the final `LayerConfigRootNode` and its complete `IntraLayerValueOrigins` map.

#### **Step 5: Finalizing the In-Memory State**

After steps 3 and 4 have been repeated for every layer defined in the project file, the `MainViewModel` holds the final, complete data structure.

* **State:** The `MainViewModel` now has a `List<CascadeLayer>`. Each element in this list is self-contained and represents the complete, validated configuration for that specific layer.

#### **Step 6: Initial Display**

With all layers loaded and processed, the `MainViewModel` prepares the initial user view.

1. It defaults to selecting the **highest-priority layer** as the active editing layer.  
2. It defaults the UI to the **"Show Merged Result"** view.  
3. It invokes the `ICascadedDomDisplayMerger` service. This service:  
   * Takes the full `List<CascadeLayer>`.  
   * Takes the `SchemaDefaultsProvider` root node as the absolute base (Layer 0).  
   * Performs the **inter-layer merge** (from schema defaults up to the highest layer), creating the final `mergedDisplayRootNode` for display.  
   * Crucially, it also populates the final origin tracking maps used by the UI (`_displayValueOriginLayerIndex`, etc.).  
4. Finally, `MainViewModel.RefreshFlatList()` is called, which uses this `mergedDisplayRootNode` to build the `FlatItemsSource` that the DataGrid is bound to, showing the user the fully merged and operational configuration.

# Assembling context menu dynamically

Of course. Assembling the context menu dynamically is a crucial part of making the layer interactions intuitive. The process leverages the detailed origin-tracking maps that are generated when the merged view is created.

Here are the principles and the step-by-step process for how the layer information will be assembled for the row's context menu.

### **Core Principle: Dynamic Menu Construction**

The context menu's "Override Sources" submenu will not be a static list. Every time the user right-clicks a row, the list of menu items will be dynamically generated based on the specific history of the selected configuration value. This ensures the information is always accurate for the node in question.

### **Key Data Structures Needed**

This process relies on two key dictionaries being populated by the `ICascadedDomDisplayMerger` service and held by the `MainViewModel`:

1. **`_displayValueOriginLayerIndex` (Dictionary\<string, int\>):** For any given DOM path, this tells us the index of the single layer that provides its **final, effective value**.  
2. **`_displayValueOverrideSources` (Dictionary\<string, List\<int\>\>):** This is the most important map for this feature. For any given DOM path, it gives us a list of the indices of **every layer** that defines or overrides a value for that path.

### **The Assembly and Display Pipeline**

Here is the sequence of events from user action to menu display:

#### **Step 1: The Trigger (User Right-Clicks)**

* The user right-clicks a row in the DataGrid.  
* The `DataContext` for the context menu is the `DataGridRowItemViewModel` for that row.  
* The `ContextMenu` requests the items for its "Override Sources" submenu.

#### **Step 2: Getting the Context**

* The `DataGridRowItemViewModel` (let's call it `selectedVm`) needs to assemble the list of menu items. It will likely have a property, e.g., `public ObservableCollection<LayerMenuItemViewModel> OverrideSourceLayers { get; }`, which it will now populate.  
* It gets the unique path for the selected row, `targetPath = selectedVm.DomNode.Path`.  
* It accesses its `ParentViewModel` (`MainViewModel`) to get the master list of all `CascadeLayer` objects and the origin tracking dictionaries.

#### **Step 3: Assembling the Menu Item ViewModels**

The `selectedVm` executes a method like `BuildOverrideSourceMenuItems()`. This method performs the following logic:

1. **Iterate All Layers:** It iterates through the master `List<CascadeLayer>` from `MainViewModel`. This is important because the menu must always show **all possible layers**, not just the ones where the value is defined. The iteration happens in the correct cascade order (e.g., Base, SiteA, Development).  
2. **Look Up Origin Info:** It performs two lookups using the `targetPath`:  
   * It gets the list of all defining layers from `_displayValueOverrideSources`. Let's say for `/network/timeout`, this returns `[0, 2]` (meaning it's defined in Base and Development).  
   * It gets the final effective layer index from `_displayValueOriginLayerIndex`. For our example, this would be `2` (Development).  
3. **Create ViewModels:** For each `CascadeLayer` in the master list, it creates a new `LayerMenuItemViewModel` instance. This new ViewModel will have properties like:  
   * `LayerName` (e.g., "Base", "SiteA", "Development")  
   * `LayerIndex` (0, 1, 2\)  
   * `IsDefinedInThisLayer` (a boolean)  
   * `IsEffectiveInThisLayer` (a boolean)  
   * `SwitchToLayerCommand` (an `ICommand`)  
4. **Set Markers:** As it creates each `LayerMenuItemViewModel`, it sets the boolean markers based on the origin info retrieved in step 2\.  
   * For the "Base" layer (index 0): `IsDefinedInThisLayer` is set to `true` (since 0 is in our list `[0, 2]`). `IsEffectiveInThisLayer` is `false`.  
   * For the "SiteA" layer (index 1): `IsDefinedInThisLayer` is `false`. `IsEffectiveInThisLayer` is `false`.  
   * For the "Development" layer (index 2): `IsDefinedInThisLayer` is `true`. `IsEffectiveInThisLayer` is also `true` (since 2 is the effective index).  
5. **Wire Up Commands:** The `SwitchToLayerCommand` on each `LayerMenuItemViewModel` is wired to call a method on the `MainViewModel`, such as `SetSelectedEditorLayer(layerIndex)`.

#   Node Saving Heuristics

how to saving heuristics of "in what file to put what dom node" will work?

Excellent question. The saving process is the reverse of the loading pipeline and requires a robust set of rules—or heuristics—to deconstruct the changes made to a layer's single in-memory tree (`LayerConfigRootNode`) and persist them back to the correct physical files.

The primary goal is to make this process feel intuitive and predictable to the user, preserving their original file structure as much as possible while intelligently handling new additions.

Here is a breakdown of the principles and the step-by-step pipeline for the saving heuristics.

### **Core Principle: Distribute from the Top Down**

The fundamental principle is that we don't decide on a file for every individual leaf node. Instead, we look at the **top-level properties** in the unified `LayerConfigRootNode` and decide which file each of those "branches" belongs to. The entire subtree for that branch is then saved to the designated file.

For example, if the `LayerConfigRootNode` contains top-level properties `network`, `database`, and `features`, the heuristic will decide a target file for `network`, a target file for `database`, and so on.

### **Prerequisites for Saving**

Before the saving process begins, the `MainViewModel` must have the following for the layer being saved:

1. **The Modified `LayerConfigRootNode`:** The current, in-memory DOM tree for the layer.  
2. **The Original `IntraLayerValueOrigins` Map:** The `Dictionary<string, string>` created during loading. This map is critical as it tells us where existing data came from *before* any edits were made.  
3. **A List of Changed Paths:** The editor must know which nodes have been added, deleted, or had their values changed. This is typically managed via the Undo/Redo stack or a separate list of "dirty" paths.

---

### **The Saving Heuristics Pipeline**

When the user saves a dirty `CascadeLayer`, the following pipeline is executed.

#### **Phase 1: Generate the "Save Plan"**

The first phase is to determine which parts of the `LayerConfigRootNode` go into which files. We do this by creating a temporary data structure, let's call it a `savePlan`.

`savePlan`: A `Dictionary<string, List<DomNode>>` where:

* **Key:** The relative file path within the layer (e.g., `network/settings.json`).  
* **Value:** A list of the top-level `DomNode` branches that must be saved to that file.

**Algorithm to build the `savePlan`:**

1. Iterate through each **direct child** of the `LayerConfigRootNode` (e.g., the `network`, `database`, and `features` nodes).  
2. For each child node, apply the following heuristics in order to find its target file:  
   **Heuristic \#1: Find Existing Origin**  
   * Look up the node's path (e.g., `/network`) in the layer's original `IntraLayerValueOrigins` map.  
   * If an entry is found, it means this branch (or at least part of it) existed before. The file path from this map is its destination.  
   * Add the node to the list for that file path in the `savePlan`.  
3. **Heuristic \#2: Find Structural Template in Lower Layers (for New Nodes)**  
   * If the node's path is not in the origin map, it's a new branch created by the user.  
   * The system now searches for a "template" to determine the new file's path. It looks at the **lower-priority cascade layers**.  
   * **Example:** User adds a new `/security` branch in the "Override" layer. The system searches the "Base" layer. If it finds any property starting with `/security` (e.g., `/security/keys`) that originates from a file named `base/security.json`, it decides that's a good structural match.  
   * **Result:** It designates that the new `/security` branch should be saved to a **new file** in the *current layer* with the same relative path: `override/security.json`. This new file path and the `/security` node are added to the `savePlan`. The `IntraLayerValueOrigins` map is then updated in memory for this new node.  
4. **Heuristic \#3: Create New File from Path (Fallback for New Nodes)**  
   * If the new node has no origin (Heuristic \#1) and no structural template can be found in lower layers (Heuristic \#2), the editor must create a new file based on a fallback rule.  
   * **Rule:** The file name will be derived from the name of the new top-level property.  
   * **Example:** A new top-level branch `/analytics` is added. No origin or template exists. The system designates a new file `analytics.json` in the root of the layer's folder.  
   * This new file path and the `/analytics` node are added to the `savePlan`, and the `IntraLayerValueOrigins` map is updated.

#### **Phase 2: Reconstruct and Write Files**

Now that the `savePlan` is complete, the system writes the files to disk.

1. Iterate through each entry (each file) in the `savePlan`.  
2. For each `relativeFilePath`:  
   * Create a new, empty in-memory `ObjectNode`. This will represent the total content of the file to be written.  
   * Take the list of top-level branches assigned to this file from the `savePlan`.  
   * Deep-merge each of these branches into the new empty `ObjectNode`. This reconstructs the file's content, pulling together all the relevant data from the unified `LayerConfigRootNode`.  
   * Use the `IDomNodeToJsonSerializer` to serialize the reconstructed `ObjectNode` into a formatted JSON string.  
   * **Crucially, check if the file needs writing.** Compare the newly generated JSON string with the original text of the file (stored in the `SourceFileInfo` at load time).  
   * **Only write to disk if:**  
     * The file is new (it wasn't in the original `SourceFileInfo` list).  
     * The new JSON string is different from the original text.

This process ensures that only files with actual changes are modified on the disk, preventing unnecessary updates to file modification dates and source control churn.

### **Summary of Benefits**

This heuristic-based approach provides a powerful and intuitive saving experience:

* **Preserves Structure:** Existing file layouts are respected.  
* **Intelligent Creation:** New files are created based on established patterns from other layers, keeping the configuration organized.  
* **Deterministic:** The rules are clear and will produce the same result every time.  
* **Efficient:** By checking for actual content changes before writing, it minimizes disk I/O and plays nicely with source control systems.

# what happens internally if the user switches the cascading layer in the layer combo?

Excellent question. This action is central to the user's workflow, and its internal handling is critical for ensuring the editor's display is always consistent with the selected context.

The process of switching the active layer in the ComboBox triggers a clean, well-defined pipeline that regenerates the main view. The exact steps depend on whether the user is in "Merged Result" view or "Selected Layer Only" view.

Here is the breakdown of the internal process.

### **The Trigger: User Selects a New Layer**

1. The user clicks the layer selection `ComboBox` and chooses a new layer (e.g., switches from "Development" to "SiteA").  
2. Through WPF data binding, the `SelectedItem` of the ComboBox updates the `SelectedEditorLayer` property on the `MainViewModel`.  
3. The `setter` for the `SelectedEditorLayer` property initiates the refresh pipeline.

### **The Internal Pipeline**

The `MainViewModel` immediately checks the state of the "Show Merged Result" checkbox to decide which of two distinct paths to take.

---

#### **Scenario A: "Show Selected Layer Only" View is Active**

This is the simpler case. The editor only needs to display the contents of the single, newly selected layer.

1. **Identify New Display Root:** The `MainViewModel` determines that the new root `DomNode` for the display is the `LayerConfigRootNode` that is already stored within the newly selected `CascadeLayer` object. No merging is required.  
2. **Refresh the UI:** The `MainViewModel` calls its internal `RefreshFlatList()` method.  
3. **Rebuild `FlatItemsSource`:** `RefreshFlatList()` clears the existing `FlatItemsSource`. It then performs a recursive walk of the newly selected layer's `LayerConfigRootNode`, creating a `DataGridRowItemViewModel` for each node and adding it to the `FlatItemsSource`.  
4. **Update Mappings:** As the list is rebuilt, the `_domToSchemaMap` and validation status are updated for the nodes in this specific layer's tree. Since this is a single-layer view, the "Origin Layer" for every row will simply be the selected layer itself.  
5. **UI Updates:** The DataGrid automatically updates to show the new content. The "Origin Layer" column will now show "SiteA" for all rows, and the values displayed will be only those defined within the files in the `config/siteA` folder.

---

#### **Scenario B: "Show Merged Result" View is Active**

This is the more complex and powerful scenario. The scope of the inter-layer merge has changed, so the merged data must be recalculated.

1. **Identify New Merge Scope:** The `MainViewModel` knows that the merge must now be performed on all layers from index 0 up to and including the index of the newly selected layer (e.g., "SiteA").  
2. **Invoke the Merger Service:** The `MainViewModel` calls the `ICascadedDomDisplayMerger` service.  
3. **Perform Inter-Layer Merge:** The merger service takes the list of relevant layers (e.g., "Schema Defaults", "Base", "SiteA"). It performs the deep merge according to the established rules (objects merge, arrays replace).  
4. **Generate New Merged Tree:** The service outputs a **brand new, temporary `mergedDisplayRootNode`**. This tree represents the state of the configuration as if "SiteA" were the final layer.  
5. **Regenerate Origin Maps:** As part of the merge, the service also generates **new origin-tracking maps** (`_displayValueOriginLayerIndex` and `_displayValueOverrideSources`). These maps now reflect the new merge scope. For instance, a value that was previously overridden by the "Development" layer might now be visible from the "Base" layer, and the maps will reflect this.  
6. **Refresh the UI:** The `MainViewModel` receives the new `mergedDisplayRootNode` and the new origin maps. It then calls `RefreshFlatList()`.  
7. **Rebuild `FlatItemsSource`:** `RefreshFlatList()` clears the old UI items and walks the new `mergedDisplayRootNode`. As it creates each `DataGridRowItemViewModel`, it consults the **new origin maps** to correctly populate the origin information for each row.  
8. **UI Updates:** The DataGrid updates. The user sees a different set of effective values. The "Origin Layer" column is completely re-evaluated; some rows might now show "Base" where they previously showed "Development", and others might now show "SiteA" if that layer provided an override.

### **Summary of Internal Actions**

In both scenarios, the core action is the same:

**Switching the layer determines a new "display root `DomNode`" and triggers a full refresh of the `FlatItemsSource` based on that new root.**

The key difference is how that new display root is obtained:

* In **Layer Only View**, it's a simple lookup of a pre-existing `DomNode` from the selected `CascadeLayer` object.  
* In **Merged Result View**, it's a recalculation involving a multi-layer merge and a regeneration of all origin-tracking data.

# what happens internally if the user opens row context menu and selects "Reset to undefined"?

Zobrazit uvažování

Of course. The "Reset to undefined" command is a powerful editing feature that surgically removes a configuration value from a *specific layer*, allowing values from lower-priority layers to "shine through."

Here is what happens internally when the user selects this option from the row context menu.

### **Core Principle: Targeted Deletion from the Active Layer**

The fundamental principle is that this action **does not delete the property from the merged view directly**. Instead, it finds and removes the corresponding node **only from the currently selected editor layer's in-memory tree** (`LayerConfigRootNode`). The change in the user-facing merged view is a *result* of this targeted deletion.

### **Prerequisites: When is the Command Available?**

Before the pipeline begins, the `CanExecute` logic for the command must be evaluated. The "Reset to undefined" menu item will only be **enabled** if the following conditions are met:

* A row is selected.  
* The selected row corresponds to a `DomNode` that is **actually present in the currently selected editor layer**.

This prevents the user from trying to "reset" a value that is purely inherited. You can't remove an override from a layer that doesn't have an override in the first place.

---

### **The Internal Pipeline**

Assuming the command is enabled and the user clicks it, the following sequence of events is triggered:

#### **Step 1: Get Context and Validate**

1. The command is initiated, with the `DataContext` being the `DataGridRowItemViewModel` of the selected row.  
2. The `MainViewModel` gets the `DomNode` associated with the ViewModel and its `Path`.  
3. It gets the currently `SelectedEditorLayer`.  
4. It performs a final check to confirm that a node with this path exists within the `SelectedEditorLayer.LayerConfigRootNode`.

#### **Step 2: Record Operation for Undo/Redo**

This is a destructive action, so preparing for an "undo" is the first critical step.

1. Before modifying anything, the system creates a `RemoveNodeOperation` object (as we've previously designed).  
2. This object captures the necessary context to reverse the action:  
   * The `DomNode` to be removed (a deep clone of it).  
   * Its parent `DomNode` within the `SelectedEditorLayer`.  
   * Its name (the property key) or its index (if it was in an array).  
3. This `RemoveNodeOperation` is pushed onto the `_undoStack`. The `_redoStack` is cleared.  
4. The `IsDirty` flag for the layer is set to `true`, and the window title is updated.

#### **Step 3: Modify the Layer's Data Model**

This is the core action where the actual data change occurs.

1. The `MainViewModel` navigates to the target node within the `SelectedEditorLayer.LayerConfigRootNode` using its path.  
2. It gets a reference to the node's parent.  
3. It calls the appropriate removal method on the parent:  
   * If the parent is an `ObjectNode`, it calls `parent.RemoveChild(node.Name)`.  
   * If the parent is an `ArrayNode`, it calls `parent.RemoveItem(node)`.  
4. At this point, the property has been successfully removed from the in-memory tree of the selected layer.

#### **Step 4: Refresh the User Interface**

The data model for the selected layer has changed, which means the final merged view is now out of date. The UI must be refreshed to show the new reality.

1. The `MainViewModel` recognizes that the display is now stale.  
2. It re-invokes the `ICascadedDomDisplayMerger` service, providing it with the full list of `CascadeLayer`s (including the one just modified).  
3. The service performs the **inter-layer merge** again, creating a new `mergedDisplayRootNode` and new origin-tracking maps.  
4. The `MainViewModel` then calls `RefreshFlatList()`, which completely rebuilds the `FlatItemsSource` for the DataGrid based on this new merged tree.

### **The User Experience: What Happens on Screen**

The user's view of the DataGrid changes in one of two ways, depending on the configuration's structure:

* **Scenario 1: A Lower-Layer Value Exists**  
  * **Before:** The user sees a `timeout: 60` row with "Origin Layer" showing "Development".  
  * **Action:** They select "Reset to undefined" while "Development" is the active layer.  
  * **After:** The row for `timeout` **remains**, but its content updates. It now displays `timeout: 30` and the "Origin Layer" column changes to "Base", as the value from the lower layer is now showing through.  
* **Scenario 2: The Value Was Unique to the Layer**  
  * **Before:** A property `newFeatureFlag: true` exists only in the "Development" layer. The "Origin Layer" shows "Development".  
  * **Action:** The user resets this property.  
  * **After:** Because no lower layer defines `newFeatureFlag`, it no longer exists in the newly generated `mergedDisplayRootNode`. Consequently, the entire row for `newFeatureFlag` **disappears** from the DataGrid.

This entire process makes "Reset to undefined" a predictable and powerful tool for managing layer overrides.

# Schema validation strategy

how the schema validation should work in most optimal way from the user perspective, for the non-top level layers where the data can be "sparsely" defined by designs, because not all layers define all values, only all merged together make sense to be validated against a "missing items" rule?

Excellent question. This gets to the heart of creating a user experience that is helpful rather than frustrating. A naive validation approach would indeed produce a lot of noise. The optimal strategy is a context-aware one that validates what the user cares about, depending on what they are currently viewing.

Here is a breakdown of the proposed schema validation strategy, designed from the user's perspective.

### **Core Principle: Validate What the User Sees and Intends**

The validation logic should adapt based on the editor's two primary view modes: "Show Merged Result" and "Show Selected Layer Only". The user's intent is different in each mode, and the validation feedback must reflect that.

---

### **Validation in "Show Merged Result" View**

This is the most important validation mode, as it represents a "health check" of the final, effective configuration.

* **What is Validated:** The validation service runs against the complete, temporary `mergedDisplayRootNode`.  
* **How Rules are Applied:**  
  * **"Missing Required Field" Rule: FULLY ENFORCED.** In this view, if the schema marks a field as required and it doesn't exist in the final merged tree, it is a critical error. This is a genuine problem because the final configuration is incomplete. This is exactly what the user wants to know.  
  * **Type, Range, Regex, and Allowed Values Rules: FULLY ENFORCED.** All other validation rules are applied to the final effective values. If the "Base" layer defines `timeout: "thirty"` (a string) and the schema requires a number, this error will be flagged on the merged `timeout` node.  
* **The User Experience:**  
  * The user gets a true and accurate picture of the health of the configuration that would be consumed by an application.  
  * **Crucially, when an error is found, the "Origin Layer" column immediately tells the user which layer is responsible for the incorrect data.** If they see an invalid `timeout` value, the origin column will point them directly to the "Base" layer, so they know exactly where to go to fix it.  
  * Errors are displayed in the "Issues" panel and visually on the row in the DataGrid.

---

### **Validation in "Selected Layer Only" View**

This mode is for focused editing of a single layer. The user's intent is to manage the overrides in this layer, not to see the state of the entire project.

* **What is Validated:** The validation service runs against the selected layer's specific `LayerConfigRootNode`.  
* **How Rules are Applied (The Key Difference):**  
  * **"Missing Required Field" Rule: DISABLED / DEMOTED.** This is the most important change for this view. The validation service will be instructed to **ignore "missing required field" errors**. A field being absent in an override layer is normal and expected. Flagging it as an error is the "noise" we want to eliminate. At most, it could be a low-priority informational message in the "Issues" panel, but it should not mark the node as invalid in the UI.  
  * **Type, Range, Regex, and Allowed Values Rules: FULLY ENFORCED.** These rules are still critical. If the user, in the "Override" layer, adds a `timeout` property with a value of `"abc"`, this is an error *within that layer*. It violates the schema's type constraint. This must be flagged immediately so the user can fix the data they are actively editing.  
* **The User Experience:**  
  * The user can focus on writing their overrides without being distracted by warnings about fields they have no intention of defining in that layer.  
  * They get immediate, relevant feedback on the quality of the data they are actually providing in the selected layer. It acts as a "linter" for just their changes.

### **The "Issues" Panel: A Unified View**

Both validation modes will report their findings to the "Issues" panel.

* When validating the **merged view**, the panel will show the list of all errors in the final configuration. Each issue will still be associated with its origin layer and file, allowing the user to double-click and navigate to the source of the problem.  
* When validating a **single layer**, the panel will only show errors specific to that layer's content (e.g., type mismatches).

This dual-mode strategy is optimal from a user's perspective because it makes the validation feedback **contextual and actionable**. It answers the right question at the right time:

* In Merged View: "Is my final configuration, as a whole, valid?"  
* In Layer-Only View: "Is the data I'm adding in *this specific layer* correctly formatted according to the schema?"

# Filtering

the filter should not apply just to currently visible nodes, but to ALL NODES in the tree, regardless of whether they are currently visible (expanded or collapsed). the nodes that matches the filter should be shown, and their parent up to the root shall be shown as well and expanded.

same for search, it should operate over all nodes in the tree, and for each "show next" it has to automatically expand the node found as well as all its parents.

"Search all layers" is an excellent idea.

How the search will work in merged view mode? Shouldn't it search all the layers included in the merged view? If we are switched to the top level layer in merged view model, wouldn't the search feature work very similarly to the "search all layers", iterating over each matching row (regardless of what layer it is defined on)? In this case it should probably match just the highest level overrides, so it never finds potrential matches on the lower levels (which would be found by "Search all layers"). Correct?

Zobrazit uvažování

Of course. Your feedback is excellent and leads to a much more powerful and intuitive design for both filtering and searching. A user's natural expectation is that these tools will find what they're looking for, regardless of the collapsed state of the UI.

Let's refine the behavior of these features based on your input.

### **Redefined Filter Behavior: From "Filter" to "Reveal"**

Your new requirement effectively changes the "Filter" from a simple view-reducer to a powerful **"Search and Reveal"** tool.

* **New Principle:** The filter will search **all nodes** in the currently displayed tree (merged or single-layer). Any node that matches the filter, along with all of its parent nodes up to the root, will be made visible. All other nodes will be hidden.  
* **Internal Behavior:**  
  1. **Full-Tree Search:** When the user types in the `FilterText` box, the `MainViewModel` will no longer just filter the existing `FlatItemsSource`. Instead, it will perform a traversal of the entire in-memory "display root" `DomNode`.  
  2. **Build Match Sets:**  
     * It first creates a set of all `DomNode`s that directly match the filter text (either by name or value).  
     * It then creates a second set, `nodesToShow`, which will contain the final list of all nodes that should be visible.  
  3. **Populate Ancestors:** The application iterates through the set of directly matching nodes. For each match, it walks *up* the parent chain to the root, adding the node itself and every one of its ancestors to the `nodesToShow` set.  
  4. **Rebuild UI:** `RefreshFlatList()` is called. It now has a new rule: as it recursively builds the `FlatItemsSource`, it only adds a `DataGridRowItemViewModel` if its corresponding `DomNode` exists in the `nodesToShow` set.  
  5. **Auto-Expansion:** When creating the `DataGridRowItemViewModel` for any node that is a parent of a matched result, its `IsExpanded` property is forcibly set to `true`.  
* **User Experience:** The user types "timeout". The tree instantly collapses to show only the root, any intermediate parent objects (like `network`), and the final `timeout` node itself, regardless of how deeply it was nested or whether its parents were previously collapsed. This is a much more effective way to quickly isolate specific settings.

---

### **Redefined Search Behavior: With Auto-Expand**

Similarly, the "Find Next/Previous" search functionality will now automatically reveal its findings.

* **New Principle:** When navigating to a search result, the editor will guarantee that the node is visible by expanding its entire parent path.  
* **Internal Behavior (`Find Next` Workflow):**  
  1. **Full-Tree Search:** The initial search still runs on the entire display tree to get a complete list of all matching `DomNode`s.  
  2. **Navigation:** The user presses `F3`. The `MainViewModel` identifies the next `DomNode` in the search results list.  
  3. **Auto-Expand Path:** Before selecting the item, the `MainViewModel` calls a new helper method, `EnsureNodeIsVisible(targetNode)`. This method walks up the `targetNode.Parent` chain to the root. For each ancestor, it finds its corresponding `DataGridRowItemViewModel` in the UI and sets its `IsExpanded` property to `true`.  
  4. **Select and Focus:** Once the path is fully expanded, the `MainViewModel` sets its `SelectedGridItem` to the ViewModel of the found node. The DataGrid scrolls it into view, and because its parents are now expanded, it is guaranteed to be visible.  
* **User Experience:** The user searches for a term, hits F3, and the tree automatically expands and scrolls to exactly where the result is, removing any need for manual clicking and expanding.

---

### **Clarified Search: "Standard Search" vs. "Search All Layers"**

You are absolutely correct. Your proposed distinction is the perfect way to define the behavior and provide two complementary tools that answer different questions.

#### **Standard Search (in Merged View)**

This search answers the question: **"What is the final, effective value for my search term?"**

* **Scope:** It operates **only on the `mergedDisplayRootNode`**.  
* **Behavior:** It finds a property **only once**. It sees the world exactly as the consuming application would. If a value is overridden, the search will only find the final override; it will be completely unaware of any values from lower layers that were masked.  
* **Example:**  
  * `Base` layer has `{ "timeout": 30 }`.  
  * `Dev` layer has `{ "timeout": 60 }`.  
  * A standard search for "timeout" in the merged view (with "Dev" as the top layer) will find **one result**: the node with value `60` and origin "Dev". It will *not* find the `30` from the Base layer.  
* **Use Case:** This is the default and most common search. It's for users who want to find and edit the current, active configuration values.

#### **"Search All Layers" (Project-Wide Search)**

This search answers the question: **"Where in my entire project is this term defined, including all overrides and masked values?"**

* **Scope:** It does **not** use the merged tree. Instead, it iterates through the `MainViewModel.ListOfCascadeLayers` and runs the search algorithm on each layer's individual `LayerConfigRootNode` independently.  
* **Behavior:** It finds **every occurrence** of the term across all files in all layers.  
* **Example:**  
  * Using the same `timeout` example, a "Search All Layers" would find **two results**:  
    1. A match in the **Base** layer with value `30`.  
    2. A match in the **Dev** layer with value `60`.  
* **Result Display:** These results would be populated in the "Issues" panel (or a new, dedicated "Search Results" panel). Each result would clearly list the **Layer**, **File**, **Path**, and **Value**, and be a clickable link that navigates the user to that precise location.  
* **Use Case:** This is a powerful auditing and debugging tool for administrators or developers trying to understand why a value is what it is, trace its history through the cascade, or find and eliminate redundant definitions.

# Recap

The core philosophy is a **Unified Data Model**. The editor has only one operational mode: **Cascade Mode**. A traditional single JSON file is treated internally as a simple cascade with two layers:

* **Layer 0:** Schema Defaults (read-only)  
* **Layer 1:** The content of the single file.

This approach ensures all features are available in all contexts, while the UI adapts to hide complexity when it is not needed.

---

## **2\. Core Concepts**

### **2.1. The Cascade Project**

A "Cascade Project" is defined by a single .cascade.jsonc file. This file contains an ordered list of layer definitions. The order is critical, defining the override precedence from lowest (base) to highest (most specific).

### **2.2. The Cascade Layer**

A "Cascade Layer" is a logical grouping of settings corresponding to a folder containing one or more .json files.

* **Intra-Layer Merge:** On load, all files within a single layer's folder are merged into one unified configuration tree for that layer.  
* **Constraint:** Files within a single layer **must not** have overlapping property definitions at the same path. Violations will be reported as errors in the "Issues Panel".

### **2.3. The Schema Defaults Layer (Layer 0\)**

The absolute base of every cascade is "Layer 0." This is a conceptual, read-only layer generated from the DefaultValue properties defined in the loaded schema. Its values are only visible if they are not overridden by any subsequent file-based layer.

### **2.4. Inter-Layer Merging**

When displaying a "Merged Result," the editor takes the configuration trees from multiple layers (starting with Layer 0\) and merges them in order of precedence.

* **Object Merging:** Objects are deep-merged. Properties from higher-priority layers are added to or replace properties in lower-priority objects.  
* **Array & Value Merging:** An array or a primitive value from a higher-priority layer completely **replaces** any array or value at the same path from a lower-priority layer.

---

## **3\. User Interface and Interaction**

The UI adapts based on the number of file-based layers loaded.

### **3.1. Main UI Controls**

* **Layer Selector (ComboBox):** *Visible only when \> 1 file-based layer is loaded.* Allows the user to select the **active editing layer**. All edits are applied to this layer.  
* **"Show Merged Result" (CheckBox):** Toggles the main grid's display between:  
  * **Selected Layer Only:** A focused view showing only the configuration values defined in the files of the currently active layer.  
  * **Merged Result:** The primary view showing the final, effective configuration.  
* **Origin Layer Column:** *Visible only in "Merged Result" view.* Displays the name of the layer that provided the final, winning value for each property.

### **3.2. Issues & Search Results Panel**

A new panel at the bottom of the editor displays:

* **Issues:** Loading errors, schema violations, and on-demand integrity warnings.  
* **"Search All Layers" Results:** A comprehensive list of all matches from a project-wide search.  
* **Navigation:** All items in this panel are hyperlinks that, when clicked, navigate the user to the exact source of the issue or search result.

---

## **4\. Key Feature Behavior**

### **4.1. Editing and Saving**

* **Editing Target:** All edits are applied to the currently **selected editor layer** (or Layer 1 in single-file mode). When a user modifies an inherited value in the merged view, an override is automatically created in the active layer.  
* **Assigning New Nodes:** When a user creates a new **top-level configuration section** (e.g., /security) in a layer where it doesn't exist, they are prompted **once** to assign it to a source file. The editor suggests a file path based on heuristics. All subsequent properties added under that same section (e.g., /security/token) are **automatically assigned to the same file** without further prompts.  
* **Saving:** The editor saves only the files that have changed within the layers marked as dirty. It uses the in-memory origin map to reconstruct the file content.

### **4.2. Copy/Paste Functionality**

* **Copy (Ctrl+C):** A **deep clone** of the selected node is stored in an internal clipboard.  
* **Paste (Ctrl+V):** The paste action is context-aware and always applies the change to the **currently active editor layer**.  
  * **On an Array Node:** The clipboard node is added as a new element.  
  * **On an Object Node:** A dialog prompts for the property name. If a name conflict occurs, a second dialog asks for confirmation to **Replace** the existing property.  
  * All paste operations are fully undoable.

### **4.3. Context Menu Actions**

* **Override Sources:** This submenu dynamically lists **all layers**, using markers (✔) to indicate which layers define a value and which provides the final, **effective** value (**Bold**). Clicking any layer name switches the active editor layer.  
* **Reset to undefined:** This command removes the selected property **from the active editor layer only**, allowing values from lower-priority layers to become visible.  
* **Move Node to Different File...:** This action is **layer-specific**. It allows the user to change the source file for a configuration section only within the currently active layer.

### **4.4. Search and Filter**

* **Filter:** The "Filter by name" tool is a **"Search and Reveal"** feature. It searches all nodes in the current view and automatically expands the tree to reveal any matches and their parents.  
* **Standard Search (Ctrl+F):** Operates on the **currently displayed data** (merged or single-layer). It only finds the final, effective values.  
* **"Search All Layers":** A separate auditing tool that searches every file in every layer independently and lists all matches, including masked values, in the results panel.

### **4.5. Validation & Schema**

* **Context-Aware Validation:**  
  * **In "Merged Result" View:** All schema rules, including "missing required field," are fully enforced.  
  * **In "Selected Layer Only" View:** The "missing required field" rule is ignored to avoid noise from sparse override files.  
* **Schema-Driven Editing:** The UI displays grayed-out "placeholder" rows for properties and array items defined in the schema but not yet present in the data, allowing the user to add them with a double-click or by pressing Enter.

---

## **5\. Project Auditing: The Integrity Check**

An on-demand tool (Tools \> Run Integrity Check...) allows users to audit their entire project for consistency. A dialog lets the user select which checks to run, with results displayed in the Issues Panel.

* **Checks Include:** File Path Consistency, Overlapping Definitions, Schema Compliance for All Layers, Property Name Casing Mismatches, and Empty Files/Folders.

TOP PRIORITY:  
**Preserve all existing editing capabilities and UI feel as the highest priority.** This includes all keyboard/mouse bindings, template selectors, styles, and event handlers.

# Node path as the key to dictionaries in MainViewModel

Many main viewmodel internal dictionaries use DomNode as key. As the DomNode is mostly a temporary stuff in the new cascading architecture, we should rather use the node path as the key.
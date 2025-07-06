BUG: Adding array node of type object (defined by schema) - now a generic value is created, json type probably deduced just from the string input.
Empty object should be created.

Folding/unfolding takes long time on little json files.

Insert key for adding new stuff into array. Insert item in context menu. Inserts ABOVE selected array item.

TestData\config\1_base\sub1\base.json gets loaded correctly into DOM. If the properties from base.json get changed,
on save they are saved into TestData\config\1_base\sub1.json
with no warning or user confirmation and the original file is left there.
On open the editor does not offer any consolidation to absorb the 1_base\sub1\base.json into 1_base\sub1.json.
I think the editor should NOT by default automatically offer the consolidation on load, rather to make it part of the Tools menu or pre-save checks.
Maybe we could put the choice of what checks will happen automatically on load to editor's configuration which should be loaded from a json file in a per-user storage location.


I think the editor should remember and keep the origin of existing nodes and save them to their original files.
I think the editor should check on startup if there is any real overlap (in same layer, some leaf-node or array-container node defined more than once)
and treat these as errors. Having overlapping definitions of object node within same layer should generate a warning but it is a valid case
when the power user does not want to have a single file for top level property but instead multiple files on deeper levels of the dom tree.

On save, the editor does not clenup the abandoned TestData\config\1_base\sub1\base.json that
was absorbed into TestData\config\1_base\sub1.json. This pre-save check is probably also missing.


BUG: Editor allows editing the Value field of a object-typed schema node

File name/folder case of json files vs. schema defined object/class field names - editor differentitates! Needs case independency...
Or at least the editor should check really hard the letter case compatibility between schema, all existing files/folder and all content in the json files.


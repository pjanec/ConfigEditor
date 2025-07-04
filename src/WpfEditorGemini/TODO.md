BUG: Adding array node of type object (defined by schema) - now a generic value is created, json type probably deduced just from the string input.
Empty object should be created.

Folding/unfolding takes long time on little json files.

BUG: Cascaded loading does not take the json file name as a level in hierarchy.

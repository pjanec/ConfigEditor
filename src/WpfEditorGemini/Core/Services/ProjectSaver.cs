using JsonConfigEditor.Core.Cascade;
using JsonConfigEditor.Core.Dom;
using JsonConfigEditor.Core.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// Handles the logic for saving a modified CascadeLayer back to its constituent physical files.
    /// </summary>
    /*
        # The Rules of Robust Saving

        ### **Rule 1: The Container Principle**

        The system uses a file's path to create a corresponding "container" object in the DOM.

        * A file at `config/server.json` has its content placed inside the `$root/config/server` DOM object.  
        * A file at `credentials.json` has its content placed inside the `$root/credentials` DOM object.

        **The unbreakable saving rule is:** To save a file, the system must find its corresponding container node in the DOM and serialize only the **children** of that container. It must never serialize the container itself. This single rule prevents the incorrect nesting you've observed.




        ### **Rule 2: The Property Ownership Principle**

        A container in the DOM might hold properties that came from different, more deeply nested files.

        * The `$root/database` container can hold a property `timeout` from `database.json` and an object `auditing` whose contents are from `database/auditing.json`.

        **The unbreakable saving rule is:** When saving a container's content to a file (e.g., to `database.json`), the system must only include children that **truly belong to that file**. It determines this by checking the origin of the child's data. It must not "steal" a child (like `auditing`) that belongs to another file (`database/auditing.json`). This prevents incorrect file consolidation.


        ### **Rule 3: The Exclusive Content Principle**

        Every piece of data (every leaf node in the DOM) has exactly one source file, which is tracked in the

        `IntraLayerValueOrigins` map .

        **The unbreakable saving rule is:** A piece of data can only be written to the single file it originated from. This is the natural outcome of correctly implementing Rule 2 and prevents data from being duplicated across multiple saved files.

        **Rule 4: New property**

        When a new property is created, only that property and its own self-contained children (if any) are assigned an origin. The parent containers it's placed in do not inherit the origin.

        **When a new property is created, the system must try to deduce its destination file. It does this by checking for a file origin in this order:**

        1. **The Parent:** Look at the immediate parent of the new property. Does it have a file origin? If yes, use that file.  
        2. **The Children/Descendants:** If the parent has no origin, look at the parent's *other* children (the new property's siblings). Do any of them, or their descendants, have a file origin? If yes, use that file.  
        3. **Prompt as a Last Resort:** If and only if both checks fail to find an existing file for that part of the DOM tree, prompt the user to choose a destination file.
    */

    public class ProjectSaver
    {
        private readonly IDomNodeToJsonSerializer _serializer;

        public ProjectSaver(IDomNodeToJsonSerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>
        /// Saves all modified files within a given cascade layer.
        /// </summary>
        public async Task SaveLayerAsync(CascadeLayer layer)
        {
            if (!layer.IsDirty)
            {
                return;
            }

            var savePlan = BuildSavePlan(layer);

            foreach (var (relativeFilePath, nodeToSave) in savePlan)
            {
                var originalFile = layer.SourceFiles.FirstOrDefault(f => f.RelativePath.Equals(relativeFilePath, StringComparison.OrdinalIgnoreCase));
                
                // The serializer correctly handles writing the contents of the object node.
                string newContent = _serializer.SerializeToString(nodeToSave, indented: true);

                if (originalFile == null || originalFile.OriginalText != newContent)
                {
                    var absoluteFilePath = Path.Combine(layer.FolderPath, relativeFilePath);
                    var directory = Path.GetDirectoryName(absoluteFilePath);
                    if (directory != null)
                    {
                        Directory.CreateDirectory(directory);
                    }
                    await File.WriteAllTextAsync(absoluteFilePath, newContent);
                }
            }

            foreach (var fileToDelete in layer.FilesToDeleteOnSave)
            {
                var absolutePath = Path.Combine(layer.FolderPath, fileToDelete);
                if (File.Exists(absolutePath))
                {
                    try
                    {
                        File.Delete(absolutePath);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Failed to delete consolidated file '{absolutePath}': {ex.Message}");
                    }
                }
            }
            layer.FilesToDeleteOnSave.Clear();
            layer.IsDirty = false;
        }

        /// <summary>
        /// Creates a plan that maps each file to an ObjectNode containing its correct content.
        /// This is done by finding the corresponding "mount node" for each file in the merged DOM
        /// and copying only the children that originated from that specific file.
        /// </summary>
        private Dictionary<string, ObjectNode> BuildSavePlan(CascadeLayer layer)
        {
            var plan = new Dictionary<string, ObjectNode>();

            // Get a list of all files that need to be considered for saving.  
            var filesToProcess = new HashSet<string>(layer.IntraLayerValueOrigins.Values);  
            foreach (var sourceFile in layer.SourceFiles)  
            {  
                filesToProcess.Add(sourceFile.RelativePath);  
            }

            foreach (var relativeFilePath in filesToProcess)  
            {  
                // Rule 1: Determine the file's container node in the DOM.  
                var pathWithoutExtension = relativeFilePath.Replace(".json", "", StringComparison.OrdinalIgnoreCase);  
                var segments = pathWithoutExtension.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);  
                var mountPath = segments.Any() ? "$root/" + string.Join("/", segments) : "$root";  
                  
                var mountNode = FindNodeByPath(layer.LayerConfigRootNode, mountPath) as ObjectNode;  
                var fileContentRoot = new ObjectNode("$file_root", null);

                if (mountNode != null)  
                {  
                    // Rule 2: Check each child of the container for ownership.  
                    foreach (var childNode in mountNode.Children.Values)  
                    {  
                        // Only include the child if its branch truly belongs to this file.  
                        if (DoesBranchBelongToFile(childNode, relativeFilePath, layer.IntraLayerValueOrigins))  
                        {  
                            fileContentRoot.AddChild(childNode.Name, DomCloning.CloneNode(childNode, fileContentRoot));  
                        }  
                    }  
                }  
                  
                plan[relativeFilePath] = fileContentRoot;  
            }  
            return plan;  
        }

        /// <summary>
        /// Checks if any leaf node within a given DOM branch originates from the target file.
        /// </summary>
        private bool DoesBranchBelongToFile(DomNode node, string targetFile, IReadOnlyDictionary<string, string> origins)
        {
            // Base Case: If this node is a leaf, check its origin directly.
            if (node is ValueNode || node is RefNode)
            {
                return origins.GetValueOrDefault(node.Path) == targetFile;
            }

            // Recursive Case: If any descendant belongs to the file, the whole branch is included.
            if (node is ObjectNode obj)
            {
                return obj.Children.Values.Any(c => DoesBranchBelongToFile(c, targetFile, origins));
            }
            if (node is ArrayNode arr)
            {
                return arr.Items.Any(i => DoesBranchBelongToFile(i, targetFile, origins));
            }
              
            return false;
        }




        /// <summary>
        /// Finds a node in a DOM tree by its full path.
        /// </summary>
        private DomNode? FindNodeByPath(DomNode rootNode, string path)
        {
            if (rootNode.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                return rootNode;
            }

            if (rootNode is ObjectNode objectNode)
            {
                foreach (var child in objectNode.GetChildren())
                {
                    if (path.StartsWith(child.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        var found = FindNodeByPath(child, path);
                        if (found != null) return found;
                    }
                }
            }
            else if (rootNode is ArrayNode arrayNode)
            {
                foreach (var item in arrayNode.GetItems())
                {
                    if (path.StartsWith(item.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        var found = FindNodeByPath(item, path);
                        if (found != null) return found;
                    }
                }
            }

            return null;
        }


    }
}

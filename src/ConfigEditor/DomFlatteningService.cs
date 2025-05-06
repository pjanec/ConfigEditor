using System;
using System.Collections.Generic;
using ConfigEditor.Dom;

namespace ConfigEditor.Util
{
    public class DomFlatteningService
    {
        public Dictionary<string, DomNode> Flatten(DomNode root)
        {
            var map = new Dictionary<string, DomNode>();
            FlattenRecursive(root, root.Path, map);
            return map;
        }

        private void FlattenRecursive(DomNode node, string path, Dictionary<string, DomNode> map)
        {
            map[path] = node;

            foreach (var (key, child) in node.GetChildren())
            {
                var childPath = string.IsNullOrEmpty(path) ? key! : $"{path}/{key}";
                FlattenRecursive(child, childPath, map);
            }
        }

        public DomNode Rebuild(Dictionary<string, DomNode> flatMap)
        {
            var root = new ObjectNode();

            foreach (var (path, node) in flatMap)
            {
                var parts = path.Split('/');
                DomNode current = root;

                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    bool isLast = i == parts.Length - 1;

                    if (int.TryParse(part, out int index))
                    {
                        var arr = current as ArrayNode ?? throw new InvalidOperationException("Path segment expects array");
                        if (isLast)
                        {
                            arr.Insert(index, node);
                        }
                        else
                        {
                            while (arr.Items.Count <= index)
                                arr.Add(new ObjectNode());
                            current = arr.Items[index];
                        }
                    }
                    else
                    {
                        var obj = current as ObjectNode ?? throw new InvalidOperationException("Path segment expects object");
                        if (isLast)
                        {
                            obj.Add(part, node);
                        }
                        else
                        {
                            if (!obj.ContainsKey(part))
                            {
                                var newChild = new ObjectNode();
                                obj.Add(part, newChild);
                                current = newChild;
                            }
                            else
                            {
                                current = obj.GetChild(part)!;
                            }
                        }
                    }
                }
            }

            return root;
        }
    }
}

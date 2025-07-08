using RuntimeConfig.Core.Dom;
using RuntimeConfig.Core.Schema;
using JsonConfigEditor.Core.SchemaLoading;
using JsonConfigEditor.ViewModels;
using System;
using System.Text.Json;

namespace JsonConfigEditor.Core.Services
{
    /// <summary>
    /// A dedicated service responsible for all low-level creation of DomNode instances.
    /// This includes materializing nodes from schema definitions and parsing raw user input
    /// into new nodes. It isolates this complex logic from the MainViewModel.
    /// </summary>
    public class DomNodeFactory
    {
        private readonly ISchemaLoaderService _schemaLoader;
        private readonly MainViewModel _mainViewModel;

        public DomNodeFactory(ISchemaLoaderService schemaLoader, MainViewModel mainViewModel)
        {
            _schemaLoader = schemaLoader ?? throw new ArgumentNullException(nameof(schemaLoader));
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
        }

        /// <summary>
        /// Creates a DomNode from a schema definition. This is used to create the
        /// initial, default state of a node when a schema-only placeholder is materialized.
        /// </summary>
        /// <param name="schema">The schema defining the node to create.</param>
        /// <param name="name">The name of the node (property name or array index).</param>
        /// <param name="parent">The parent DomNode this new node will be attached to.</param>
        /// <returns>A new DomNode instance (e.g., ObjectNode, ArrayNode, or ValueNode with a default value).</returns>
        public DomNode? CreateFromSchema(SchemaNode schema, string name, DomNode? parent)
        {
            if (schema == null) return null;

            // Convert the schema's default value to a JsonElement for the ValueNode constructor.
            JsonElement defaultValue = ConvertObjectToJsonElement(schema.DefaultValue, schema.ClrType);

            switch (schema.NodeType)
            {
                case SchemaNodeType.Object:
                    return new ObjectNode(name, parent);
                case SchemaNodeType.Array:
                    return new ArrayNode(name, parent);
                case SchemaNodeType.Value:
                    return new ValueNode(name, parent, defaultValue);
                default:
                    return null; // Should not happen
            }
        }

        /// <summary>
        /// Creates a DomNode from a raw string value provided by the user. This method
        /// intelligently attempts to parse the string as a number or boolean before
        /// defaulting to a JSON string.
        /// </summary>
        /// <param name="value">The raw string input from the user.</param>
        /// <param name="name">The name for the new node.</param>
        /// <param name="parent">The parent DomNode.</param>
        /// <param name="schema">The schema context, used to aid in type inference.</param>
        /// <returns>A new DomNode, typically a ValueNode.</returns>
        public DomNode CreateFromValue(string value, string name, DomNode parent, SchemaNode? schema)
        {
            try
            {
                // Attempt to parse the value as a literal JSON value (e.g., number, true, false, null)
                var jsonDoc = JsonDocument.Parse(value);
                return new ValueNode(name, parent, jsonDoc.RootElement.Clone());
            }
            catch (JsonException)
            {
                // If parsing fails, treat it as a string literal.
                var stringJson = $"\"{JsonEncodedText.Encode(value)}\"";
                var jsonDoc = JsonDocument.Parse(stringJson);
                return new ValueNode(name, parent, jsonDoc.RootElement.Clone());
            }
        }

        /// <summary>
        /// Ensures a full path of DomNodes exists, materializing any missing parent nodes
        /// from their schema definitions along the way. This is the core logic for
        /// turning a schema-only placeholder into a real node in the DOM.
        /// </summary>
        /// <param name="targetPathKey">The full path of the node to materialize (e.g., "database/port").</param>
        /// <param name="targetSchemaNodeContext">The schema for the target node.</param>
        /// <returns>The DomNode at the target path, now guaranteed to exist.</returns>
        public DomNode? MaterializeDomPathRecursive(string targetPathKey, SchemaNode? targetSchemaNodeContext)
        {
            // This logic is moved directly from MainViewModel.
            // It recursively ensures that each segment of a path exists, creating parent
            // ObjectNodes from the schema as needed, until the final target node is reached and returned.
            // Note: This method will need access to the MainViewModel to record undo/redo operations
            // for the creation of parent nodes.
              
            // The root for materialization MUST be the active editing layer's root, not the merged display root.
            var rootNode = _mainViewModel.ActiveEditorLayer?.LayerConfigRootNode;

            // If there's no active layer, we can't materialize anything.
            if (rootNode == null) return null;


            if (string.IsNullOrEmpty(targetPathKey) || targetPathKey == "$root")
            {
                if (rootNode != null) return rootNode;
                
                // Handle root creation if it doesn't exist (logic moved from MainViewModel)
                if (targetSchemaNodeContext == null)
                {
                    return null;
                }

                var newRoot = CreateFromSchema(targetSchemaNodeContext, "$root", null);
                if (newRoot != null)
                {
                    // The MainViewModel will handle the root replacement operation
                    _mainViewModel.ReplaceRootWithHistory(newRoot);
                    return _mainViewModel.GetRootDomNode();
                }
                return null;
            }

            // Find existing node first
            DomNode? existingNode = _mainViewModel.FindDomNodeByPath(targetPathKey);
            if (existingNode != null)
            {
                return existingNode;
            }

            // If not found, materialize the path
            string parentPathKey;
            string currentNodeName;

            string normalizedTargetPathKey = targetPathKey.StartsWith("$root/") ? targetPathKey.Substring("$root/".Length) : targetPathKey;

            int lastSlash = normalizedTargetPathKey.LastIndexOf('/');
            if (lastSlash == -1)
            {
                parentPathKey = "$root";
                currentNodeName = normalizedTargetPathKey;
            }
            else
            {
                parentPathKey = "$root/" + normalizedTargetPathKey.Substring(0, lastSlash);
                currentNodeName = normalizedTargetPathKey.Substring(lastSlash + 1);
            }

            SchemaNode? parentSchema = _schemaLoader.FindSchemaForPath(parentPathKey);
            DomNode? parentDomNode = MaterializeDomPathRecursive(parentPathKey, parentSchema);

            if (parentDomNode == null || !(parentDomNode is ObjectNode parentAsObject))
            {
                return null; // Cannot add a child to a non-object or null parent
            }

            SchemaNode? currentNodeSchema = targetSchemaNodeContext ?? _schemaLoader.FindSchemaForPath(targetPathKey);
            if (currentNodeSchema == null)
            {
                return null; // Cannot create a node without a schema definition
            }

            var newNode = CreateFromSchema(currentNodeSchema, currentNodeName, parentDomNode);
            if (newNode == null)
            {
                return null;
            }

            // IMPORTANT: The factory creates the node, but the MainViewModel performs the state change.
            // The MainViewModel will call this method, and then it will be responsible for
            // adding the returned node to the parent and recording the undo operation.
            _mainViewModel.AddNodeWithHistory(parentAsObject, newNode, currentNodeName);

            return newNode;
        }

        private JsonElement ConvertObjectToJsonElement(object? value, Type? clrType)
        {
            // This helper method is moved directly from MainViewModel
            if (value == null)
            {
                return JsonDocument.Parse("null").RootElement;
            }
            if (value is JsonElement element)
            {
                return element.Clone();
            }

            // If we have a target type, try to convert the value to that type first
            if (clrType != null)
            {
                try
                {
                    // Handle common type conversions
                    if (clrType == typeof(bool))
                    {
                        if (value is bool boolValue)
                            return JsonSerializer.SerializeToElement(boolValue);
                        if (value is string stringValue && bool.TryParse(stringValue, out bool parsedBool))
                            return JsonSerializer.SerializeToElement(parsedBool);
                        // Default to false for boolean types
                        return JsonSerializer.SerializeToElement(false);
                    }
                    else if (clrType == typeof(int) || clrType == typeof(long))
                    {
                        if (value is int intValue)
                            return JsonSerializer.SerializeToElement(intValue);
                        if (value is long longValue)
                            return JsonSerializer.SerializeToElement(longValue);
                        if (value is string stringValue && long.TryParse(stringValue, out long parsedLong))
                            return JsonSerializer.SerializeToElement(parsedLong);
                        // Default to 0 for integer types
                        return JsonSerializer.SerializeToElement(0);
                    }
                    else if (clrType == typeof(double) || clrType == typeof(float) || clrType == typeof(decimal))
                    {
                        if (value is double doubleValue)
                            return JsonSerializer.SerializeToElement(doubleValue);
                        if (value is float floatValue)
                            return JsonSerializer.SerializeToElement(floatValue);
                        if (value is decimal decimalValue)
                            return JsonSerializer.SerializeToElement(decimalValue);
                        if (value is string stringValue && double.TryParse(stringValue, out double parsedDouble))
                            return JsonSerializer.SerializeToElement(parsedDouble);
                        // Default to 0.0 for floating point types
                        return JsonSerializer.SerializeToElement(0.0);
                    }
                }
                catch (Exception)
                {
                    // Fall through to default handling
                }
            }

            try
            {
                return JsonSerializer.SerializeToElement(value);
            }
            catch (Exception)
            {
                return JsonDocument.Parse("null").RootElement;
            }
        }
    }
} 
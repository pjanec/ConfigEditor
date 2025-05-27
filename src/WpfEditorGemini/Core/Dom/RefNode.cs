using System;
using System.Text.Json;

namespace JsonConfigEditor.Core.Dom
{
    /// <summary>
    /// Represents a symbolic link within the DOM tree.
    /// Its value is a JSON object of the form {"$ref": "/path/to/another/dom/node"}.
    /// The link can point to another DOM node anywhere in the tree, or potentially to a path outside the currently loaded DOM tree.
    /// (From specification document, Section 2.1)
    /// </summary>
    public class RefNode : DomNode
    {
        private string _referencePath;
        private readonly JsonElement _originalValue;

        /// <summary>
        /// Gets or sets the path string from the "$ref" property.
        /// </summary>
        public string ReferencePath
        {
            get => _referencePath;
            set => _referencePath = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets the original JsonElement that represented this $ref object.
        /// </summary>
        public JsonElement OriginalValue => _originalValue;

        /// <summary>
        /// Initializes a new instance of the RefNode class.
        /// </summary>
        /// <param name="name">The name of the node</param>
        /// <param name="parent">The parent node</param>
        /// <param name="referencePath">The reference path</param>
        /// <param name="originalValue">The original JSON element</param>
        public RefNode(string name, DomNode? parent, string referencePath, JsonElement originalValue) : base(name, parent)
        {
            _referencePath = referencePath ?? throw new ArgumentNullException(nameof(referencePath));
            // Clone the JsonElement to avoid disposal issues
            _originalValue = CloneJsonElement(originalValue);
        }

        /// <summary>
        /// Clones a JsonElement to avoid disposal issues with the underlying JsonDocument.
        /// </summary>
        /// <param name="element">The JsonElement to clone</param>
        /// <returns>A cloned JsonElement backed by a new JsonDocument</returns>
        private static JsonElement CloneJsonElement(JsonElement element)
        {
            // Convert to string and parse back to create a new JsonDocument
            var rawText = element.GetRawText();
            return JsonDocument.Parse(rawText).RootElement;
        }

        /// <summary>
        /// Creates a RefNode from a JsonElement that contains a $ref property.
        /// </summary>
        /// <param name="name">The name of the node</param>
        /// <param name="parent">The parent node</param>
        /// <param name="jsonElement">The JSON element containing the $ref</param>
        /// <returns>A new RefNode instance</returns>
        /// <exception cref="ArgumentException">Thrown if the JSON element doesn't contain a valid $ref property</exception>
        public static RefNode FromJsonElement(string name, DomNode? parent, JsonElement jsonElement)
        {
            if (jsonElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("RefNode must be created from a JSON object", nameof(jsonElement));

            if (!jsonElement.TryGetProperty("$ref", out var refProperty))
                throw new ArgumentException("JSON object must contain a '$ref' property", nameof(jsonElement));

            if (refProperty.ValueKind != JsonValueKind.String)
                throw new ArgumentException("'$ref' property must be a string", nameof(jsonElement));

            var referencePath = refProperty.GetString();
            if (string.IsNullOrEmpty(referencePath))
                throw new ArgumentException("'$ref' property cannot be null or empty", nameof(jsonElement));

            return new RefNode(name, parent, referencePath, jsonElement);
        }

        /// <summary>
        /// Checks if a JsonElement represents a reference node (contains a $ref property).
        /// </summary>
        /// <param name="jsonElement">The JSON element to check</param>
        /// <returns>True if the element represents a reference, false otherwise</returns>
        public static bool IsRefElement(JsonElement jsonElement)
        {
            return jsonElement.ValueKind == JsonValueKind.Object &&
                   jsonElement.TryGetProperty("$ref", out var refProperty) &&
                   refProperty.ValueKind == JsonValueKind.String &&
                   !string.IsNullOrEmpty(refProperty.GetString());
        }

        /// <summary>
        /// Creates a new JsonElement representing this RefNode.
        /// </summary>
        /// <returns>A JsonElement containing the $ref object</returns>
        public JsonElement ToJsonElement()
        {
            var jsonString = $"{{\"$ref\": \"{EscapeJsonString(_referencePath)}\"}}";
            return JsonDocument.Parse(jsonString).RootElement;
        }

        /// <summary>
        /// Escapes a string for use in JSON.
        /// </summary>
        /// <param name="value">The string to escape</param>
        /// <returns>The escaped string</returns>
        private static string EscapeJsonString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>
        /// Gets a display representation of the reference path.
        /// </summary>
        /// <returns>A string representation for display purposes</returns>
        public string GetDisplayValue()
        {
            return $"$ref: {_referencePath}";
        }

        /// <summary>
        /// Validates the reference path syntax.
        /// Only checks the path format (segments separated by forward slashes), not the target existence.
        /// </summary>
        /// <returns>True if the path syntax is valid, false otherwise</returns>
        public bool IsPathSyntaxValid()
        {
            if (string.IsNullOrEmpty(_referencePath))
                return false;

            // Basic validation: should start with / for absolute paths or be a relative path
            // For now, we'll accept any non-empty string as potentially valid
            // More sophisticated validation could be added here
            return !string.IsNullOrWhiteSpace(_referencePath);
        }

        /// <summary>
        /// Determines if this reference points to an external path (outside the current DOM).
        /// This is a heuristic based on the path format.
        /// </summary>
        /// <returns>True if the path appears to be external, false otherwise</returns>
        public bool IsExternalReference()
        {
            // Simple heuristic: if the path contains a protocol or starts with certain patterns
            // it might be external. This could be enhanced based on specific requirements.
            return _referencePath.Contains("://") || 
                   _referencePath.StartsWith("file:") ||
                   _referencePath.StartsWith("http:") ||
                   _referencePath.StartsWith("https:");
        }

        /// <summary>
        /// Returns a string representation of this reference node.
        /// </summary>
        public override string ToString()
        {
            return $"RefNode: {Name} -> {_referencePath} (Path: {Path})";
        }
    }
} 
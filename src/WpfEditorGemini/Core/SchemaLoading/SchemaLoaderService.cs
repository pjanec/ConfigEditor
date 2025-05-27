using JsonConfigEditor.Contracts.Attributes;
using JsonConfigEditor.Core.Schema;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace JsonConfigEditor.Core.SchemaLoading
{
    /// <summary>
    /// Service responsible for loading schema definitions from C# assemblies.
    /// (From specification document, Section 2.2)
    /// </summary>
    public class SchemaLoaderService : ISchemaLoaderService
    {
        private readonly Dictionary<string, SchemaNode> _rootSchemas = new();
        private readonly List<string> _errorMessages = new();

        /// <summary>
        /// Gets the loaded root schemas keyed by their mount paths.
        /// </summary>
        public IReadOnlyDictionary<string, SchemaNode> RootSchemas => _rootSchemas;

        /// <summary>
        /// Gets any error messages that occurred during schema loading.
        /// </summary>
        public IReadOnlyList<string> ErrorMessages => _errorMessages;

        /// <summary>
        /// Asynchronously loads schema definitions from the specified assembly paths.
        /// </summary>
        /// <param name="assemblyPaths">The paths to assemblies containing schema classes</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LoadSchemasFromAssembliesAsync(IEnumerable<string> assemblyPaths)
        {
            await Task.Run(() =>
            {
                _rootSchemas.Clear();
                _errorMessages.Clear();

                foreach (var assemblyPath in assemblyPaths)
                {
                    try
                    {
                        ProcessAssemblyPath(assemblyPath);
                    }
                    catch (Exception ex)
                    {
                        _errorMessages.Add($"Failed to process assembly path '{assemblyPath}': {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Finds the most specific schema node for a given DOM path.
        /// </summary>
        /// <param name="domPath">The path in the DOM tree</param>
        /// <returns>The most specific schema node, or null if no schema matches</returns>
        public SchemaNode? FindSchemaForPath(string domPath)
        {
            if (string.IsNullOrEmpty(domPath))
                return null;

            // Find the longest matching mount path
            var bestMatch = _rootSchemas
                .Where(kvp => domPath.StartsWith(kvp.Key) || string.IsNullOrEmpty(kvp.Key))
                .OrderByDescending(kvp => kvp.Key.Length)
                .FirstOrDefault();

            if (bestMatch.Value == null)
                return null;

            var rootSchema = bestMatch.Value;
            var mountPath = bestMatch.Key;

            // If the path exactly matches the mount path, return the root schema
            if (domPath == mountPath || (string.IsNullOrEmpty(mountPath) && string.IsNullOrEmpty(domPath)))
                return rootSchema;

            // Navigate within the schema to find the specific node
            var relativePath = string.IsNullOrEmpty(mountPath) ? domPath : domPath.Substring(mountPath.Length);
            if (relativePath.StartsWith("/"))
                relativePath = relativePath.Substring(1);

            return NavigateSchemaPath(rootSchema, relativePath);
        }

        /// <summary>
        /// Clears all loaded schemas and error messages.
        /// </summary>
        public void Clear()
        {
            _rootSchemas.Clear();
            _errorMessages.Clear();
        }

        /// <summary>
        /// Processes an assembly path (file or directory).
        /// </summary>
        private void ProcessAssemblyPath(string assemblyPath)
        {
            if (Directory.Exists(assemblyPath))
            {
                // Process all .dll files in the directory
                var dllFiles = Directory.GetFiles(assemblyPath, "*.dll", SearchOption.TopDirectoryOnly);
                foreach (var dllFile in dllFiles)
                {
                    ProcessAssemblyFile(dllFile);
                }
            }
            else if (File.Exists(assemblyPath) && assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                ProcessAssemblyFile(assemblyPath);
            }
            else
            {
                _errorMessages.Add($"Assembly path '{assemblyPath}' is not a valid file or directory");
            }
        }

        /// <summary>
        /// Processes a single assembly file.
        /// </summary>
        private void ProcessAssemblyFile(string assemblyFilePath)
        {
            try
            {
                var assembly = Assembly.LoadFrom(assemblyFilePath);
                ProcessAssembly(assembly);
            }
            catch (Exception ex)
            {
                _errorMessages.Add($"Failed to load assembly '{assemblyFilePath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a loaded assembly to find schema classes.
        /// </summary>
        private void ProcessAssembly(Assembly assembly)
        {
            try
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    var configSchemaAttr = type.GetCustomAttribute<ConfigSchemaAttribute>();
                    if (configSchemaAttr != null)
                    {
                        ProcessSchemaClass(type, configSchemaAttr);
                    }
                }
            }
            catch (Exception ex)
            {
                _errorMessages.Add($"Failed to process types in assembly '{assembly.FullName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a class marked with ConfigSchemaAttribute.
        /// </summary>
        private void ProcessSchemaClass(Type schemaType, ConfigSchemaAttribute configSchemaAttr)
        {
            try
            {
                var mountPath = configSchemaAttr.MountPath;

                // Check for duplicate mount paths
                if (_rootSchemas.ContainsKey(mountPath))
                {
                    _errorMessages.Add($"Duplicate mount path '{mountPath}' found in type '{schemaType.FullName}'. Previous definition will be overwritten.");
                }

                // Build the schema recursively
                var processedTypes = new HashSet<Type>();
                var schemaNode = BuildSchemaRecursive(schemaType, schemaType.Name, null, processedTypes);
                
                // Set the mount path for the root schema
                var rootSchema = new SchemaNode(
                    schemaNode.Name,
                    schemaNode.ClrType,
                    schemaNode.IsRequired,
                    schemaNode.IsReadOnly,
                    schemaNode.DefaultValue,
                    schemaNode.Min,
                    schemaNode.Max,
                    schemaNode.RegexPattern,
                    schemaNode.AllowedValues,
                    schemaNode.IsEnumFlags,
                    schemaNode.Properties,
                    schemaNode.AdditionalPropertiesSchema,
                    schemaNode.AllowAdditionalProperties,
                    schemaNode.ItemSchema,
                    mountPath);

                _rootSchemas[mountPath] = rootSchema;
            }
            catch (Exception ex)
            {
                _errorMessages.Add($"Failed to process schema class '{schemaType.FullName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively builds a schema node from a C# type.
        /// </summary>
        private SchemaNode BuildSchemaRecursive(Type currentType, string nodeName, PropertyInfo? sourcePropertyInfo, HashSet<Type> processedTypesInPath)
        {
            // Prevent infinite recursion
            if (processedTypesInPath.Contains(currentType))
            {
                // Return a simple schema for recursive types
                return new SchemaNode(nodeName, currentType, false, false, null, null, null, null, null, false, null, null, false, null);
            }

            processedTypesInPath.Add(currentType);

            try
            {
                // Get property-level attributes if this is from a property
                var isRequired = IsPropertyRequired(sourcePropertyInfo, currentType);
                var isReadOnly = IsPropertyReadOnly(sourcePropertyInfo);
                var defaultValue = GetDefaultValue(sourcePropertyInfo, currentType);
                var (min, max) = GetMinMaxValues(sourcePropertyInfo);
                var regexPattern = GetRegexPattern(sourcePropertyInfo);
                var allowedValues = GetAllowedValues(sourcePropertyInfo, currentType);
                var isEnumFlags = currentType.IsEnum && currentType.GetCustomAttribute<FlagsAttribute>() != null;

                // Determine the schema type and build accordingly
                if (IsCollectionType(currentType, out var itemType))
                {
                    // Array type
                    var itemSchema = itemType != null ? BuildSchemaRecursive(itemType, "*", null, new HashSet<Type>(processedTypesInPath)) : null;
                    return new SchemaNode(nodeName, currentType, isRequired, isReadOnly, defaultValue, min, max, regexPattern, allowedValues, isEnumFlags, null, null, false, itemSchema);
                }
                else if (IsDictionaryType(currentType, out var valueType))
                {
                    // Object type with additional properties
                    var additionalPropertiesSchema = valueType != null ? BuildSchemaRecursive(valueType, "*", null, new HashSet<Type>(processedTypesInPath)) : null;
                    return new SchemaNode(nodeName, currentType, isRequired, isReadOnly, defaultValue, min, max, regexPattern, allowedValues, isEnumFlags, null, additionalPropertiesSchema, true, null);
                }
                else if (IsComplexType(currentType))
                {
                    // Object type with defined properties
                    var properties = BuildPropertiesSchema(currentType, processedTypesInPath);
                    return new SchemaNode(nodeName, currentType, isRequired, isReadOnly, defaultValue, min, max, regexPattern, allowedValues, isEnumFlags, properties, null, false, null);
                }
                else
                {
                    // Value type
                    return new SchemaNode(nodeName, currentType, isRequired, isReadOnly, defaultValue, min, max, regexPattern, allowedValues, isEnumFlags, null, null, false, null);
                }
            }
            finally
            {
                processedTypesInPath.Remove(currentType);
            }
        }

        /// <summary>
        /// Builds the properties schema for a complex type.
        /// </summary>
        private Dictionary<string, SchemaNode> BuildPropertiesSchema(Type objectType, HashSet<Type> processedTypesInPath)
        {
            var properties = new Dictionary<string, SchemaNode>();

            var publicProperties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0); // Exclude indexers

            foreach (var property in publicProperties)
            {
                try
                {
                    var propertySchema = BuildSchemaRecursive(property.PropertyType, property.Name, property, processedTypesInPath);
                    properties[property.Name] = propertySchema;
                }
                catch (Exception ex)
                {
                    _errorMessages.Add($"Failed to build schema for property '{property.Name}' in type '{objectType.FullName}': {ex.Message}");
                }
            }

            return properties;
        }

        /// <summary>
        /// Navigates within a schema to find a node at the specified path.
        /// </summary>
        private SchemaNode? NavigateSchemaPath(SchemaNode rootSchema, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return rootSchema;

            var pathSegments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentSchema = rootSchema;

            foreach (var segment in pathSegments)
            {
                if (currentSchema.Properties != null && currentSchema.Properties.TryGetValue(segment, out var propertySchema))
                {
                    currentSchema = propertySchema;
                }
                else if (currentSchema.ItemSchema != null && int.TryParse(segment, out _))
                {
                    // Array index
                    currentSchema = currentSchema.ItemSchema;
                }
                else if (currentSchema.AdditionalPropertiesSchema != null)
                {
                    // Dynamic property
                    currentSchema = currentSchema.AdditionalPropertiesSchema;
                }
                else
                {
                    return null; // Path not found
                }
            }

            return currentSchema;
        }

        // Helper methods for reflection and attribute processing

        private bool IsPropertyRequired(PropertyInfo? propertyInfo, Type propertyType)
        {
            if (propertyInfo == null)
                return false;

            // Check for nullable reference types or value types
            var nullabilityContext = new NullabilityInfoContext();
            var nullabilityInfo = nullabilityContext.Create(propertyInfo);
            
            return nullabilityInfo.WriteState == NullabilityState.NotNull;
        }

        private bool IsPropertyReadOnly(PropertyInfo? propertyInfo)
        {
            return propertyInfo?.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly ?? false;
        }

        private object? GetDefaultValue(PropertyInfo? propertyInfo, Type propertyType)
        {
            // Try to get default value from property initializer or constructor
            // This is a simplified implementation - in practice, this would be more complex
            try
            {
                if (propertyType.IsValueType)
                {
                    return Activator.CreateInstance(propertyType);
                }
                else if (propertyType.GetConstructor(Type.EmptyTypes) != null)
                {
                    return Activator.CreateInstance(propertyType);
                }
            }
            catch
            {
                // Ignore errors in default value creation
            }

            return null;
        }

        private (double? min, double? max) GetMinMaxValues(PropertyInfo? propertyInfo)
        {
            var rangeAttr = propertyInfo?.GetCustomAttribute<RangeAttribute>();
            if (rangeAttr != null)
            {
                return (Convert.ToDouble(rangeAttr.Minimum), Convert.ToDouble(rangeAttr.Maximum));
            }
            return (null, null);
        }

        private string? GetRegexPattern(PropertyInfo? propertyInfo)
        {
            return propertyInfo?.GetCustomAttribute<SchemaRegexPatternAttribute>()?.Pattern;
        }

        private List<string>? GetAllowedValues(PropertyInfo? propertyInfo, Type propertyType)
        {
            // Check for custom allowed values attribute
            var allowedValuesAttr = propertyInfo?.GetCustomAttribute<SchemaAllowedValuesAttribute>();
            if (allowedValuesAttr != null)
            {
                return allowedValuesAttr.AllowedValues.ToList();
            }

            // Check for enum types
            if (propertyType.IsEnum)
            {
                return Enum.GetNames(propertyType).ToList();
            }

            return null;
        }

        private bool IsCollectionType(Type type, out Type? itemType)
        {
            itemType = null;

            if (type.IsArray)
            {
                itemType = type.GetElementType();
                return true;
            }

            if (type.IsGenericType)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();
                if (genericTypeDef == typeof(List<>) || genericTypeDef == typeof(IList<>) || 
                    genericTypeDef == typeof(ICollection<>) || genericTypeDef == typeof(IEnumerable<>))
                {
                    itemType = type.GetGenericArguments()[0];
                    return true;
                }
            }

            return false;
        }

        private bool IsDictionaryType(Type type, out Type? valueType)
        {
            valueType = null;

            if (type.IsGenericType)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();
                if (genericTypeDef == typeof(Dictionary<,>) || genericTypeDef == typeof(IDictionary<,>))
                {
                    var genericArgs = type.GetGenericArguments();
                    if (genericArgs[0] == typeof(string)) // Key must be string
                    {
                        valueType = genericArgs[1];
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsComplexType(Type type)
        {
            return !type.IsPrimitive && 
                   type != typeof(string) && 
                   type != typeof(DateTime) && 
                   type != typeof(decimal) && 
                   !type.IsEnum &&
                   type != typeof(Guid);
        }
    }
} 
using JsonConfigEditor.Contracts.Attributes;
using JsonConfigEditor.Core.Schema;
using System;
using System.Collections; // Required for IDictionary, IEnumerable
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using JsonConfigEditor.Contracts.Rendering;
using JsonConfigEditor.Contracts.Editors;
using JsonConfigEditor.Contracts.Tooltips;
using JsonConfigEditor.Wpf.Services; // Assuming this is for CustomUIRegistryService

namespace JsonConfigEditor.Core.SchemaLoading
{
    /// <summary>
    /// Service responsible for loading schema definitions from C# assemblies.
    /// (From specification document, Section 2.2)
    /// </summary>
    public class SchemaLoaderService : ISchemaLoaderService
    {
        private readonly Dictionary<string, SchemaNode> _rootSchemas = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _errorMessages = new();
        private readonly List<string> _logMessages = new(); // NEW
        private readonly CustomUIRegistryService _uiRegistry;
        
        // Missing field from previous erroneous edit
        private readonly Dictionary<string, Type> _processedTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaLoaderService"/> class.
        /// </summary>
        /// <param name="uiRegistry">The UI registry service for custom renderers and editors.</param>
        public SchemaLoaderService(CustomUIRegistryService uiRegistry)
        {
            _uiRegistry = uiRegistry ?? throw new ArgumentNullException(nameof(uiRegistry));
        }

        /// <summary>
        /// Gets the loaded root schemas keyed by their mount paths.
        /// </summary>
        public IReadOnlyDictionary<string, SchemaNode> RootSchemas => _rootSchemas;

        /// <summary>
        /// Gets any error messages that occurred during schema loading.
        /// </summary>
        public IReadOnlyList<string> ErrorMessages => _errorMessages;

        // NEW: Implement the new property
        public IReadOnlyList<string> LogMessages => _logMessages;

        /// <summary>
        /// Asynchronously loads schema definitions from the specified assembly paths.
        /// </summary>
        /// <param name="assemblyPaths">The paths to assemblies containing schema classes</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LoadSchemasFromAssembliesAsync(IEnumerable<string> assemblyPaths)
        {
            await Task.Run(() => // Use Task.Run for CPU-bound work on a background thread
            {
                _rootSchemas.Clear();
                _errorMessages.Clear();
                _logMessages.Clear(); // Clear log messages when loading starts
                _uiRegistry.ClearRegistry(); // Assuming this is safe to call multiple times
                _processedTypes.Clear(); // Clear for fresh loading session

                foreach (var assemblyPath in assemblyPaths)
                {
                    try
                    {
                        ProcessAssemblyPath(assemblyPath);
                    }
                    catch (Exception ex)
                    {
                        _errorMessages.Add($"SchemaLoaderService: Failed to process assembly path '{assemblyPath}': {ex.Message}");
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
            if (domPath == null) throw new ArgumentNullException(nameof(domPath));

            var bestMatchRoot = _rootSchemas
                .Where(kvp => domPath.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(kvp.Key)) // "" mount path is a candidate for all
                .OrderByDescending(kvp => kvp.Key.Length)
                .Select(kvp => kvp.Value)
                .FirstOrDefault();

            if (bestMatchRoot == null) return null;

            string relativePath = domPath;
            if (!string.IsNullOrEmpty(bestMatchRoot.MountPath) && domPath.StartsWith(bestMatchRoot.MountPath, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = domPath.Substring(bestMatchRoot.MountPath.Length);
            }
            relativePath = relativePath.TrimStart('/');

            return NavigateSchemaPath(bestMatchRoot, relativePath);
        }

        /// <summary>
        /// Clears all loaded schemas and error messages.
        /// </summary>
        public void Clear()
        {
            _rootSchemas.Clear();
            _errorMessages.Clear();
            _logMessages.Clear(); // Clear log messages when clearing
            _processedTypes.Clear();
            _uiRegistry.ClearRegistry();
        }

        /// <summary>
        /// Gets the primary or first loaded root schema.
        /// This implementation returns the schema associated with the "$root" mount path if present,
        /// otherwise the first schema in the dictionary, or null if empty.
        /// </summary>
        public SchemaNode? GetRootSchema()
        {
            // Prefer empty string "" as the key for the absolute root schema, as per common interpretation.
            if (_rootSchemas.TryGetValue("", out var rootSchema))
            {
                return rootSchema;
            }
            // Fallback to "$root" if that's used, or first as ultimate fallback.
            if (_rootSchemas.TryGetValue("$root", out rootSchema))
            {
                return rootSchema;
            }
            return _rootSchemas.Values.FirstOrDefault();
        }

        /// <summary>
        /// Processes an assembly path (file or directory).
        /// </summary>
        private void ProcessAssemblyPath(string assemblyPath)
        {
            if (Directory.Exists(assemblyPath))
            {
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
                _errorMessages.Add($"SchemaLoaderService: Assembly path '{assemblyPath}' is not a valid file or directory.");
            }
        }

        /// <summary>
        /// Processes a single assembly file.
        /// </summary>
        private void ProcessAssemblyFile(string assemblyFilePath)
        {
            // In ProcessAssemblyFile method, at the beginning
            _logMessages.Add($"Scanning assembly: {assemblyFilePath}");
            try
            {
                var assembly = Assembly.LoadFrom(assemblyFilePath); // Consider AssemblyLoadContext for unloadability if needed
                ProcessAssembly(assembly);
            }
            catch (ReflectionTypeLoadException ex)
            {
                _errorMessages.Add($"SchemaLoaderService: Failed to load assembly '{assemblyFilePath}' due to type load errors: {ex.Message}");
                if (ex.LoaderExceptions != null)
                {
                    foreach (var loaderException in ex.LoaderExceptions)
                    {
                        if (loaderException != null)
                            _errorMessages.Add($"SchemaLoaderService:   LoaderException: {loaderException.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _errorMessages.Add($"SchemaLoaderService: Failed to load assembly '{assemblyFilePath}': {ex.Message}");
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
                    try
                    {
                        var configSchemaAttr = type.GetCustomAttribute<ConfigSchemaAttribute>();
                        if (configSchemaAttr != null)
                        {
                            ProcessSchemaClass(type, configSchemaAttr);
                        }
                        DiscoverAndRegisterCustomUIComponents(type);
                    }
                    catch (Exception ex)
                    {
                         _errorMessages.Add($"SchemaLoaderService: Error processing type '{type.FullName}' in assembly '{assembly.FullName}': {ex.Message}");
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                _errorMessages.Add($"SchemaLoaderService: Failed to process types in assembly '{assembly.FullName}' due to type load errors: {ex.Message}");
                if (ex.LoaderExceptions != null)
                {
                    foreach (var loaderException in ex.LoaderExceptions)
                    {
                        if (loaderException != null)
                        _errorMessages.Add($"SchemaLoaderService:   LoaderException: {loaderException.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _errorMessages.Add($"SchemaLoaderService: Failed to process types in assembly '{assembly.FullName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a class marked with ConfigSchemaAttribute.
        /// </summary>
        private void ProcessSchemaClass(Type schemaClassType, ConfigSchemaAttribute configSchemaAttribute)
        {
            var mountPath = configSchemaAttribute.MountPath ?? ""; // Ensure mountPath is not null

            if (_rootSchemas.ContainsKey(mountPath))
            {
                _errorMessages.Add($"SchemaLoaderService: Duplicate mount path '{mountPath}' for type '{schemaClassType.FullName}'. Previous: '{_rootSchemas[mountPath].ClrType.FullName}'. Overwriting.");
            }
            
            // Pass schemaClassType.Name as the initial name for the root schema node
            var rootNode = ParseTypeRecursive(schemaClassType, schemaClassType.Name, mountPath, memberInfo: null, isRootConfigSchemaType: true);
            if (rootNode != null)
            {
                // The MountPath is an intrinsic property of the root SchemaNode, set via constructor or property
                // If SchemaNode constructor doesn't take mountPath directly for all nodes, ensure it's set here for roots.
                // Assuming SchemaNode has a constructor or property for MountPath.
                // If the SchemaNode constructor used by ParseTypeRecursive doesn't set MountPath, 
                // we might need a different constructor or a setter for the root one.
                // For now, let's assume ParseTypeRecursive sets currentPath, and for root, currentPath IS mountPath.
                // The current SchemaNode constructor takes mountPath as the last optional string parameter.
                // We need to ensure this is correctly passed or reconstructed.

                // Reconstruct the root node with the mount path IF the main constructor doesn't take it
                // OR if ParseTypeRecursive's `currentPath` isn't automatically the MountPath for the root.
                // Based on SchemaNode provided, it has an optional mountPath.
                // ParseTypeRecursive passes `currentPath` which is `mountPath` for the root.

                _rootSchemas[mountPath] = rootNode; // rootNode should have its MountPath correctly set if constructor handles it
                // In ProcessSchemaClass method, after successfully creating rootNode
                _logMessages.Add($"  -> Found root schema '{schemaClassType.FullName}' with mount path '{mountPath}'");
                 System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ProcessSchemaClass: Registered root schema for Type='{schemaClassType.FullName}', MountPath='{mountPath}', SchemaNodeName='{rootNode.Name}'");
            }
            else
            {
                 _errorMessages.Add($"SchemaLoaderService: Failed to parse root schema for type '{schemaClassType.FullName}' at mount path '{mountPath}'.");
            }
        }

        /// <summary>
        /// Navigates within a schema to find a node at the specified path.
        /// </summary>
        private SchemaNode? NavigateSchemaPath(SchemaNode currentSchema, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return currentSchema;

            var segments = relativePath.Split('/');
            foreach (var segment in segments)
            {
                if (currentSchema.Properties != null && currentSchema.Properties.TryGetValue(segment, out var propertySchema))
                {
                    currentSchema = propertySchema;
                }
                else if (currentSchema.ItemSchema != null && int.TryParse(segment, out _)) // Array item
                {
                    currentSchema = currentSchema.ItemSchema;
                }
                else if (currentSchema.AdditionalPropertiesSchema != null) // Dictionary item
                {
                    currentSchema = currentSchema.AdditionalPropertiesSchema;
                }
                else
                {
                    return null; // Path segment not found
                }
            }
            return currentSchema;
        }

        private SchemaNode? ParseTypeRecursive(Type type, string name, string currentPath, MemberInfo? memberInfo = null, bool isRootConfigSchemaType = false)
        {
            // Logging entry point
            System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive: Parsing Type='{type.FullName}', Name='{name}', Path='{currentPath}', IsRootConfig='{isRootConfigSchemaType}'");

            if (_processedTypes.TryGetValue(currentPath, out var foundType) && foundType == type)
            {
                System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive:     SKIPPING Type='{type.FullName}' at Path='{currentPath}' due to cycle (already processed this type at this exact path).");
                return new SchemaNode(name, type, false, true, null, null, null, null, null, false, null, null, false, null, isRootConfigSchemaType ? currentPath : null); // Return a stub for cycles
            }
            _processedTypes[currentPath] = type;

            var nodeType = GetSchemaNodeType(type);
            var remarks = new List<string>();

            // Property-specific attributes (if memberInfo is PropertyInfo)
            PropertyInfo? propertyInfo = memberInfo as PropertyInfo;
            bool isRequired = propertyInfo != null && IsPropertyRequired(propertyInfo, type);
            bool isReadOnly = propertyInfo != null && IsPropertyReadOnly(propertyInfo);
            object? defaultValue = GetDefaultValue(propertyInfo, type); // Handles both PropertyInfo and Type
            (double? min, double? max) = propertyInfo != null ? GetMinMaxValues(propertyInfo) : (null, null);
            string? regexPattern = propertyInfo != null ? GetRegexPattern(propertyInfo) : null;
            List<string>? allowedValues = GetAllowedValues(propertyInfo, type); // Handles both
            bool isEnumFlags = type.IsEnum && type.GetCustomAttribute<FlagsAttribute>() != null;

            // Mount path is only for the absolute root of a schema tree from [ConfigSchema]
            string? mountPathForNode = isRootConfigSchemaType ? currentPath : null;

            SchemaNode? resultNode = null;

            if (nodeType == SchemaNodeType.Value)
            {
                resultNode = new SchemaNode(name, type, isRequired, isReadOnly, defaultValue, min, max, regexPattern, allowedValues, isEnumFlags, null, null, false, null, mountPathForNode);
                System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive: Created VALUE SchemaNode for Path='{currentPath}', Name='{name}', ClrType='{type.FullName}'");
            }
            else if (nodeType == SchemaNodeType.Array)
            {
                System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive: Path='{currentPath}', Name='{name}' is ARRAY type.");
                resultNode = CreateArraySchemaNode(type, name, currentPath, isRequired, isReadOnly, defaultValue, mountPathForNode, memberInfo, isRootConfigSchemaType);
            }
            else if (nodeType == SchemaNodeType.Object) // This includes Dictionaries now based on GetSchemaNodeType
            {
                 if (type.IsGenericType && typeof(IDictionary).IsAssignableFrom(type) && type.GetGenericArguments().Length == 2 && type.GetGenericArguments()[0] == typeof(string))
                 {
                    System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive: Path='{currentPath}', Name='{name}' is DICTIONARY<string, T> type.");
                    resultNode = CreateDictionarySchemaNode(type, name, currentPath, isRequired, isReadOnly, defaultValue, mountPathForNode, memberInfo, isRootConfigSchemaType);
                 }
                 else // Regular complex object
                 {
                    System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive: Path='{currentPath}', Name='{name}' is COMPLEX OBJECT type. Iterating properties...");
                    var properties = new Dictionary<string, SchemaNode>(StringComparer.OrdinalIgnoreCase);
                    foreach (var propInfo in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive:   Processing Property='{propInfo.Name}' of Type='{type.FullName}' (PropertyType='{propInfo.PropertyType.FullName}')");
                        if (ShouldIgnoreProperty(propInfo))
                        {
                            System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive:     IGNORING Property='{propInfo.Name}' due to ShouldIgnoreProperty (e.g., [JsonIgnore]).");
                            continue;
                        }
                        string propertyPath = string.IsNullOrEmpty(currentPath) ? propInfo.Name : $"{currentPath}/{propInfo.Name}";
                        
                        System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive:     RECURSING for Property='{propInfo.Name}', PropertyPath='{propertyPath}'");
                        var propertySchema = ParseTypeRecursive(propInfo.PropertyType, propInfo.Name, propertyPath, propInfo, false); // isRootConfigSchemaType is false for children
                        if (propertySchema != null)
                        {
                            properties[propInfo.Name] = propertySchema;
                            System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive:     SUCCESSFULLY parsed Property='{propInfo.Name}', added to Properties dictionary for '{name}'.");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive:     FAILED to parse Property='{propInfo.Name}' (returned null), NOT added for '{name}'.");
                        }
                    }
                    resultNode = new SchemaNode(name, type, isRequired, isReadOnly, defaultValue, min, max, regexPattern, allowedValues, isEnumFlags, properties, null, properties.Any(), null, mountPathForNode);
                    System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive: Created OBJECT SchemaNode for Path='{currentPath}', Name='{name}', ClrType='{type.FullName}', Properties.Count='{properties.Count}'");
                 }
            }
            else 
            {
                 System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive: Path='{currentPath}', Name='{name}', Type='{type.FullName}' resulted in UNKNOWN SchemaNodeType from GetSchemaNodeType. Returning null.");
            }
            
            _processedTypes.Remove(currentPath); // Backtrack for current path
            return resultNode;
        }
        
        private SchemaNodeType GetSchemaNodeType(Type type)
        {
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(Guid) || type.IsEnum)
            {
                return SchemaNodeType.Value;
            }
            // Check for IEnumerable<T> but not IDictionary specifically
            if (type.IsArray || (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type) && !typeof(IDictionary).IsAssignableFrom(type) && type.GetGenericArguments().Length == 1) )
            {
                return SchemaNodeType.Array;
            }
            // Covers classes, structs (that are not primitive/enum), and IDictionary<string, T>
            if (type.IsClass || (type.IsValueType && !type.IsEnum && !type.IsPrimitive) || typeof(IDictionary).IsAssignableFrom(type))
            {
                return SchemaNodeType.Object;
            }
            System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.GetSchemaNodeType: Type '{type.FullName}' fell through to fallback as ValueNode.");
            return SchemaNodeType.Value; // Fallback
        }

        private SchemaNode CreateArraySchemaNode(Type type, string name, string currentPath, bool isRequired, bool isReadOnly, object? defaultValue, string? mountPath, MemberInfo? memberInfo, bool isRootConfigSchemaType)
        {
            Type? itemType = null;
            if (type.IsArray) itemType = type.GetElementType();
            else if (type.IsGenericType && type.GetGenericArguments().Length == 1)
            {
                itemType = type.GetGenericArguments()[0];
            }

            SchemaNode? itemSchema = null;
            if (itemType != null)
            {
                string itemPath = $"{currentPath}/*"; // Placeholder path for item schema definition
                 System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.CreateArraySchemaNode: Recursing for ItemSchema of Array='{name}', ItemType='{itemType.FullName}', ItemPath='{itemPath}'");
                itemSchema = ParseTypeRecursive(itemType, "*", itemPath, null, false); // Name for item schema is typically "*"
            }
            var arrayNode = new SchemaNode(name, type, isRequired, isReadOnly, defaultValue, null, null, null, null, false, null, null, false, itemSchema, mountPath);
            System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive: Created ARRAY SchemaNode for Path='{currentPath}', Name='{name}', ClrType='{type.FullName}', ItemSchemaName='{itemSchema?.Name}'");
            return arrayNode;
        }

        private SchemaNode CreateDictionarySchemaNode(Type type, string name, string currentPath, bool isRequired, bool isReadOnly, object? defaultValue, string? mountPath, MemberInfo? memberInfo, bool isRootConfigSchemaType)
        {
            SchemaNode? additionalPropertiesSchema = null;
            if (type.IsGenericType && type.GetGenericArguments().Length == 2)
            {
                var valueType = type.GetGenericArguments()[1];
                string valuePath = $"{currentPath}/*"; // Placeholder for value schema definition
                System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.CreateDictionarySchemaNode: Recursing for AdditionalPropertiesSchema of Dictionary='{name}', ValueType='{valueType.FullName}', ValuePath='{valuePath}'");
                additionalPropertiesSchema = ParseTypeRecursive(valueType, "*", valuePath, null, false);
            }
            // Dictionaries allow additional properties by nature.
            var dictNode = new SchemaNode(name, type, isRequired, isReadOnly, defaultValue, null, null, null, null, false, null, additionalPropertiesSchema, true, null, mountPath);
            System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.ParseTypeRecursive: Created DICTIONARY SchemaNode for Path='{currentPath}', Name='{name}', ClrType='{type.FullName}', AdditionalPropertiesSchemaName='{additionalPropertiesSchema?.Name}'");
            return dictNode;
        }
        
        private bool ShouldIgnoreProperty(PropertyInfo propertyInfo)
        {
            // Only consider System.Text.Json.Serialization.JsonIgnoreAttribute
            return propertyInfo.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() != null;
        }
        
        private bool IsPropertyRequired(PropertyInfo propertyInfo, Type propertyType)
        {
            var nullabilityContext = new NullabilityInfoContext();
            var nullabilityInfo = nullabilityContext.Create(propertyInfo);
            return nullabilityInfo.WriteState == NullabilityState.NotNull;
        }

        private bool IsPropertyReadOnly(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly ?? false;
        }
        
        private object? GetDefaultValue(PropertyInfo? propertyInfo, Type typeForDefault)
        {
            if (propertyInfo != null)
            {
                var defaultValueAttr = propertyInfo.GetCustomAttribute<DefaultValueAttribute>();
                if (defaultValueAttr != null) return defaultValueAttr.Value;

                // Try to get the value from the property initializer by creating a new instance
                // of the declaring type. This requires the class to have a parameterless constructor.
                var declaringType = propertyInfo.DeclaringType;
                if (declaringType != null && declaringType.GetConstructor(Type.EmptyTypes) != null)
                {
                    try
                    {
                        var instance = Activator.CreateInstance(declaringType);
                        return propertyInfo.GetValue(instance);
                    }
                    catch 
                    { 
                        // Instantiation or getting the value might fail, so we fall through.
                    }
                }
            }
            try
            {
                 // For value types, Activator.CreateInstance returns their default (0, false, etc.)
                if (typeForDefault.IsValueType) return Activator.CreateInstance(typeForDefault);
                // For reference types with a parameterless constructor, create an instance.
                if (typeForDefault.GetConstructor(Type.EmptyTypes) != null) return Activator.CreateInstance(typeForDefault);
            }
            catch (Exception ex) {
                 System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.GetDefaultValue: Error creating default for Type='{typeForDefault.FullName}', Property='{propertyInfo?.Name}'. Ex: {ex.Message}");
            }
            return null; 
        }

        private (double? min, double? max) GetMinMaxValues(PropertyInfo propertyInfo)
        {
            var rangeAttr = propertyInfo.GetCustomAttribute<RangeAttribute>();
            if (rangeAttr != null)
            {
                try 
                {
                    object minVal = rangeAttr.Minimum;
                    object maxVal = rangeAttr.Maximum;
                    if (minVal is IConvertible && maxVal is IConvertible)
                    { 
                        return (Convert.ToDouble(minVal), Convert.ToDouble(maxVal)); 
                    }
                }
                catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"SchemaLoaderService.GetMinMaxValues: Error converting RangeAttribute for Property='{propertyInfo.Name}'. Ex: {ex.Message}");
                }
            }
            return (null, null);
        }

        private string? GetRegexPattern(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<SchemaRegexPatternAttribute>()?.Pattern;
        }

        private List<string>? GetAllowedValues(PropertyInfo? propertyInfo, Type typeForEnumCheck)
        {
            if (propertyInfo != null)
            {
                var allowedValuesAttr = propertyInfo.GetCustomAttribute<SchemaAllowedValuesAttribute>();
                if (allowedValuesAttr != null) return allowedValuesAttr.AllowedValues.ToList();
            }
            if (typeForEnumCheck.IsEnum) return Enum.GetNames(typeForEnumCheck).ToList();
            return null;
        }
        
        private void DiscoverAndRegisterCustomUIComponents(Type typeInAssembly)
        {
            if (!typeInAssembly.IsClass || typeInAssembly.IsAbstract) return;

            var rendererAttrs = typeInAssembly.GetCustomAttributes<ValueRendererAttribute>(false);
            foreach (var attr in rendererAttrs)
            {
                if (typeof(IValueRenderer).IsAssignableFrom(typeInAssembly))
                    _uiRegistry.RegisterRenderer(attr.TargetClrType, typeInAssembly);
                else
                     _errorMessages.Add($"UI Discovery Error: Type '{typeInAssembly.FullName}' has ValueRendererAttribute but does not implement IValueRenderer.");
            }

            var editorAttrs = typeInAssembly.GetCustomAttributes<ValueEditorAttribute>(false);
            foreach (var attr in editorAttrs)
            {
                if (typeof(IValueEditor).IsAssignableFrom(typeInAssembly))
                    _uiRegistry.RegisterEditor(attr.TargetClrType, typeInAssembly, attr.RequiresModal);
                else
                    _errorMessages.Add($"UI Discovery Error: Type '{typeInAssembly.FullName}' has ValueEditorAttribute but does not implement IValueEditor.");
            }

            var tooltipAttrs = typeInAssembly.GetCustomAttributes<TooltipProviderAttribute>(false);
            foreach (var attr in tooltipAttrs)
            {
                if (typeof(ITooltipProvider).IsAssignableFrom(typeInAssembly))
                     _uiRegistry.RegisterTooltipProvider(attr.TargetClrType, typeInAssembly);
                else
                    _errorMessages.Add($"UI Discovery Error: Type '{typeInAssembly.FullName}' has TooltipProviderAttribute but does not implement ITooltipProvider.");
            }
        }
    }
} 
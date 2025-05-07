using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ConfigDom
{
    /// <summary>
    /// Dynamically discovers and registers schema types from assemblies at runtime.
    /// Schema types are identified by a custom attribute and grouped by mount path.
    /// </summary>
    public class RuntimeSchemaCatalog
    {
        private readonly Dictionary<string, ISchemaNode> _schemasByMount = new();

        /// <summary>
        /// Loads schema classes from all specified assemblies and registers them by mount path.
        /// </summary>
        /// <param name="assemblyPaths">A list of DLL or EXE files to inspect.</param>
        public void LoadSchemasFromAssemblies(IEnumerable<string> assemblyPaths)
        {
            foreach (var path in assemblyPaths)
            {
                var asm = Assembly.LoadFrom(path);
                foreach (var type in asm.GetTypes())
                {
                    var attr = type.GetCustomAttribute<ConfigSchemaAttribute>();
                    if (attr != null && typeof(ISchemaNode).IsAssignableFrom(type))
                    {
                        var instance = (ISchemaNode)Activator.CreateInstance(type)!;
                        _schemasByMount[attr.MountPath] = instance;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the root schema node for a given mount path.
        /// </summary>
        /// <param name="mountPath">The mount path to look up.</param>
        /// <returns>The root schema node, or null if not found.</returns>
        public ISchemaNode? GetSchemaForMount(string mountPath)
        {
            return _schemasByMount.TryGetValue(mountPath, out var schema) ? schema : null;
        }
    }

    /// <summary>
    /// Marker attribute to declare a schema class and associate it with a mount path.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigSchemaAttribute : Attribute
    {
        public string MountPath { get; }
        public ConfigSchemaAttribute(string mountPath)
        {
            MountPath = mountPath;
        }
    }

}

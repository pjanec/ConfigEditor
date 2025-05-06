using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ConfigEditor.Schema;

namespace ConfigEditor.SchemaRuntime
{
    public class RuntimeSchemaCatalog
    {
        public Dictionary<string, ISchemaNode> Schemas { get; } = new();

        public void LoadFromAssemblies(IEnumerable<Assembly> assemblies)
        {
            foreach (var asm in assemblies)
            {
                foreach (var type in asm.GetTypes())
                {
                    if (!type.IsClass || type.IsAbstract) continue;
                    if (!type.GetCustomAttributes(typeof(ConfigSchemaAttribute), inherit: false).Any()) continue;

                    var instance = Activator.CreateInstance(type);
                    if (instance is ISchemaNode rootSchema)
                    {
                        var mountAttr = type.GetCustomAttribute<ConfigSchemaAttribute>();
                        var mountPath = mountAttr?.MountPath ?? "";
                        Schemas[mountPath] = rootSchema;
                    }
                }
            }
        }
    }

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

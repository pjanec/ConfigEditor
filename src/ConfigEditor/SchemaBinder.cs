using System;
using System.Linq;
using System.Reflection;

namespace ConfigDom;

public static class SchemaBinder
{
    public static SchemaNode FromType(Type t)
    {
        if (t == typeof(int))
        {
            return new LeafSchemaNode();
        }
        if (t == typeof(string))
        {
            return new LeafSchemaNode();
        }
        if (t.IsEnum)
        {
            return new LeafSchemaNode
            {
                AllowedValues = Enum.GetNames(t).ToList()
            };
        }

        if (t.IsClass)
        {
            var obj = new ObjectSchemaNode();
            var instance = Activator.CreateInstance(t);

            foreach (var prop in t.GetProperties())
            {
                var schema = FromType(prop.PropertyType);

                // Range, format, pattern, etc.
                if (schema is LeafSchemaNode leaf)
                {
                    var rangeAttr = prop.GetCustomAttribute<RangeAttribute>();
                    if (rangeAttr != null)
                    {
                        leaf.Min = rangeAttr.Min;
                        leaf.Max = rangeAttr.Max;
                    }

                    var formatAttr = prop.GetCustomAttribute<FormatAttribute>();
                    if (formatAttr != null)
                    {
                        leaf.Format = formatAttr.Format;
                    }

                    var patternAttr = prop.GetCustomAttribute<PatternAttribute>();
                    if (patternAttr != null)
                    {
                        leaf.RegexPattern = patternAttr.Regex;
                    }

                    if (prop.PropertyType.IsEnum)
                    {
                        leaf.AllowedValues = Enum.GetNames(prop.PropertyType).ToList();
                    }
                }

                bool isRequired =
                    prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) == null ||
                    prop.GetCustomAttribute<RequiredAttribute>() != null;

                object? defaultValue = prop.GetCustomAttribute<DefaultValueAttribute>()?.Value;
                if (defaultValue == null && instance != null)
                {
                    defaultValue = prop.GetValue(instance);
                }

                var unit = prop.GetCustomAttribute<UnitAttribute>()?.Unit;
                var desc = prop.GetCustomAttribute<DescriptionAttribute>()?.Text;

                obj.Properties[prop.Name] = new SchemaProperty
                {
                    Schema = schema,
                    IsRequired = isRequired,
                    DefaultValue = defaultValue,
                    Unit = unit,
                    Description = desc
                };
            }
            return obj;
        }

        throw new NotSupportedException();
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class RequiredAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public sealed class DefaultValueAttribute : Attribute
{
    public object? Value { get; }
    public DefaultValueAttribute(object? value) => Value = value;
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class UnitAttribute : Attribute
{
    public string Unit { get; }
    public UnitAttribute(string unit) => Unit = unit;
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class DescriptionAttribute : Attribute
{
    public string Text { get; }
    public DescriptionAttribute(string text) => Text = text;
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class RangeAttribute : Attribute
{
    public int Min { get; }
    public int Max { get; }
    public RangeAttribute(int min, int max)
    {
        Min = min;
        Max = max;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class FormatAttribute : Attribute
{
    public string Format { get; }
    public FormatAttribute(string format) => Format = format;
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class PatternAttribute : Attribute
{
    public string Regex { get; }
    public PatternAttribute(string regex) => Regex = regex;
}

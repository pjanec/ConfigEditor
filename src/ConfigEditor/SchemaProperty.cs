using ConfigDom;

public sealed class SchemaProperty
{
    public required SchemaNode Schema { get; init; }

    // Field-level metadata
    public bool IsRequired { get; init; } = false;
    public object? DefaultValue { get; init; }

    // UI and documentation metadata
    public string? Unit { get; init; }
    public string? Description { get; init; }
}

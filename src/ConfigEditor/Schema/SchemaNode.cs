using System.Collections.Generic;

namespace ConfigEditor.Schema;

/// <summary>
/// Represents the base type for all schema node types.
/// Provides shared validation and UI metadata across leaf, object, and array nodes.
/// </summary>
public abstract class SchemaNode
{
	/// <summary>
	/// Optional minimum value constraint.
	/// Applicable to numeric leaf nodes.
	/// </summary>
	public int? Min { get; set; }

	/// <summary>
	/// Optional maximum value constraint.
	/// Applicable to numeric leaf nodes.
	/// </summary>
	public int? Max { get; set; }

	/// <summary>
	/// Optional formatting hint for the field.
	/// Applies primarily to string values, e.g. "hostname", "ipv4", "email".
	/// </summary>
	public string? Format { get; set; }

	/// <summary>
	/// List of allowed literal values.
	/// Used for enum validation or fixed string sets.
	/// </summary>
	public List<string>? AllowedValues { get; set; }
}

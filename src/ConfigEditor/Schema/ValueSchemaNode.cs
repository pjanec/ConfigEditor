using System;
using System.Collections.Generic;

namespace ConfigEditor.Schema
{
	/// <summary>
	/// Represents the schema for a leaf node (value node).
	/// Includes optional validation and UI metadata.
	/// </summary>
	public class ValueSchemaNode : SchemaNode
	{
		/// <summary>
		/// Optional regular expression that the value must match (string only).
		/// </summary>
		public string? RegexPattern { get; set; }
	}
}

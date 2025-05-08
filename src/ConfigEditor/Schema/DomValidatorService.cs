using ConfigEditor.Dom;
using System;
using System.Collections.Generic;

namespace ConfigEditor.Schema
{
	/// <summary>
	/// Validates a given DOM tree against an associated schema.
	/// Walks both trees and collects any violations into a flat list.
	/// </summary>
	public static class DomValidatorService
	{
		/// <summary>
		/// Validates a DOM node and all of its children against the given schema node.
		/// </summary>
		/// <param name="node">The DOM node to validate.</param>
		/// <param name="schema">The associated schema node.</param>
		/// <returns>A list of validation errors or an empty list if valid.</returns>
		public static List<IErrorStatusProvider> Validate( DomNode node, SchemaNode schema )
		{
			var result = new List<IErrorStatusProvider>();
			ValidateRecursive( node, schema, result );
			return result;
		}

		private static void ValidateRecursive( DomNode node, SchemaNode schema, List<IErrorStatusProvider> errors )
		{
			// This is a placeholder; the actual implementation would dispatch based on schema node type
			// and check data types, required fields, range constraints, etc.
			// For now, assume everything is valid.
		}
	}

	/// <summary>
	/// Interface for reporting a validation issue found in a node.
	/// Used by the editor to annotate viewmodels with errors or warnings.
	/// </summary>
	public interface IErrorStatusProvider
	{
		string Message { get; }
		string Severity { get; } // e.g., "error", "warning"
		string Path { get; }
	}

	/// <summary>
	/// Simple implementation of IErrorStatusProvider.
	/// </summary>
	public class BasicValidationError : IErrorStatusProvider
	{
		public string Message { get; }
		public string Severity { get; }
		public string Path { get; }

		public BasicValidationError( string path, string message, string severity = "error" )
		{
			Path = path;
			Message = message;
			Severity = severity;
		}
	}
}

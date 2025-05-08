using ConfigEditor.Dom;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ConfigEditor.Schema
{
	public static class SchemaValidator
	{
		public static List<DomValidationError> ValidateTree( DomNode node, SchemaNode schema, string path = "", DomNode? domRoot = null )
		{
			var errors = new List<DomValidationError>();

			if( schema is ObjectSchemaNode objSchema && node is ObjectNode objNode )
			{
				foreach( var (fieldName, schemaProp) in objSchema.Properties )
				{
					if( schemaProp.IsRequired && !objNode.Children.ContainsKey( fieldName ) )
					{
						errors.Add( new DomValidationError
						{
							Path = path + "/" + fieldName,
							Message = "Required field missing"
						} );
					}
				}

				foreach( var (childKey, childNode) in objNode.Children )
				{
					var childPath = path + "/" + childKey;
					if( objSchema.Properties.TryGetValue( childKey, out var childSchemaProp ) )
					{
						errors.AddRange( ValidateTree( childNode, childSchemaProp.Schema, childPath, domRoot ) );
					}
					else
					{
						errors.Add( new DomValidationError
						{
							Path = childPath,
							Message = "Unexpected field"
						} );
					}
				}
			}
			else if( schema is ArraySchemaNode arrSchema && node is ArrayNode arrNode )
			{
				for( int i = 0; i < arrNode.Items.Count; i++ )
				{
					var item = arrNode.Items[i];
					var itemPath = path + "/" + i;
					errors.AddRange( ValidateTree( item, arrSchema.ItemSchema, itemPath, domRoot ) );
				}
			}
			else if( node is ValueNode leaf )
			{
				var json = leaf.Value;

				if( json.ValueKind == JsonValueKind.Number && (schema.Min != null || schema.Max != null) )
				{
					if( json.TryGetDouble( out var val ) )
					{
						if( schema.Min != null && val < schema.Min )
						{
							errors.Add( new DomValidationError { Path = path, Message = "Value below minimum" } );
						}
						if( schema.Max != null && val > schema.Max )
						{
							errors.Add( new DomValidationError { Path = path, Message = "Value above maximum" } );
						}
					}
				}

				if( schema is ValueSchemaNode leafSchema )
				{
					if( leafSchema.AllowedValues != null &&
						!leafSchema.AllowedValues.Contains( json.ToString() ) )
					{
						errors.Add( new DomValidationError
						{
							Path = path,
							Message = $"Value '{json.ToString()}' is not in allowed set"
						} );
					}

					if( leafSchema.RegexPattern != null &&
						json.ValueKind == JsonValueKind.String &&
						!Regex.IsMatch( json.GetString() ?? "", leafSchema.RegexPattern ) )
					{
						errors.Add( new DomValidationError
						{
							Path = path,
							Message = $"Value does not match pattern: {leafSchema.RegexPattern}"
						} );
					}
				}
			}
			else if( node is RefNode refNode )
			{
				if( domRoot == null )
				{
					errors.Add( new DomValidationError
					{
						Path = path,
						Message = "Cannot resolve $ref — root not provided"
					} );
				}
				else
				{
					var target = RefNodeResolver.Resolve( refNode, domRoot );
					if( target == null )
					{
						errors.Add( new DomValidationError
						{
							Path = path,
							Message = "Invalid or unresolved $ref"
						} );
					}
					else if( schema != null )
					{
						errors.AddRange( ValidateTree( target, schema, path + " (resolved)", domRoot ) );
					}
				}
			}

			return errors;
		}
	}

	public class DomValidationError
	{
		public string Path { get; init; } = "";
		public string Message { get; init; } = "";
		public string? SourceFile { get; init; }
		public int? CascadeLevel { get; init; }
	}
}

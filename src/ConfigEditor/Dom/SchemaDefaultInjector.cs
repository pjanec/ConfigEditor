using ConfigEditor.Schema;
using System;
using System.Text.Json;

namespace ConfigEditor.Dom;

/// <summary>
/// Inserts missing default-valued fields into the DOM tree prior to export.
/// </summary>
public static class SchemaDefaultInjector
{
	public static void ApplyDefaults( DomNode node, SchemaNode schema )
	{
		if( node is ObjectNode objNode && schema is ObjectSchemaNode objSchema )
		{
			foreach( var (key, prop) in objSchema.Properties )
			{
				if( !objNode.Children.ContainsKey( key ) )
				{
					if( prop.DefaultValue != null )
					{
						var json = JsonSerializer.SerializeToElement( prop.DefaultValue );
						objNode.AddChild( new ValueNode( key, json, objNode ) );
					}
					else if( prop.IsRequired )
					{
						throw new InvalidOperationException( $"Missing required field: {key}" );
					}
				}
				else
				{
					ApplyDefaults( objNode.Children[key], prop.Schema );
				}
			}
		}
		else if( node is ArrayNode arrNode && schema is ArraySchemaNode arrSchema )
		{
			foreach( var item in arrNode.Items )
			{
				ApplyDefaults( item, arrSchema.ItemSchema );
			}
		}
	}
}

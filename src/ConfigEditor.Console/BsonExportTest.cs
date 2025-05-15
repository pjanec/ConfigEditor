using ConfigEditor.Dom;
using ConfigEditor.IO;
using ConfigEditor.Schema;
using System.Text.Json;

namespace ConfigEditor;

public static class BsonExportTest
{
    public static void Run()
    {
        // Build a sample schema
        var schema = new ObjectSchemaNode
        {
            Properties = new()
            {
                ["env"] = new SchemaProperty
                {
                    Schema = new ObjectSchemaNode
                    {
                        Properties = new()
                        {
                            ["ip"] = new SchemaProperty
                            {
                                Schema = new ValueSchemaNode(),
                                IsRequired = true
                            },
                            ["port"] = new SchemaProperty
                            {
                                Schema = new ValueSchemaNode(),
                                DefaultValue = 8080
                            },
                            ["host"] = new SchemaProperty
                            {
                                Schema = new ValueSchemaNode(),
                                DefaultValue = "localhost"
                            }
                        }
                    },
                    IsRequired = true
                }
            }
        };

        // Build a sample DOM
        var root = new ObjectNode("root");
        var env = new ObjectNode("env");
        root.AddChild(env);
        env.AddChild(new ValueNode("ip", JsonSerializer.SerializeToElement("192.168.0.1"), env));
        env.AddChild(new RefNode("host", "shared/defaultHost", env));

        var shared = new ObjectNode("shared");
        root.AddChild(shared);
        shared.AddChild(new ValueNode("defaultHost", JsonSerializer.SerializeToElement("ref.example.com"), shared));

        // Export to BSON
        BsonExporter.ExportToBsonFile(root, schema, "config-out.bson");

        Console.WriteLine("BSON export completed.");
    }
}

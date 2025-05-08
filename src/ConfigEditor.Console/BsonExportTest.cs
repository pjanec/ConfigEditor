using System.Text.Json;

namespace ConfigDom;

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
                                Schema = new LeafSchemaNode(),
                                IsRequired = true
                            },
                            ["port"] = new SchemaProperty
                            {
                                Schema = new LeafSchemaNode(),
                                DefaultValue = 8080
                            },
                            ["host"] = new SchemaProperty
                            {
                                Schema = new LeafSchemaNode(),
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
        var env = new ObjectNode("env", root);
        root.Children["env"] = env;
        env.Children["ip"] = new LeafNode("ip", JsonSerializer.SerializeToElement("192.168.0.1"), env);
        env.Children["host"] = new RefNode("host", "shared/defaultHost", env);

        var shared = new ObjectNode("shared", root);
        root.Children["shared"] = shared;
        shared.Children["defaultHost"] = new LeafNode("defaultHost", JsonSerializer.SerializeToElement("ref.example.com"), shared);

        // Export to BSON
        BsonExporter.ExportToBsonFile(root, schema, "config-out.bson");

        Console.WriteLine("BSON export completed.");
    }
}

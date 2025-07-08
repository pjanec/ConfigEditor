using RuntimeConfig.Core;
using RuntimeConfig.Core.Models;
using RuntimeConfig.Core.Providers;
using RuntimeConfig.Core.Querying;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Use the test data in the output config folder
        var testDataPath = Path.Combine(AppContext.BaseDirectory, "config");

        var layers = new List<LayerDefinition>
        {
            new LayerDefinition("Base", Path.Combine(testDataPath, "1_base")),
            new LayerDefinition("Staging", Path.Combine(testDataPath, "2_staging")),
            new LayerDefinition("Production", Path.Combine(testDataPath, "3_production"))
        };

        var provider = new CascadingJsonProvider(layers);
        var configTree = new RuntimeDomTree();
        configTree.RegisterProvider("app", provider);

        Console.WriteLine("Loading and resolving configuration...");
        await configTree.RefreshAsync();
        Console.WriteLine("Load complete.");

        // Debug: Let's see what the actual DOM structure looks like
        Console.WriteLine("\n=== DOM Structure Debug ===");
        DumpDomStructure(configTree.ResolvedRoot, 0);

        DomQuery query = configTree.Query();

        // Test various queries - using the actual data that exists
        try
        {
            string appName = query.Get<string>("app/ApplicationName");
            Console.WriteLine($"Application Name: {appName}"); // Expected: My Awesome App
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting ApplicationName: {ex.Message}");
        }

        try
        {
            int port = query.Get<int>("app/Port");
            Console.WriteLine($"Port: {port}"); // Expected: 8080
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting Port: {ex.Message}");
        }

        try
        {
            bool enableLogging = query.Get<bool>("app/EnableLogging");
            Console.WriteLine($"Enable Logging: {enableLogging}"); // Expected: false
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting EnableLogging: {ex.Message}");
        }

        try
        {
            string apiKey = query.Get<string>("app/secrets/ApiKey");
            Console.WriteLine($"API Key: {apiKey}"); // Expected: STAGING_API_KEY
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting ApiKey: {ex.Message}");
        }
    }

    private static void DumpDomStructure(RuntimeConfig.Core.Dom.DomNode? node, int depth)
    {
        if (node == null) return;

        var indent = new string(' ', depth * 2);
        Console.WriteLine($"{indent}{node.GetType().Name}: {node.Name} (Path: {node.Path})");

        if (node is RuntimeConfig.Core.Dom.ObjectNode obj)
        {
            foreach (var child in obj.Children.Values)
            {
                DumpDomStructure(child, depth + 1);
            }
        }
        else if (node is RuntimeConfig.Core.Dom.ArrayNode arr)
        {
            for (int i = 0; i < arr.Items.Count; i++)
            {
                DumpDomStructure(arr.Items[i], depth + 1);
            }
        }
        else if (node is RuntimeConfig.Core.Dom.ValueNode val)
        {
            Console.WriteLine($"{indent}  Value: {val.Value}");
        }
    }
}

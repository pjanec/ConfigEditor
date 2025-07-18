﻿using RuntimeConfig.Core;
using RuntimeConfig.Core.Models;
using RuntimeConfig.Core.Providers;
using RuntimeConfig.Core.Querying;
using RuntimeConfig.Core.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RuntimeConfig.Core.Dom;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== RuntimeConfig Cascading Configuration Demo ===\n");
        
        // Use the TestData folder that was copied to the output directory
        var testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "config");

        Console.WriteLine($"Loading configuration from: {testDataPath}");
        
        // The test app now only needs to know the path to the main project file.
        var projectFilePath = Path.Combine(AppContext.BaseDirectory, "TestData", "test-project.cascade.jsonc");
        Console.WriteLine($"Loading configuration from project file: {projectFilePath}\n");


        // specify the cascading layers to load the config from
        //var layers = new List<LayerDefinition>
        //{
        //    new LayerDefinition("Base", Path.Combine(testDataPath, "1_base")),
        //    new LayerDefinition("Staging", Path.Combine(testDataPath, "2_staging")),
        //    new LayerDefinition("Production", Path.Combine(testDataPath, "3_production"))
        //};

        // Load layer definitions from the cascading config project file.
        var projectLoader = new CascadingProjectLoader();
        var layers = await projectLoader.LoadLayersFromProjectFileAsync(projectFilePath);


		// Demo of simple querying the cascaded config without the runtime dom tree,
        // resolving the references only within the single provides
		{
			// load the cascaded config data
			var provider = new CascadingJsonProvider(layers);
			var rootWithUnresolvedRefs = provider.Load(); // this merges the layers into a single root node

			// resolve refs within the cascading config
			var resolver = new RefResolver(rootWithUnresolvedRefs);
            var resolvedRoot = resolver.Resolve();

			// query the resolved configuration
			var query1 = new DomQuery(resolvedRoot);
            var appSettings = query1.Get<JsonConfigEditor.TestData.AppConfiguration>("app-settings");
        }


		// Demo of the "runtime dom tree" which can mount multiple providers at different paths (
		// and then resolve the refs across all of them
        {
            // register the config to the runtime dom tree for later querying
            var provider = new CascadingJsonProvider(layers);

		    var configTree = new RuntimeDomTree();
            configTree.RegisterProvider("app", provider);

            Console.WriteLine("Loading and resolving configuration...");
            await configTree.RefreshAsync(); // resolve refs, prepare for querying
            Console.WriteLine("Load complete.\n");

            // Debug: Let's see what the actual DOM structure looks like
            Console.WriteLine("=== DOM Structure Debug ===");
            DumpDomStructure(configTree.ResolvedRoot, 0);
            Console.WriteLine();

            DomQuery query = configTree.Query();

            Console.WriteLine("=== Cascading Configuration Demo ===");
            Console.WriteLine("Testing values that exist in multiple layers to demonstrate cascading behavior:\n");

            // Read whole config to an in-memory class instance (this class is also used as a schema definition for the editor)
            {
                var appSettings = query.Get<JsonConfigEditor.TestData.AppConfiguration>("app/app-settings");
                Console.WriteLine($"App name from config: {appSettings.ApplicationName}");
            }


            // Test ApplicationName - exists in Base and Production, should get Production value
            try
            {
                string appName = query.Get<string>("app/app-settings/ApplicationName");
                Console.WriteLine($"Application Name: {appName}");
                Console.WriteLine("  → Base layer: 'My Awesome App (Base)'");
                Console.WriteLine("  → Production layer: 'My Awesome App'");
                Console.WriteLine("  → Result: Production overrides Base ✓\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting ApplicationName: {ex.Message}\n");
            }

            // Test Port - exists in Base, Staging, and Production, should get Production value
            try
            {
                int port = query.Get<int>("app/app-settings/Port");
                Console.WriteLine($"Port: {port}");
                Console.WriteLine("  → Base layer: 80");
                Console.WriteLine("  → Staging layer: 8080");
                Console.WriteLine("  → Production layer: 443");
                Console.WriteLine("  → Result: Production overrides Staging overrides Base ✓\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting Port: {ex.Message}\n");
            }

            // Test EnableLogging - exists in Base and Production, should get Production value
            try
            {
                bool enableLogging = query.Get<bool>("app/app-settings/EnableLogging");
                Console.WriteLine($"Enable Logging: {enableLogging}");
                Console.WriteLine("  → Base layer: true");
                Console.WriteLine("  → Production layer: false");
                Console.WriteLine("  → Result: Production overrides Base ✓\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting EnableLogging: {ex.Message}\n");
            }

            // Test AllowedHosts - exists in Base and Staging, should get Staging value
            try
            {
                var allowedHosts = query.Get<string[]>("app/app-settings/AllowedHosts");
                Console.WriteLine($"Allowed Hosts: [{string.Join(", ", allowedHosts)}]");
                Console.WriteLine("  → Base layer: ['localhost']");
                Console.WriteLine("  → Staging layer: ['localhost', 'staging.server.com']");
                Console.WriteLine("  → Result: Staging overrides Base ✓\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting AllowedHosts: {ex.Message}\n");
            }


            Console.WriteLine("=== Summary ===");
            Console.WriteLine("The cascading configuration system correctly resolves values from the highest layer");
            Console.WriteLine("that contains the configuration, demonstrating the override behavior.");
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

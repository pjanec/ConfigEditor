using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ConfigDom.TestScaffold
{
    public static class CascadeEditorTestScaffold
    {
        public static void Run()
        {
            Console.WriteLine("=== Testing Json5CascadeEditorContext ===");

            // Simulate 3 cascade levels with overlapping keys
            var baseJson = """{ "hostname": "node01", "ip": "192.168.1.1", "region": "eu" }""";
            var siteJson = """{ "ip": "192.168.1.20" }""";
            var localJson = """{ "region": "us", "debug": true }""";

            var baseFile = new Json5SourceFile("base.json", "base.json",
                JsonDomBuilder.BuildFromJsonElement("base", JsonDocument.Parse(baseJson).RootElement),
                baseJson);

            var siteFile = new Json5SourceFile("site.json", "site.json",
                JsonDomBuilder.BuildFromJsonElement("site", JsonDocument.Parse(siteJson).RootElement),
                siteJson);

            var localFile = new Json5SourceFile("local.json", "local.json",
                JsonDomBuilder.BuildFromJsonElement("local", JsonDocument.Parse(localJson).RootElement),
                localJson);

            var sources = new List<Json5SourceFile> { baseFile, siteFile, localFile };

            var layers = new List<CascadeLayer>
			{
				new CascadeLayer("Base", new List<Json5SourceFile> { baseFile }),
				new CascadeLayer("Site", new List<Json5SourceFile> { siteFile }),
				new CascadeLayer("Local", new List<Json5SourceFile> { localFile })
			};

			var mergeOriginTracker = new MergeOriginTracker();
			var mergedRoot = JsonMergeService.MergeCascade( layers, mergeOriginTracker );

            var context = new Json5CascadeEditorContext("config/test", layers);

            void DumpValue(string path)
            {
                var val = ExportJsonSubtree.Get(context.GetRoot(), path);
                Console.WriteLine($"{path} = {val}");
            }

            DumpValue("config/test/hostname"); // base
            DumpValue("config/test/ip");       // overridden by site
            DumpValue("config/test/region");   // overridden by local
            DumpValue("config/test/debug");    // added in local
        }
    }
}

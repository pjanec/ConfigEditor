using ConfigEditor.Dom;
using ConfigEditor.EditCtx;
using ConfigEditor.History;
using ConfigEditor.IO;
using System;
using System.Text.Json;

namespace ConfigEditor.TestScaffold
{
	public static class DomEditorTestScaffold
    {
        public static void Run()
        {
            Console.WriteLine("=== Testing FlatJsonEditorContext ===");

            var testJson = """
            {
                "system": {
                    "hostname": "node01",
                    "ip": "192.168.1.100"
                },
                "enabled": true
            }
            """;

            var root = JsonDomBuilder.BuildFromJsonElement("root", JsonDocument.Parse(testJson).RootElement);
            var file = new SourceFile("mock.json", "mock.json", root, testJson);
            var context = new FlatJsonEditorContext("config/test", file);

            var before = ExportJsonSubtree.Get(context.GetRoot(), "config/test/system/hostname");
            Console.WriteLine("Original hostname: " + before);

            // Use helper to create an edit with automatic snapshot
            var edit = DomEditActionFactory.CreateWithSnapshot(
                context.GetRoot(),
                "config/test/system/hostname",
                JsonDocument.Parse("\"node02\"").RootElement
            );

            context.ApplyEdit(edit);
            var after = ExportJsonSubtree.Get(context.GetRoot(), "config/test/system/hostname");
            Console.WriteLine("Edited hostname: " + after);

            context.Undo();
            var reverted = ExportJsonSubtree.Get(context.GetRoot(), "config/test/system/hostname");
            Console.WriteLine("After Undo: " + reverted);

            context.Redo();
            var redone = ExportJsonSubtree.Get(context.GetRoot(), "config/test/system/hostname");
            Console.WriteLine("After Redo: " + redone);
        }
    }
}

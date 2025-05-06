using ConfigEditor.Context;
using ConfigEditor.Util;
using ConfigEditor.Validation;
using System.Reflection;

// Set up paths (mocked for now)
var cascadeFolders = new List<string> { "config/base", "config/override" };
var mountPath = "exampleConfig";

// Services
var merger = new JsonMergeService();
var flattener = new DomFlatteningService();
var context = new Json5CascadeEditorContext(mountPath, cascadeFolders, merger, flattener);

// Optionally validate
var validator = new DomValidatorService();
var errors = validator.ValidateTree(context.Root);

foreach (var error in errors)
{
    Console.WriteLine(error);
}

// Save changes
if (context.IsDirty)
{
    context.Save();
    Console.WriteLine("Changes saved.");
}

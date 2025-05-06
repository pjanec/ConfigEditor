using System;
using System.IO;

namespace ConfigEditor.Util
{
    public static class Json5SourceFileLoader
    {
        public static Json5SourceFile? Load(string filePath)
        {
            try
            {
                var text = File.ReadAllText(filePath);
                return new Json5SourceFile(filePath, text);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load JSON5 file '{filePath}': {ex.Message}");
                return null;
            }
        }
    }
}

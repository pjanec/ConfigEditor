using System.Collections.Generic;  
using System.Text.Json.Serialization;  
using JsonConfigEditor.Core.Services; // For IntegrityCheckType

namespace JsonConfigEditor.Core.Settings  
{  
    // Represents the entire settings.json file  
    public class UserSettingsModel  
    {  
        [JsonPropertyName("window")]  
        public WindowSettings Window { get; set; } = new();

        [JsonPropertyName("dataGrid")]  
        public DataGridSettings DataGrid { get; set; } = new();

        [JsonPropertyName("integrityChecks")]  
        public IntegrityCheckSettings IntegrityChecks { get; set; } = new();  
    }

    // Represents window layout settings  
    public class WindowSettings  
    {  
        [JsonPropertyName("height")]  
        public double Height { get; set; } = 700; // Default height

        [JsonPropertyName("width")]  
        public double Width { get; set; } = 1100; // Default width  
    }

    // Represents DataGrid layout settings  
    public class DataGridSettings  
    {  
        // Key: The Header of the column. Value: The stored width.  
        [JsonPropertyName("columnWidths")]  
        public Dictionary<string, double> ColumnWidths { get; set; } = new();  
    }

    // Represents the user's choices for which integrity checks to run  
    public class IntegrityCheckSettings  
    {  
        [JsonPropertyName("checksToRun")]  
        public IntegrityCheckType ChecksToRun { get; set; } = IntegrityCheckType.All; // Default to all checks enabled  
    }  
} 
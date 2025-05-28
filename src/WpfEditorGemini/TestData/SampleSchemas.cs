using JsonConfigEditor.Contracts.Attributes;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace JsonConfigEditor.TestData
{
    /// <summary>
    /// Sample schema class for testing the root configuration.
    /// </summary>
    [ConfigSchema("", typeof(AppConfiguration))]
    public class AppConfiguration
    {
        public string ApplicationName { get; set; } = "Default App";
        
        [Range(1, 65535)]
        public int Port { get; set; } = 8080;
        
        public bool EnableLogging { get; set; } = true;
        
        public DatabaseSettings Database { get; set; } = new();
        
        public List<string> AllowedHosts { get; set; } = new() { "localhost" };

        public FeatureConfiguration features  { get; set; }     = new();
	}

    /// <summary>
    /// Sample schema class for database configuration.
    /// </summary>
    public class DatabaseSettings
    {
        [SchemaAllowedValues("SqlServer", "PostgreSQL", "MySQL")]
        public string Provider { get; set; } = "SqlServer";
        
        public string ConnectionString { get; set; } = "";
        
        [Range(1, 3600)]
        public int TimeoutSeconds { get; set; } = 30;
        
        [ReadOnly(true)]
        public string Version { get; set; } = "1.0";
    }

    /// <summary>
    /// Sample schema for a nested configuration section.
    /// </summary>
    //[ConfigSchema("features", typeof(FeatureConfiguration))]
    public class FeatureConfiguration
    {
        public bool EnableFeatureA { get; set; } = false;
        
        public bool EnableFeatureB { get; set; } = true;
        
        public AdvancedSettings Advanced { get; set; } = new();
    }

    /// <summary>
    /// Sample schema for advanced settings.
    /// </summary>
    public class AdvancedSettings
    {
        [SchemaRegexPattern(@"^[A-Z]{2,4}$")]
        public string CountryCode { get; set; } = "US";
        
        public Dictionary<string, string> CustomSettings { get; set; } = new();
        
        public LogLevel LogLevel { get; set; } = LogLevel.Information;
    }

    /// <summary>
    /// Sample enum for testing enum schema generation.
    /// </summary>
    public enum LogLevel
    {
        Trace,
        Debug,
        Information,
        Warning,
        Error,
        Critical
    }
} 
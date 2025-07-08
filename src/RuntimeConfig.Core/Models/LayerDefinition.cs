namespace RuntimeConfig.Core.Models
{
    /// <summary>
    /// Represents the definition of a single configuration layer.
    /// </summary>
    /// <param name="Name">The user-friendly name of the layer (e.g., "Base", "Production").</param>
    /// <param name="BasePath">The absolute path to the layer's root directory.</param>
    public record LayerDefinition(string Name, string BasePath);
} 
using System.Collections.Generic;

namespace ConfigDom;

/// <summary>
/// Represents a single layer in the configuration cascade.
/// Each layer has a name (for UI display) and a list of source files.
/// </summary>
public record CascadeLayer(string Name, List<Json5SourceFile> Files);

using ConfigEditor.Dom;

/// <summary>
/// Provides access to the fully merged configuration DOM tree at runtime.
/// Implementations typically return a root node representing the result of merging
/// all cascade layers into a single DOM structure. This interface is intended for
/// read-only runtime use and does not expose any editing or validation functionality.
/// </summary>
public interface IRuntimeDomProvider
{
    string Name { get; }              // Optional but helpful
    DomNode GetRoot();               // Required to mount the subtree
    void Refresh();                  // Required for polling/updating dynamic content
}

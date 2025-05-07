using ConfigDom;

public interface IRuntimeDomProvider
{
    string Name { get; }              // Optional but helpful
    DomNode GetRoot();               // Required to mount the subtree
    void Refresh();                  // Required for polling/updating dynamic content
}

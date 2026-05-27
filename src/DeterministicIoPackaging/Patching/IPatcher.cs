interface IPatcher
{
    public void PatchXml(XDocument xml, string entryName);
    public bool IsMatch(Entry entry);
}

// Patchers that match a single fixed FullName can implement this so the
// dispatcher can route them via a dictionary lookup rather than calling
// IsMatch on every patcher for every entry.
interface IExactMatchPatcher : IPatcher
{
    string ExactMatch { get; }
}

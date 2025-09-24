class CorePatcher :
    IPatcher
{
    static XNamespace dc = "http://purl.org/dc/elements/1.1/";
    static XName creator = dc + "creator";
    static XNamespace cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    static XName lastModifiedBy = cp + "lastModifiedBy";
    static XNamespace dcterms = "http://purl.org/dc/terms/";
    static XName created = dcterms + "created";
    static XName modified = dcterms + "modified";

    public void PatchXml(XDocument xml)
    {
        var root = xml.Root!;
        root.Element(creator)?.Remove();
        root.Element(lastModifiedBy)?.Remove();
        root.Element(created)?.Remove();
        root.Element(modified)?.Remove();
    }

    public bool IsMatch(Entry entry) =>
        entry.FullName == "docProps/core.xml";
}
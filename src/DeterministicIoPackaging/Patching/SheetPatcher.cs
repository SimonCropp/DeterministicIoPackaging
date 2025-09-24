class SheetPatcher: IPatcher
{
    static XNamespace xr = "http://schemas.microsoft.com/office/spreadsheetml/2014/revision";
    static XName xName = xr + "uid";

    public void PatchXml(XDocument xml) =>
        xml.Root!.Attribute(xName)?.Remove();

    public bool IsMatch(Entry entry)
    {
        var name = entry.FullName;
        return name.StartsWith("xl/worksheets/") &&
               name.EndsWith(".xml");
    }
}
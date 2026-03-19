class SheetPatcher: IPatcher
{
    static XNamespace xr = "http://schemas.microsoft.com/office/spreadsheetml/2014/revision";
    static XName xName = xr + "uid";

    public bool PatchXml(XDocument xml, string entryName)
    {
        var attr = xml.Root!.Attribute(xName);
        if (attr == null)
        {
            return false;
        }

        attr.Remove();
        return true;
    }

    public bool IsMatch(Entry entry)
    {
        var name = entry.FullName;
        return name.StartsWith("xl/worksheets/") &&
               name.EndsWith(".xml");
    }
}

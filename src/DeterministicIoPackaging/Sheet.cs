static class Sheet
{
    static XDocument PatchSheet(Stream sourceStream)
    {
        var xml = XDocument.Load(sourceStream);
        return PatchSheet(xml);
    }

    internal static XDocument PatchSheet(XDocument xml)
    {
        XNamespace xr = "http://schemas.microsoft.com/office/spreadsheetml/2014/revision";
        xml.Root!.Attribute(xr + "uid")?.Remove();
        return xml;
    }

    public static bool IsWorksheetXml(this Entry entry)
    {
        var name = entry.FullName;
        return name.StartsWith("xl/worksheets/") &&
               name.EndsWith(".xml");
    }
}
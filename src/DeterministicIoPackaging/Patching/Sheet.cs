static class Sheet
{
    internal static XDocument Patch(Stream stream)
    {
        var xml = XDocument.Load(stream);
        Patch(xml);
        return xml;
    }

    public static async Task<XDocument> Patch(Stream stream, Cancel cancel)
    {
        var xml = await XDocument.LoadAsync(stream, LoadOptions.None, cancel);
        Patch(xml);
        return xml;
    }

    static XNamespace xr = "http://schemas.microsoft.com/office/spreadsheetml/2014/revision";
    static XName xName = xr + "uid";

    static void Patch(XDocument xml) =>
        xml.Root!.Attribute(xName)?.Remove();

    public static bool IsWorksheetXml(this Entry entry)
    {
        var name = entry.FullName;
        return name.StartsWith("xl/worksheets/") &&
               name.EndsWith(".xml");
    }
}
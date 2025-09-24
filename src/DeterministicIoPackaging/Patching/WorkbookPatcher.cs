static class WorkbookPatcher
{
    static XNamespace mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    static XName alternateContent = mc + "AlternateContent";
    static XNamespace x15ac = "http://schemas.microsoft.com/office/spreadsheetml/2010/11/ac";
    static XName abspath = x15ac + "absPath";

    public static XDocument Patch(Stream stream)
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

    static void Patch(XDocument xml)
    {
        var absPath = xml
            .Descendants(alternateContent)
            .FirstOrDefault(_ => _.Descendants(abspath).Any());

        absPath?.Remove();
    }

    public static bool IsWorkbookXml(this Entry entry) =>
        entry.FullName == "xl/workbook.xml";
}
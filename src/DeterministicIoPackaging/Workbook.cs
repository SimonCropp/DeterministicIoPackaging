static class Workbook
{
    static XNamespace mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    static XNamespace x15ac = "http://schemas.microsoft.com/office/spreadsheetml/2010/11/ac";

    public static XDocument Patch(Stream sourceStream)
    {
        var xml = XDocument.Load(sourceStream);

        var absPath = xml
            .Descendants(mc + "AlternateContent")
            .FirstOrDefault(_ => _.Descendants(x15ac + "absPath").Any());

        absPath?.Remove();

        return xml;
    }

    public static bool IsWorkbookXml(this Entry entry) =>
        entry.FullName == "xl/workbook.xml";
}
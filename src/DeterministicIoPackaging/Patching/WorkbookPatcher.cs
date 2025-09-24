class WorkbookPatcher : IPatcher
{
    static XNamespace mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    static XName alternateContent = mc + "AlternateContent";
    static XNamespace x15ac = "http://schemas.microsoft.com/office/spreadsheetml/2010/11/ac";
    static XName abspath = x15ac + "absPath";

    public void PatchXml(XDocument xml)
    {
        var absPath = xml
            .Descendants(alternateContent)
            .FirstOrDefault(_ => _.Descendants(abspath).Any());

        absPath?.Remove();
    }

    public bool IsMatch(Entry entry) =>
        entry.FullName == "xl/workbook.xml";
}
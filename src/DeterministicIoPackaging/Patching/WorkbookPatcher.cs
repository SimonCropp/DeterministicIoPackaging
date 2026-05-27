class WorkbookPatcher(WorkbookRelationshipPatcher relsPatcher) : IExactMatchPatcher
{
    static XNamespace mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    static XName alternateContent = mc + "AlternateContent";
    static XNamespace x15ac = "http://schemas.microsoft.com/office/spreadsheetml/2010/11/ac";
    static XName abspath = x15ac + "absPath";

    public string ExactMatch => "xl/workbook.xml";

    public void PatchXml(XDocument xml, string entryName)
    {
        DeterministicPackage.FixPrefixedDefaultNamespace(xml);

        // Find the absPath directly and walk up to the AlternateContent
        // ancestor. The previous shape did Descendants(alternateContent)
        // and for each match walked all descendants again looking for
        // absPath — quadratic in the worst case.
        xml.Descendants(abspath)
            .FirstOrDefault()
            ?.Ancestors(alternateContent)
            .FirstOrDefault()
            ?.Remove();

        if (relsPatcher.IdMapping.Count > 0)
        {
            RelationshipRenumber.RemapIds(xml, relsPatcher.IdMapping);
        }
    }

    public bool IsMatch(Entry entry) =>
        entry.FullName == "xl/workbook.xml";
}

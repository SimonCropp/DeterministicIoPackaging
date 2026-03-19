class WorkbookRelationshipPatcher : IPatcher
{
    internal Dictionary<string, string> IdMapping { get; private set; } = [];

    public bool IsMatch(Entry entry) =>
        entry.FullName is "xl/_rels/workbook.xml.rels";

    public bool PatchXml(XDocument xml, string entryName)
    {
        IdMapping = RelationshipRenumber.RenumberAndSort(xml, entryName);
        return true;
    }
}

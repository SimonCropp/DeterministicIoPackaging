class WorkbookRelationshipPatcher : IExactMatchPatcher
{
    internal Dictionary<string, string> IdMapping { get; private set; } = [];

    public string ExactMatch => "xl/_rels/workbook.xml.rels";

    public bool IsMatch(Entry entry) =>
        entry.FullName is "xl/_rels/workbook.xml.rels";

    public void PatchXml(XDocument xml, string entryName) =>
        IdMapping = RelationshipRenumber.RenumberAndSort(xml, entryName);
}

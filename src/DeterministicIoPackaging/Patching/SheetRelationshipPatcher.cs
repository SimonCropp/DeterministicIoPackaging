class SheetRelationshipPatcher : IPatcher
{
    public bool IsMatch(Entry entry) =>
        entry.FullName.StartsWith("xl/worksheets/_rels/") &&
        entry.FullName.EndsWith(".xml.rels");

    public void PatchXml(XDocument xml, string entryName) =>
        RelationshipRenumber.RenumberAndSort(xml, entryName);
}

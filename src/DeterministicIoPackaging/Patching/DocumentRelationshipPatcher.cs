class DocumentRelationshipPatcher : IPatcher
{
    internal Dictionary<string, string> IdMapping { get; private set; } = [];

    public bool IsMatch(Entry entry) =>
        entry.FullName is "word/_rels/document.xml.rels";

    public bool PatchXml(XDocument xml, string entryName)
    {
        IdMapping = RelationshipRenumber.RenumberAndSort(xml, entryName);
        return true;
    }
}

class DocumentRelationshipPatcher : IExactMatchPatcher
{
    internal Dictionary<string, string> IdMapping { get; private set; } = [];

    public string ExactMatch => "word/_rels/document.xml.rels";

    public bool IsMatch(Entry entry) =>
        entry.FullName is "word/_rels/document.xml.rels";

    public void PatchXml(XDocument xml, string entryName) =>
        IdMapping = RelationshipRenumber.RenumberAndSort(xml, entryName);
}

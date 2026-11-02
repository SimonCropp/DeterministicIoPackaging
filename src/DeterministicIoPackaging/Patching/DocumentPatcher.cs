class DocumentPatcher(DocumentRelationshipPatcher relsPatcher, WordIdNumbering idNumbering) : IExactMatchPatcher
{
    public string ExactMatch => "word/document.xml";

    public bool IsMatch(Entry entry) =>
        entry.FullName is "word/document.xml";

    public void PatchXml(XDocument xml, string entryName)
    {
        idNumbering.Apply(xml.Root!);

        if (relsPatcher.IdMapping.Count > 0)
        {
            RelationshipRenumber.RemapIds(xml, relsPatcher.IdMapping);
        }
    }
}

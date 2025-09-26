class SheetRelationshipPatcher : IPatcher
{
    public bool IsMatch(Entry entry) =>
        entry.FullName.StartsWith("xl/worksheets/_rels/") &&
        entry.FullName.EndsWith(".xml.rels");

    public void PatchXml(XDocument xml)
    {
        var root = xml.Root!;
        var relationships = root.Elements()
            .OrderBy(_ => _.Attribute("Id")!.Value)
            .ToList();
        root.ReplaceAll(relationships);
    }
}
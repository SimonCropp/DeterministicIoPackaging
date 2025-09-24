class WorkbookRelationshipPatcher : IPatcher
{
    public bool IsMatch(Entry entry) =>
        entry.FullName is "xl/_rels/workbook.xml.rels";

    public void PatchXml(XDocument xml)
    {
        var root = xml.Root!;
        var relationships = root.Elements()
            .OrderBy(_ => _.Attribute("Type")!.Value)
            .ThenBy(_ => _.Attribute("Target")!.Value)
            .ToList();
        root.ReplaceAll(relationships);
    }
}
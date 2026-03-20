class SheetRelationshipPatcher : IPatcher
{
    internal Dictionary<string, Dictionary<string, string>> IdMappings { get; } = [];

    public bool IsMatch(Entry entry) =>
        entry.FullName.StartsWith("xl/worksheets/_rels/") &&
        entry.FullName.EndsWith(".xml.rels");

    public void PatchXml(XDocument xml, string entryName)
    {
        var mapping = RelationshipRenumber.RenumberAndSort(xml, entryName);
        if (mapping.Count > 0)
        {
            // xl/worksheets/_rels/sheet1.xml.rels → sheet1.xml
            var sheetName = entryName
                .Replace("xl/worksheets/_rels/", "")
                .Replace(".rels", "");
            IdMappings[sheetName] = mapping;
        }
    }
}

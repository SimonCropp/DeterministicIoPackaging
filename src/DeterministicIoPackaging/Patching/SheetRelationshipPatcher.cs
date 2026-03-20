class SheetRelationshipPatcher : IPatcher
{
    // oldId → newId (DeterministicIdN) per sheet
    internal Dictionary<string, Dictionary<string, string>> IdMappings { get; } = [];

    // DeterministicIdN → target URL per sheet.
    // Used by SheetPatcher to normalize interchangeable IDs when multiple
    // relationships share the same target (e.g. two hyperlinks to the same URL).
    internal Dictionary<string, Dictionary<string, string>> TargetMappings { get; } = [];

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

            // Build DeterministicId → target lookup from the renumbered rels
            var targets = new Dictionary<string, string>();
            foreach (var rel in xml.Root!.Elements())
            {
                targets[rel.Attribute("Id")!.Value] = rel.Attribute("Target")!.Value;
            }

            TargetMappings[sheetName] = targets;
        }
    }
}

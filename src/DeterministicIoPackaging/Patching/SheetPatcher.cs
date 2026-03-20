class SheetPatcher(SheetRelationshipPatcher relsPatcher) : IPatcher
{
    static XNamespace xr = "http://schemas.microsoft.com/office/spreadsheetml/2014/revision";
    static XName xName = xr + "uid";

    public void PatchXml(XDocument xml, string entryName)
    {
        DeterministicPackage.FixPrefixedDefaultNamespace(xml);
        xml.Root!.Attribute(xName)?.Remove();

        // xl/worksheets/sheet1.xml → sheet1.xml
        var sheetName = entryName.Replace("xl/worksheets/", "");
        if (relsPatcher.IdMappings.TryGetValue(sheetName, out var mapping) && mapping.Count > 0)
        {
            RelationshipRenumber.RemapIds(xml, mapping);
        }
    }

    public bool IsMatch(Entry entry)
    {
        var name = entry.FullName;
        return name.StartsWith("xl/worksheets/") &&
               name.EndsWith(".xml");
    }
}

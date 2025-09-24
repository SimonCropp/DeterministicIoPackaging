static class WorkbookRelationshipPatcher
{
    public static bool IsWorkbookRelationships(this Entry entry) =>
        entry.FullName is "xl/_rels/workbook.xml.rels";

    public static XDocument Patch(Stream sourceStream)
    {
        var xml = XDocument.Load(sourceStream);
        Patch(xml);
        return xml;
    }

    public static async Task<XDocument> Patch(Stream stream, Cancel cancel)
    {
        var xml = await XDocument.LoadAsync(stream, LoadOptions.None, cancel);
        Patch(xml);
        return xml;
    }

    static void Patch(XDocument xml)
    {
        var root = xml.Root!;
        var relationships = root.Elements()
            .OrderBy(_ => _.Attribute("Type")!.Value)
            .ToList();
        root.ReplaceAll(relationships);
    }
}
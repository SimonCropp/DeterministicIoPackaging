static class Relationships
{
    public static bool IsWorkbookRelationships(this Entry entry) =>
        entry.FullName is "xl/_rels/workbook.xml.rels";

    public static bool IsRelationships(this Entry entry) =>
        entry.FullName is "_rels/.rels";

    public static XDocument PatchRelationships(Stream sourceStream)
    {
        var xml = XDocument.Load(sourceStream);
        PatchRelationships(xml);
        return xml;
    }

    public static XDocument PatchWorkbookRelationships(Stream sourceStream)
    {
        var xml = XDocument.Load(sourceStream);
        PatchWorkbookRelationships(xml);
        return xml;
    }

    public static void PatchWorkbookRelationships(XDocument xml)
    {
        var root = xml.Root!;
        var relationships = root.Elements()
            .OrderBy(_ => _.Attribute("Type")!.Value)
            .ToList();
        root.ReplaceAll(relationships);
    }

    public static void PatchRelationships(XDocument xml)
    {
        var root = xml.Root!;
        var relationships = root.Elements()
            .OrderBy(_ => _.Attribute("Type")!.Value)
            .ToList();

        foreach (var element in relationships.Where(IsPsmdcpElement).ToList())
        {
            relationships.Remove(element);
        }

        for (var index = 0; index < relationships.Count; index++)
        {
            var relationship = relationships[index];
            relationship.Attribute("Id")!.SetValue($"DeterministicId{index + 1}");
        }

        root.ReplaceAll(relationships);

        static bool IsPsmdcpElement(XElement element)
        {
            var target = element.Attribute("Target")!;
            return target.Value.EndsWith(".psmdcp");
        }
    }
}
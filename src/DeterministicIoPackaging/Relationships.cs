static class Relationships
{
    static XName relationshipName = XName.Get("Relationship", "http://schemas.openxmlformats.org/package/2006/relationships");

    public static bool IsWorkbookRelationships(this Entry entry) =>
        entry.FullName is "xl/_rels/workbook.xml.rels";

    public static bool IsRelationships(this Entry entry) =>
        entry.FullName is "_rels/.rels";

    public static XDocument PatchRelationships(Stream sourceStream)
    {
        var xml = XDocument.Load(sourceStream);
        PatchRelationships(xml, true);
        return xml;
    }

    public static XDocument PatchWorkbookRelationships(Stream sourceStream)
    {
        var xml = XDocument.Load(sourceStream);
        PatchWorkbookRelationships(xml);
        return xml;
    }

    public static void PatchWorkbookRelationships(XDocument xml) =>
        PatchRelationships(xml, false);

    public static void PatchRelationships(XDocument xml, bool patchIds)
    {
        var root = xml.Root!;
        var relationships = root.Elements(relationshipName)
            .Where(_ => !IsPsmdcpElement(_))
            .OrderBy(_ => _.Attribute("Type")!.Value)
            .ToList();

        root.Elements(relationshipName).Remove();

        if (patchIds)
        {
            for (var index = 0; index < relationships.Count; index++)
            {
                var relationship = relationships[index];
                relationship.Attribute("Id")!.SetValue($"DeterministicId{index + 1}");
            }
        }

        root.Add(relationships);

        static bool IsPsmdcpElement(XElement element)
        {
            var target = element.Attribute("Target")!;
            return target.Value.EndsWith(".psmdcp");
        }
    }
}
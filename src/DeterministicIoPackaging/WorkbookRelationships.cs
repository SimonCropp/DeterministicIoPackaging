static class WorkbookRelationships
{
    static XName relationshipName = XName.Get("Relationship", "http://schemas.openxmlformats.org/package/2006/relationships");
    public static bool IsWorkbookRelationships(this Entry entry) =>
        entry.FullName is "xl/_rels/workbook.xml.rels";
    public static XDocument PatchRelationships(XDocument xml, bool patchIds)
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
        return xml;

        static bool IsPsmdcpElement(XElement rel)
        {
            var target = rel.Attribute("Target")!;
            return target.Value.EndsWith(".psmdcp");
        }
    }
}
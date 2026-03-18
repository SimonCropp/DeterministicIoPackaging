static class RelationshipRenumber
{
    static XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static XName rId = r + "id";

    public static Dictionary<string, string> RenumberAndSort(XDocument xml)
    {
        var root = xml.Root!;
        var relationships = root.Elements()
            .OrderBy(_ => _.Attribute("Type")!.Value)
            .ThenBy(_ => _.Attribute("Target")!.Value)
            .ToList();

        var mapping = new Dictionary<string, string>();
        for (var index = 0; index < relationships.Count; index++)
        {
            var relationship = relationships[index];
            var idAttr = relationship.Attribute("Id")!;
            var newId = $"DeterministicId{index + 1}";
            mapping[idAttr.Value] = newId;
            idAttr.SetValue(newId);
        }

        root.ReplaceAll(relationships);
        return mapping;
    }

    public static void RemapIds(XDocument xml, Dictionary<string, string> mapping)
    {
        foreach (var attr in xml.Descendants().Attributes(rId))
        {
            if (mapping.TryGetValue(attr.Value, out var newId))
            {
                attr.SetValue(newId);
            }
        }
    }
}

interface IPatcher
{
    public void PatchXml(XDocument xml);
    public bool IsMatch(Entry entry);
}

static class RelationshipRenumber
{
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
        foreach (var attr in xml.Descendants().Attributes())
        {
            if (attr.Name.LocalName == "id" &&
                attr.Name.Namespace != XNamespace.None &&
                mapping.TryGetValue(attr.Value, out var newId))
            {
                attr.SetValue(newId);
            }
        }
    }
}

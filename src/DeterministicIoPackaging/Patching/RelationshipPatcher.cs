class RelationshipPatcher : IPatcher
{
    public bool IsMatch(Entry entry) =>
        entry.FullName is "_rels/.rels";

    public void PatchXml(XDocument xml)
    {
        var root = xml.Root!;
        var relationships = root.Elements()
            .OrderBy(_ => _.Attribute("Type")!.Value)
            .ThenBy(_ => _.Attribute("Target")!.Value)
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
    }

    static bool IsPsmdcpElement(XElement element)
    {
        var target = element.Attribute("Target")!;
        return target.Value.EndsWith(".psmdcp");
    }
}
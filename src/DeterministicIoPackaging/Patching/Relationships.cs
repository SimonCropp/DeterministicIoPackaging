static class Relationships
{
    public static bool IsRelationships(this Entry entry) =>
        entry.FullName is "_rels/.rels";

    public static XDocument Patch(Stream stream)
    {
        var xml = XDocument.Load(stream);
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
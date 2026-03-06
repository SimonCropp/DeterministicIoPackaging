class ContentTypesPatcher : IPatcher
{
    public bool IsMatch(Entry entry) =>
        entry.FullName is "[Content_Types].xml";

    public void PatchXml(XDocument xml)
    {
        var root = xml.Root!;
        var elements = root.Elements()
            .OrderBy(_ => _.Name.LocalName)
            .ThenBy(_ => (string?)_.Attribute("Extension") ?? "")
            .ThenBy(_ => (string?)_.Attribute("PartName") ?? "")
            .ToList();

        root.ReplaceAll(elements);
    }
}

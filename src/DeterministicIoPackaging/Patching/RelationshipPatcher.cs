class RelationshipPatcher : IPatcher
{
    public bool IsMatch(Entry entry) =>
        entry.FullName is "_rels/.rels";

    public bool PatchXml(XDocument xml, string entryName)
    {
        var root = xml.Root!;

        foreach (var element in root.Elements().Where(IsPsmdcpElement).ToList())
        {
            element.Remove();
        }

        RelationshipRenumber.RenumberAndSort(xml, entryName);

        return true;
    }

    static bool IsPsmdcpElement(XElement element)
    {
        var target = element.Attribute("Target")!;
        return target.Value.EndsWith(".psmdcp");
    }
}

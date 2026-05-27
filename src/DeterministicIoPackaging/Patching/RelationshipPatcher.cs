class RelationshipPatcher : IExactMatchPatcher
{
    public string ExactMatch => "_rels/.rels";

    public bool IsMatch(Entry entry) =>
        entry.FullName is "_rels/.rels";

    public void PatchXml(XDocument xml, string entryName)
    {
        var root = xml.Root!;

        // Extensions.Remove(IEnumerable<XElement>) snapshots internally, so
        // the explicit ToList() + foreach loop is redundant.
        root.Elements().Where(IsPsmdcpElement).Remove();

        RelationshipRenumber.RenumberAndSort(xml, entryName);
    }

    static bool IsPsmdcpElement(XElement element)
    {
        var target = element.Attribute("Target")!;
        return target.Value.EndsWith(".psmdcp");
    }
}

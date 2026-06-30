class NumberingPatcher : IExactMatchPatcher
{
    public string ExactMatch => "word/numbering.xml";

    static XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static XName nsid = w + "nsid";
    static XName abstractNum = w + "abstractNum";
    static XName abstractNumId = w + "abstractNumId";
    static XName num = w + "num";
    static XName val = w + "val";

    public bool IsMatch(Entry entry) =>
        entry.FullName is "word/numbering.xml";

    public void PatchXml(XDocument xml, string entryName)
    {
        var root = xml.Root!;

        // Remove all w:nsid elements
        root
            .Descendants(nsid)
            .ToList()
            .Remove();

        // Remove redundant namespace declarations from all descendants
        // This normalizes elements that declare xmlns:p2="..." when the root already has the default namespace
        RemoveRedundantNamespaceDeclarations(root);

        // Sort abstractNum elements by their content to ensure deterministic order
        // First normalize the abstractNumId to a placeholder so sorting is consistent
        var abstractNums = root.Elements(abstractNum).ToList();

        // Create a mapping from old abstractNumId to new abstractNumId (sorted index)
        // We need to determine the sorted order first by comparing content excluding the ID
        var idMapping = new Dictionary<string, string>();

        // Temporarily set all abstractNumId to "0" for consistent sorting
        var originalIds = new Dictionary<XElement, string>();
        foreach (var element in abstractNums)
        {
            var attr = element.Attribute(abstractNumId);
            if (attr != null)
            {
                originalIds[element] = attr.Value;
                attr.Value = "0";
            }
        }

        // Now sort by content (which is now consistent since IDs are all "0").
        // Use Ordinal rather than the culture-sensitive default comparer so the
        // order — and therefore the abstractNumId assignment below — is identical
        // across machines, cultures and OSes. Without it, OrderBy's default
        // linguistic comparison is a latent determinism risk (see the same
        // deliberate choice in RelationshipRenumber.RenumberAndSort). OrderBy
        // (stable) is retained so equal-content elements keep document order.
        var sortedAbstractNums = abstractNums
            .OrderBy(_ => _.ToString(), StringComparer.Ordinal)
            .ToList();

        // Build the mapping and update abstractNumId attributes to their new sorted index
        for (var i = 0; i < sortedAbstractNums.Count; i++)
        {
            var element = sortedAbstractNums[i];
            var newId = i.ToString();
            element.Attribute(abstractNumId)!.Value = newId;
            if (originalIds.TryGetValue(element, out var oldId))
            {
                idMapping[oldId] = newId;
            }
        }

        // Replace abstractNum elements with sorted ones
        var nums = root.Elements(num).ToList();
        root.ReplaceAll(sortedAbstractNums.Concat(nums));

        // Update references in num elements
        foreach (var numElement in root.Elements(num))
        {
            var abstractNumIdElement = numElement.Element(abstractNumId);
            if (abstractNumIdElement == null)
            {
                continue;
            }

            var oldId = abstractNumIdElement.Attribute(val)?.Value;
            if (oldId == null ||
                !idMapping.TryGetValue(oldId, out var newId))
            {
                continue;
            }

            abstractNumIdElement.Attribute(val)!.Value = newId;
        }
    }

    static void RemoveRedundantNamespaceDeclarations(XElement root)
    {
        // Get the default namespace from the root
        var defaultNsName = root.GetDefaultNamespace().NamespaceName;

        // Walk attributes via the linked-list to avoid allocating a Where
        // iterator and a List<XAttribute> for every element.
        foreach (var element in root.DescendantsAndSelf())
        {
            var attr = element.FirstAttribute;
            while (attr != null)
            {
                var next = attr.NextAttribute;
                if (attr.IsNamespaceDeclaration &&
                    attr.Value == defaultNsName &&
                    // Don't remove the default namespace declaration itself
                    attr.Name.LocalName != "xmlns")
                {
                    attr.Remove();
                }

                attr = next;
            }
        }
    }
}

// Takes the WordPartRelationshipPatcher because word/_rels/numbering.xml.rels is one of the parts it
// renumbers: picture bullets (w:numPicBullet) reference their images by r:id, and those ids have to
// follow the renumbering.
class NumberingPatcher(WordPartRelationshipPatcher relsPatcher) : IExactMatchPatcher
{
    public NumberingPatcher() :
        this(new())
    {
    }

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

        PutBackInOrder(root, abstractNums, sortedAbstractNums);

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

        if (relsPatcher.IdMappings.TryGetValue(entryName, out var mapping) &&
            mapping.Count > 0)
        {
            RelationshipRenumber.RemapIds(xml, mapping);
        }
    }

    // Moves the abstractNums into sorted order where they stood, and touches nothing else. The
    // part holds more than abstractNums and nums: w:numPicBullet (the picture bullets the
    // abstractNums point at) comes before them, w:numIdMacAtCleanup after, and the root carries
    // the namespace declarations and mc:Ignorable. ReplaceAll would have removed all of that.
    static void PutBackInOrder(XElement root, List<XElement> abstractNums, List<XElement> sorted)
    {
        if (abstractNums.Count == 0)
        {
            return;
        }

        var anchor = abstractNums[0].PreviousNode;
        foreach (var element in abstractNums)
        {
            element.Remove();
        }

        if (anchor == null)
        {
            root.AddFirst(sorted);
        }
        else
        {
            anchor.AddAfterSelf(sorted);
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

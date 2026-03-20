class SheetPatcher(SheetRelationshipPatcher relsPatcher) : IPatcher
{
    static XNamespace xr = "http://schemas.microsoft.com/office/spreadsheetml/2014/revision";
    static XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static XName xName = xr + "uid";
    static XName rId = r + "id";

    public void PatchXml(XDocument xml, string entryName)
    {
        DeterministicPackage.FixPrefixedDefaultNamespace(xml);
        xml.Root!.Attribute(xName)?.Remove();

        // xl/worksheets/sheet1.xml → sheet1.xml
        var sheetName = entryName.Replace("xl/worksheets/", "");
        if (relsPatcher.IdMappings.TryGetValue(sheetName, out var mapping) && mapping.Count > 0)
        {
            RelationshipRenumber.RemapIds(xml, mapping);
        }

        if (relsPatcher.TargetMappings.TryGetValue(sheetName, out var targets) && targets.Count > 0)
        {
            NormalizeInterchangeableIds(xml, targets);
        }
    }

    // When multiple relationships share the same target (e.g. two hyperlinks to the same URL),
    // the DeterministicId assignment depends on the non-deterministic original order.
    // Normalize by assigning the lowest DeterministicId to the earliest cell reference.
    static void NormalizeInterchangeableIds(XDocument xml, Dictionary<string, string> targets)
    {
        // Find targets that are shared by multiple DeterministicIds
        var targetToIds = new Dictionary<string, List<string>>();
        foreach (var (id, target) in targets)
        {
            if (!targetToIds.TryGetValue(target, out var ids))
            {
                ids = [];
                targetToIds[target] = ids;
            }

            ids.Add(id);
        }

        // Only process targets with multiple IDs (the ambiguous case)
        var interchangeableGroups = new Dictionary<string, List<string>>();
        foreach (var (target, ids) in targetToIds)
        {
            if (ids.Count > 1)
            {
                interchangeableGroups[target] = ids;
            }
        }

        if (interchangeableGroups.Count == 0)
        {
            return;
        }

        // Build a set of all interchangeable IDs for quick lookup
        var interchangeableIds = new HashSet<string>();
        foreach (var ids in interchangeableGroups.Values)
        {
            foreach (var id in ids)
            {
                interchangeableIds.Add(id);
            }
        }

        // Find all elements with r:id attributes that reference interchangeable IDs,
        // grouped by target
        var targetToElements = new Dictionary<string, List<XAttribute>>();
        foreach (var attr in xml.Descendants().Attributes(rId))
        {
            if (!interchangeableIds.Contains(attr.Value))
            {
                continue;
            }

            var target = targets[attr.Value];
            if (!targetToElements.TryGetValue(target, out var elements))
            {
                elements = [];
                targetToElements[target] = elements;
            }

            elements.Add(attr);
        }

        // For each group, sort elements by a deterministic key (parent ref attribute, then element position)
        // and assign DeterministicIds in sorted order
        foreach (var attributes in targetToElements.Values)
        {
            if (attributes.Count <= 1)
            {
                continue;
            }

            // Sort by the ref attribute of the parent element (cell reference like "B2"),
            // falling back to string comparison of the current r:id value
            attributes.Sort((a, b) =>
            {
                var refA = a.Parent?.Attribute("ref")?.Value ?? "";
                var refB = b.Parent?.Attribute("ref")?.Value ?? "";
                var cmp = string.Compare(refA, refB, StringComparison.Ordinal);
                return cmp != 0 ? cmp : string.Compare(a.Value, b.Value, StringComparison.Ordinal);
            });

            // Collect and sort the DeterministicIds
            var sortedIds = attributes.Select(_ => _.Value).Order(StringComparer.Ordinal).ToList();

            // Assign in order
            for (var i = 0; i < attributes.Count; i++)
            {
                attributes[i].SetValue(sortedIds[i]);
            }
        }
    }

    public bool IsMatch(Entry entry)
    {
        var name = entry.FullName;
        return name.StartsWith("xl/worksheets/") &&
               name.EndsWith(".xml");
    }
}

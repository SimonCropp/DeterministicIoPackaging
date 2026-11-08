static class RelationshipRenumber
{
    static XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    // ISO/IEC 29500 Strict variant of the relationships namespace. "Strict Open XML Document" (Word)
    // and Aspose's Iso29500_2008_Strict emit r:id/r:embed/r:link in this namespace; without matching
    // it, RemapIds would leave those references pointing at the pre-renumber ids (dangling).
    static XNamespace rStrict = "http://purl.oclc.org/ooxml/officeDocument/relationships";
    // VML's own attribute for a relationship id (v:imagedata, v:fill), beside r:id.
    static XName oRelId = XNamespace.Get("urn:schemas-microsoft-com:office:office") + "relid";

    public static Dictionary<string, string> RenumberAndSort(XDocument xml, string? entryName = null)
    {
        var root = xml.Root!;

        if (entryName != null)
        {
            NormalizeTargets(root, entryName);
        }

        // Ordinal sort matches the entry-name comparison used elsewhere (see
        // DeterministicPackage_Convert.OrderedEntries). Without an explicit
        // comparer, OrderBy defaults to culture-sensitive comparison — slower
        // and a latent determinism risk on machines with different cultures.
        var relationships = root.Elements()
            .OrderBy(_ => _.Attribute("Type")!.Value, StringComparer.Ordinal)
            .ThenBy(_ => _.Attribute("Target")!.Value, StringComparer.Ordinal)
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

    // Convert absolute Target paths to relative.
    // Spreadsheet Compare cannot open xlsx files with absolute relationship targets.
    // e.g. in _rels/.rels: Target="/xl/workbook.xml" -> Target="xl/workbook.xml"
    // e.g. in xl/_rels/workbook.xml.rels: Target="/xl/worksheets/sheet1.xml" -> Target="worksheets/sheet1.xml"
    static void NormalizeTargets(XElement root, string entryName)
    {
        // _rels/.rels -> base is ""
        // xl/_rels/workbook.xml.rels -> base is "xl/"
        // word/_rels/document.xml.rels -> base is "word/"
        var relsIndex = entryName.IndexOf("/_rels/");
        var basePath = relsIndex > 0
            ? "/" + entryName[..relsIndex] + "/"
            : "/";

        foreach (var element in root.Elements())
        {
            var targetAttr = element.Attribute("Target");
            if (targetAttr == null)
            {
                continue;
            }

            var target = targetAttr.Value;
            if (!target.StartsWith('/'))
            {
                continue;
            }

            // Strip the base path prefix to make it relative
            if (target.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                targetAttr.SetValue(target[basePath.Length..]);
            }
            // else: absolute path points outside base directory — keep as-is
            // (stripping the leading / would create a broken relative path)
        }
    }

    // Every attribute the relationships namespace defines is a relationship id: not only r:id,
    // r:embed and r:link, but SmartArt's r:dm/r:lo/r:qs/r:cs on dgm:relIds, and VML's r:pict and
    // r:href on v:imagedata. So match on the namespace rather than a list of names, which left the
    // ones not listed pointing at ids the renumbering had replaced.
    public static void RemapIds(XDocument xml, Dictionary<string, string> mapping)
    {
        // Walk attributes via the linked-list once per element. XNamespace and XName equality are
        // fast reference compares because both are interned.
        foreach (var descendant in xml.Descendants())
        {
            for (var attr = descendant.FirstAttribute; attr != null; attr = attr.NextAttribute)
            {
                if (IsRelationshipId(attr.Name) &&
                    mapping.TryGetValue(attr.Value, out var newId))
                {
                    attr.SetValue(newId);
                }
            }
        }
    }

    static bool IsRelationshipId(XName name)
    {
        var ns = name.Namespace;
        return ns == r ||
               ns == rStrict ||
               name == oRelId;
    }
}

static class RelationshipRenumber
{
    static XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static XName rId = r + "id";

    public static Dictionary<string, string> RenumberAndSort(XDocument xml, string? entryName = null)
    {
        var root = xml.Root!;

        if (entryName != null)
        {
            NormalizeTargets(root, entryName);
        }

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
            else
            {
                // Absolute path doesn't match base — just strip the leading /
                targetAttr.SetValue(target[1..]);
            }
        }
    }

    public static void RemapIds(XDocument xml, Dictionary<string, string> mapping)
    {
        foreach (var attr in xml.Descendants().Attributes(rId))
        {
            if (mapping.TryGetValue(attr.Value, out var newId))
            {
                attr.SetValue(newId);
            }
        }
    }
}

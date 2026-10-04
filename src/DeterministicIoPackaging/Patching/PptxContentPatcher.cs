// Patches PowerPoint content XML files (presentation.xml, slide*.xml,
// slideLayout*.xml, slideMaster*.xml, notesSlide*.xml, etc.) to remap
// relationship IDs that were renumbered by PptxRelationshipPatcher, and to
// renumber the ids of text fields.
//
// Must be registered after PptxRelationshipPatcher so that ID mappings
// are populated before this patcher runs.
//
// Only parts that have a .rels file are matched. Every part that can hold a
// text field has one: a slide names its layout, a layout its master, a master
// its theme, a notes slide its slide.
class PptxContentPatcher(PptxRelationshipPatcher relsPatcher) : IPatcher
{
    static XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static XName field = a + "fld";

    public bool IsMatch(Entry entry) =>
        entry.FullName.StartsWith("ppt/") &&
        !entry.FullName.Contains("/_rels/") &&
        entry.FullName.EndsWith(".xml") &&
        relsPatcher.IdMappings.ContainsKey(entry.FullName);

    public void PatchXml(XDocument xml, string entryName)
    {
        if (relsPatcher.IdMappings.TryGetValue(entryName, out var mapping) && mapping.Count > 0)
        {
            RelationshipRenumber.RemapIds(xml, mapping);
        }

        RenumberFieldIds(xml);
    }

    // A text field (a:fld: a slide number, a date) is identified by a guid. A producer
    // that builds a presentation in code, rather than loading one, makes those up on
    // every save. They are numbered instead, in document order.
    //
    // Numbered within the part, so a field added to one slide leaves the ids of every
    // other part alone. The same id then appears in several parts. Within the part,
    // fields that shared a guid still do.
    static void RenumberFieldIds(XDocument xml)
    {
        Dictionary<string, string>? ids = null;
        foreach (var element in xml.Descendants(field))
        {
            var attribute = element.Attribute("id");
            if (attribute == null)
            {
                continue;
            }

            ids ??= new(StringComparer.OrdinalIgnoreCase);
            if (!ids.TryGetValue(attribute.Value, out var id))
            {
                id = $"{{{ids.Count + 1:X8}-0000-0000-0000-000000000000}}";
                ids[attribute.Value] = id;
            }

            attribute.Value = id;
        }
    }
}

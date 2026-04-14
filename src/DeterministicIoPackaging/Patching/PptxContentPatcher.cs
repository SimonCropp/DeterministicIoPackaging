// Patches PowerPoint content XML files (presentation.xml, slide*.xml,
// slideLayout*.xml, slideMaster*.xml, notesSlide*.xml, etc.) to remap
// relationship IDs that were renumbered by PptxRelationshipPatcher.
//
// Must be registered after PptxRelationshipPatcher so that ID mappings
// are populated before this patcher runs.
class PptxContentPatcher(PptxRelationshipPatcher relsPatcher) : IPatcher
{
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
    }
}

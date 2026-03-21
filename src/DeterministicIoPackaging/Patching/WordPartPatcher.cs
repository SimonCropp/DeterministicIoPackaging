// Patches Word sub-part XML files (headers, footers, etc.) to remap
// relationship IDs that were renumbered by WordPartRelationshipPatcher.
//
// Matches files like word/footer1.xml, word/header1.xml — any XML file
// under word/ that has a corresponding entry in the relationship patcher's
// ID mappings. Excludes document.xml and numbering.xml which have their
// own dedicated patchers.
//
// Must be registered after WordPartRelationshipPatcher so that ID mappings
// are populated before this patcher runs.
class WordPartPatcher(WordPartRelationshipPatcher relsPatcher) : IPatcher
{
    public bool IsMatch(Entry entry) =>
        entry.FullName.StartsWith("word/") &&
        entry.FullName != "word/document.xml" &&
        entry.FullName != "word/numbering.xml" &&
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

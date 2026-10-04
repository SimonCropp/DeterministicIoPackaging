// Patches Word sub-part XML files (headers, footers, etc.) to strip
// per-save revision markers and protection salts/hashes (word/settings.xml), and remap relationship IDs that were
// renumbered by WordPartRelationshipPatcher.
//
// Matches files like word/footer1.xml, word/header1.xml — any XML file
// under word/ that is not a relationship file. Excludes document.xml and
// numbering.xml which have their own dedicated patchers.
//
// Must be registered after WordPartRelationshipPatcher so that ID mappings
// are populated before this patcher runs.
class WordPartPatcher(WordPartRelationshipPatcher relsPatcher) : IPatcher
{
    public bool IsMatch(Entry entry)
    {
        var name = entry.FullName;
        return name.StartsWith("word/") &&
               name != "word/document.xml" &&
               name != "word/numbering.xml" &&
               !name.Contains("/_rels/") &&
               name.EndsWith(".xml");
    }

    public void PatchXml(XDocument xml, string entryName)
    {
        WordRevisionMarkers.Strip(xml);
        ProtectionHashes.Scrub(xml);

        if (relsPatcher.IdMappings.TryGetValue(entryName, out var mapping) &&
            mapping.Count > 0)
        {
            RelationshipRenumber.RemapIds(xml, mapping);
        }
    }
}

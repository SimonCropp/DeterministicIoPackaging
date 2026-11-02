// Strips revision markers from a Word part and renumbers the ids that
// producers assign arbitrarily:
//   - wp:docPr id and pic:cNvPr id on drawings (often random or a running
//     counter that depends on what else the producer did in the session)
//   - w:sdtPr/w:id on content controls (a random int per control)
//
// One instance is shared by DocumentPatcher and WordPartPatcher for a
// conversion, so numbering continues from part to part rather than
// restarting at 1 in each. Both id spaces are meant to be unique across the
// whole document (Word can treat a duplicate wp:docPr id in a header and
// the body as corruption). Entries are processed in ordinal name order, so
// the numbering is deterministic.
class WordIdNumbering
{
    int nextDrawingId = 1;
    int nextSdtId = 1;

    public void Apply(XElement root)
    {
        var docPrIds = new List<XAttribute>();
        var picIds = new List<XAttribute>();
        var sdtIds = new List<XAttribute>();
        WordRevisionMarkers.StripAndCollectIds(root, docPrIds, picIds, sdtIds);

        // All wp:docPr ids of the part first, then pic:cNvPr ids, numbering
        // continuing from where the docPrs left off.
        foreach (var attribute in docPrIds)
        {
            attribute.Value = nextDrawingId.ToString();
            nextDrawingId++;
        }

        foreach (var attribute in picIds)
        {
            attribute.Value = nextDrawingId.ToString();
            nextDrawingId++;
        }

        foreach (var attribute in sdtIds)
        {
            attribute.Value = nextSdtId.ToString();
            nextSdtId++;
        }
    }
}

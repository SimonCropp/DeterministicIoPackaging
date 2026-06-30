// Strips per-save revision/identity attributes from WordprocessingML XML.
//
// Word and tools like Aspose.Words emit random IDs on every save for
// change-tracking and cross-reference lookup. They have no semantic
// meaning for document content and break deterministic output:
//   - w14:paraId / w14:textId on <w:p> (random hex per paragraph save)
//   - w:rsidR, w:rsidRPr, w:rsidP, w:rsidRDefault, w:rsidDel,
//     w:rsidTr, w:rsidSect (revision save IDs on paragraphs, runs,
//     table rows, section properties)
static class WordRevisionMarkers
{
    static XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static XNamespace w14 = "http://schemas.microsoft.com/office/word/2010/wordml";

    static readonly HashSet<XName> attributesToRemove =
    [
        w14 + "paraId",
        w14 + "textId",
        w + "rsidR",
        w + "rsidRPr",
        w + "rsidP",
        w + "rsidRDefault",
        w + "rsidDel",
        w + "rsidTr",
        w + "rsidSect"
    ];

    public static void Strip(XDocument xml)
    {
        var root = xml.Root;
        if (root == null)
        {
            return;
        }

        foreach (var element in root.DescendantsAndSelf())
        {
            StripAttributes(element);
        }
    }

    // Strips revision markers and, in the same traversal, collects the
    // wp:docPr / pic:cNvPr id attributes DocumentPatcher renumbers.
    // word/document.xml is the largest part in a .docx, so folding the strip
    // and the id collection into a single Descendants() walk avoids traversing
    // the whole tree twice. The collection order is identical to a standalone
    // root.Descendants() pass: all docPr ids then all pic ids in document order.
    public static void StripAndCollectDrawingIds(
        XElement root,
        XName docPrName,
        XName cNvPrName,
        List<XAttribute> docPrIds,
        List<XAttribute> picIds)
    {
        // Process the root's own attributes first, mirroring Strip's use of
        // DescendantsAndSelf. The root of word/document.xml is w:document —
        // never a drawing element — so it needs stripping, not id collection.
        StripAttributes(root);

        foreach (var element in root.Descendants())
        {
            StripAttributes(element);

            var name = element.Name;
            if (name == docPrName)
            {
                docPrIds.Add(element.Attribute("id")!);
            }
            else if (name == cNvPrName)
            {
                picIds.Add(element.Attribute("id")!);
            }
        }
    }

    // Walk the attribute linked-list manually rather than via LINQ + ToList().
    // Capturing NextAttribute before Remove() lets us mutate safely without
    // per-element allocations — the common case is zero matching attributes, so
    // a Where iterator + List<XAttribute> per node would be pure waste.
    static void StripAttributes(XElement element)
    {
        var attr = element.FirstAttribute;
        while (attr != null)
        {
            var next = attr.NextAttribute;
            if (attributesToRemove.Contains(attr.Name))
            {
                attr.Remove();
            }

            attr = next;
        }
    }
}

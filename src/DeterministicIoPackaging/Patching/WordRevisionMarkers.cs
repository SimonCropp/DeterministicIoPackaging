// Strips per-save revision/identity attributes from WordprocessingML XML.
//
// Word and tools like Aspose.Words emit random IDs on every save for
// change-tracking and cross-reference lookup. They have no semantic
// meaning for document content and break deterministic output:
//   - w14:paraId / w14:textId on <w:p> (random hex per paragraph save)
//   - w:rsidR, w:rsidRPr, w:rsidP, w:rsidRDefault, w:rsidDel,
//     w:rsidTr, w:rsidSect (revision save IDs on paragraphs, runs,
//     table rows, section properties)
//   - wp14:anchorId / wp14:editId on <wp:inline> and <wp:anchor>
//     (random hex per drawing save)
//   - w:date on tracked changes (<w:ins>, <w:del>, <w:rPrChange>, ...)
//     and comments, and w16cex:dateUtc in word/commentsExtensible.xml
//     (the time the change or comment was made, often DateTime.Now
//     when built in code)
static class WordRevisionMarkers
{
    static XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static XNamespace w14 = "http://schemas.microsoft.com/office/word/2010/wordml";
    static XNamespace wp14 = "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing";
    static XNamespace w16cex = "http://schemas.microsoft.com/office/word/2018/wordml/cex";
    static XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    static XNamespace pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    static XName docPr = wp + "docPr";
    static XName picCNvPr = pic + "cNvPr";
    static XName sdtPr = w + "sdtPr";
    static XName sdtId = w + "id";
    static XName val = w + "val";

    static HashSet<XName> attributesToRemove =
    [
        w14 + "paraId",
        w14 + "textId",
        w + "rsidR",
        w + "rsidRPr",
        w + "rsidP",
        w + "rsidRDefault",
        w + "rsidDel",
        w + "rsidTr",
        w + "rsidSect",
        wp14 + "anchorId",
        wp14 + "editId",
        w + "date",
        w16cex + "dateUtc"
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

    // Strips revision markers and, in the same traversal, collects the ids
    // WordIdNumbering renumbers: wp:docPr and pic:cNvPr id attributes, and
    // the w:val of each content control's w:sdtPr/w:id. word/document.xml is
    // the largest part in a .docx, so folding the strip and the id
    // collection into a single Descendants() walk avoids traversing the
    // whole tree twice. Each list is in document order.
    public static void StripAndCollectIds(
        XElement root,
        List<XAttribute> docPrIds,
        List<XAttribute> picIds,
        List<XAttribute> sdtIds)
    {
        // Process the root's own attributes first, mirroring Strip's use of
        // DescendantsAndSelf. The root of a Word part (w:document, w:hdr,
        // w:ftr, ...) is never a drawing or content control element, so it
        // needs stripping, not id collection.
        StripAttributes(root);

        foreach (var element in root.Descendants())
        {
            StripAttributes(element);

            var name = element.Name;
            if (name == docPr)
            {
                docPrIds.Add(element.Attribute("id")!);
            }
            else if (name == picCNvPr)
            {
                picIds.Add(element.Attribute("id")!);
            }
            else if (name == sdtId &&
                     element.Parent!.Name == sdtPr)
            {
                var attribute = element.Attribute(val);
                if (attribute != null)
                {
                    sdtIds.Add(attribute);
                }
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

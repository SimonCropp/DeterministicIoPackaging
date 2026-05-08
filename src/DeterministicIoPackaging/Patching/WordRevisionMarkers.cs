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
            element.Attributes()
                .Where(_ => attributesToRemove.Contains(_.Name))
                .ToList()
                .Remove();
        }
    }
}

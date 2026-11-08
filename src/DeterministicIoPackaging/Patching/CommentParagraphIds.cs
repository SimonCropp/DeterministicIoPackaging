// Keeps a comment linked to its reply thread and resolved state.
//
// WordRevisionMarkers strips w14:paraId everywhere, since Word makes paragraph ids up on every save.
// But in word/comments.xml the id of a comment's last paragraph is how the comment is found from
// the parts that extend it:
//   - word/commentsExtended.xml: w15:commentEx w15:paraId (resolved state) and w15:paraIdParent
//     (the comment a reply answers)
//   - word/commentsIds.xml: w16cid:commentId w16cid:paraId (the durable id)
// Stripping it unthreaded every reply and lost every resolved state. So in word/comments.xml the
// ids are renumbered in document order instead, and the references in those two parts remapped to
// match. comments.xml sorts before both, so its ids are known by the time they are reached.
class CommentParagraphIds
{
    static XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static XNamespace w14 = "http://schemas.microsoft.com/office/word/2010/wordml";
    static XNamespace w15 = "http://schemas.microsoft.com/office/word/2012/wordml";
    static XNamespace w16cid = "http://schemas.microsoft.com/office/word/2016/wordml/cid";
    static XName paragraph = w + "p";
    static XName paraId = w14 + "paraId";

    static HashSet<XName> references =
    [
        w15 + "paraId",
        w15 + "paraIdParent",
        w16cid + "paraId"
    ];

    // Hex, so case does not distinguish two ids.
    Dictionary<string, string> mapping = new(StringComparer.OrdinalIgnoreCase);
    int lastId;

    public static bool IsComments(string entryName) =>
        entryName == "word/comments.xml";

    public static bool RefersToComments(string entryName) =>
        entryName is "word/commentsExtended.xml" or "word/commentsIds.xml";

    // Before the revision markers are stripped: each paragraph that has an id, with that id.
    public static List<(XElement Paragraph, string Id)> Collect(XElement root) =>
        root.Descendants(paragraph)
            .Select(_ => (Paragraph: _, Id: _.Attribute(paraId)?.Value))
            .Where(_ => _.Id != null)
            .Select(_ => (_.Paragraph, _.Id!))
            .ToList();

    // After the strip: give each of those paragraphs its id back, numbered in document order.
    // Ids are ST_LongHexNumber and must stay below 0x80000000.
    public void Renumber(List<(XElement Paragraph, string Id)> paragraphs)
    {
        foreach (var (element, oldId) in paragraphs)
        {
            lastId++;
            var newId = lastId.ToString("X8");
            mapping.TryAdd(oldId, newId);
            element.SetAttributeValue(paraId, newId);
        }
    }

    public void Remap(XElement root)
    {
        if (mapping.Count == 0)
        {
            return;
        }

        foreach (var element in root.DescendantsAndSelf())
        {
            for (var attribute = element.FirstAttribute; attribute != null; attribute = attribute.NextAttribute)
            {
                if (references.Contains(attribute.Name) &&
                    mapping.TryGetValue(attribute.Value, out var newId))
                {
                    attribute.Value = newId;
                }
            }
        }
    }
}

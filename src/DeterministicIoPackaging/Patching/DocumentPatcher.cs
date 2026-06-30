class DocumentPatcher(DocumentRelationshipPatcher relsPatcher) : IExactMatchPatcher
{
    static XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    static XNamespace pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    static XName wpDocPr = wp + "docPr";
    static XName picCNvPr = pic + "cNvPr";

    public string ExactMatch => "word/document.xml";

    public bool IsMatch(Entry entry) =>
        entry.FullName is "word/document.xml";

    public void PatchXml(XDocument xml, string entryName)
    {
        var root = xml.Root!;

        // Strip revision markers and collect the drawing-id attributes in a
        // single tree traversal. word/document.xml is the largest content part,
        // so fusing these two passes avoids walking it twice. Ordering is
        // preserved: all wp:docPr ids first, then pic:cNvPr ids, numbering
        // continuing from where the docPrs left off. Storing the XAttribute
        // directly avoids a redundant Attribute("id") lookup during renumbering.
        var docPrIds = new List<XAttribute>();
        var picIds = new List<XAttribute>();
        WordRevisionMarkers.StripAndCollectDrawingIds(root, wpDocPr, picCNvPr, docPrIds, picIds);

        var index = 1;
        foreach (var attr in docPrIds)
        {
            attr.Value = index.ToString();
            index++;
        }

        foreach (var attr in picIds)
        {
            attr.Value = index.ToString();
            index++;
        }

        if (relsPatcher.IdMapping.Count > 0)
        {
            RelationshipRenumber.RemapIds(xml, relsPatcher.IdMapping);
        }
    }
}

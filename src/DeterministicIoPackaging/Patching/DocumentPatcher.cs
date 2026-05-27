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

        WordRevisionMarkers.Strip(xml);

        // Collect id attributes in one Descendants() walk rather than two.
        // Preserves the original ordering: all wp:docPr ids first, then
        // pic:cNvPr ids, numbering continuing from where the docPrs left off.
        // Storing the XAttribute directly avoids a redundant Attribute("id")
        // lookup during renumbering.
        var docPrIds = new List<XAttribute>();
        var picIds = new List<XAttribute>();
        foreach (var element in root.Descendants())
        {
            var name = element.Name;
            if (name == wpDocPr)
            {
                docPrIds.Add(element.Attribute("id")!);
            }
            else if (name == picCNvPr)
            {
                picIds.Add(element.Attribute("id")!);
            }
        }

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

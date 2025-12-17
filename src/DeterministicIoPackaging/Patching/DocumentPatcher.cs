class DocumentPatcher : IPatcher
{
    static XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    static XNamespace pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    static XName wpDocPr = wp + "docPr";
    static XName picCNvPr = pic + "cNvPr";

    public bool IsMatch(Entry entry) =>
        entry.FullName is "word/document.xml";

    public void PatchXml(XDocument xml)
    {
        var root = xml.Root!;

        // Find all elements with id attributes that need normalization
        var elementsWithIds = new List<XElement>();
        elementsWithIds.AddRange(root.Descendants(wpDocPr));
        elementsWithIds.AddRange(root.Descendants(picCNvPr));

        // Renumber all id attributes deterministically
        for (var i = 0; i < elementsWithIds.Count; i++)
        {
            // Use index + 1 for 1-based numbering (common in Office Open XML)
            elementsWithIds[i].Attribute("id")!.Value = (i + 1).ToString();
        }
    }
}

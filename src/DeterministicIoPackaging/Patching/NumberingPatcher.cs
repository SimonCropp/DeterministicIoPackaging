class NumberingPatcher : IPatcher
{
    static XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static XName nsid = w + "nsid";
    static XName abstractNum = w + "abstractNum";
    static XName num = w + "num";

    public bool IsMatch(Entry entry) =>
        entry.FullName is "word/numbering.xml";

    public void PatchXml(XDocument xml)
    {
        var root = xml.Root!;

        // Remove all w:nsid elements
        var nsidElements = root.Descendants(nsid).ToList();
        foreach (var element in nsidElements)
        {
            element.Remove();
        }

        // Sort abstractNum elements by their content to ensure deterministic order
        var abstractNums = root.Elements(abstractNum).ToList();
        var sortedAbstractNums = abstractNums
            .OrderBy(_ => _.ToString())
            .ToList();

        // Create a mapping from old abstractNumId to new abstractNumId
        var idMapping = new Dictionary<string, string>();
        for (var i = 0; i < abstractNums.Count; i++)
        {
            var oldId = abstractNums[i].Attribute(w + "abstractNumId")?.Value;
            var newId = i.ToString();
            if (oldId != null)
            {
                idMapping[oldId] = newId;
            }
        }

        // Update abstractNumId attributes
        foreach (var (element, index) in sortedAbstractNums.Select((e, i) => (e, i)))
        {
            element.Attribute(w + "abstractNumId")!.Value = index.ToString();
        }

        // Replace abstractNum elements with sorted ones
        var nums = root.Elements(num).ToList();
        root.ReplaceAll(sortedAbstractNums.Concat(nums));

        // Update references in num elements
        foreach (var numElement in root.Elements(num))
        {
            var abstractNumIdElement = numElement.Element(w + "abstractNumId");
            if (abstractNumIdElement != null)
            {
                var oldId = abstractNumIdElement.Attribute(w + "val")?.Value;
                if (oldId != null && idMapping.TryGetValue(oldId, out var newId))
                {
                    abstractNumIdElement.Attribute(w + "val")!.Value = newId;
                }
            }
        }
    }
}

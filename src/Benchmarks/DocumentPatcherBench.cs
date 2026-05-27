[MemoryDiagnoser]
public class DocumentPatcherBench
{
    XDocument source = null!;
    DocumentRelationshipPatcher relsPatcher = null!;

    [Params(500, 2000)]
    public int Paragraphs { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        source = SampleXml.BuildWordDocument(Paragraphs, drawings: 300, hyperlinks: 200);
        relsPatcher = new();
        var mapping = SampleXml.BuildRIdMapping(hyperlinks: 200);
        // Populate the rels patcher's mapping directly so DocumentPatcher exercises RemapIds too.
        foreach (var kvp in mapping)
        {
            relsPatcher.IdMapping[kvp.Key] = kvp.Value;
        }
    }

    [Benchmark]
    public void Patch()
    {
        var doc = new XDocument(source);
        new DocumentPatcher(relsPatcher).PatchXml(doc, "word/document.xml");
    }
}

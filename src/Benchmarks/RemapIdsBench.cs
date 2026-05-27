[MemoryDiagnoser]
public class RemapIdsBench
{
    XDocument source = null!;
    Dictionary<string, string> mapping = null!;

    [Params(500, 2000)]
    public int Paragraphs { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        source = SampleXml.BuildWordDocument(Paragraphs, drawings: 200, hyperlinks: 200);
        mapping = SampleXml.BuildRIdMapping(hyperlinks: 200);
    }

    [Benchmark]
    public void RemapIds()
    {
        var doc = new XDocument(source);
        RelationshipRenumber.RemapIds(doc, mapping);
    }
}

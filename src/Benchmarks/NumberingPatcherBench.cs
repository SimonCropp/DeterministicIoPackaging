[MemoryDiagnoser]
public class NumberingPatcherBench
{
    XDocument source = null!;

    [Params(20, 100)]
    public int AbstractNums { get; set; }

    [GlobalSetup]
    public void Setup() =>
        source = SampleXml.BuildNumbering(AbstractNums, namespaceDecls: 8);

    [Benchmark]
    public void Patch()
    {
        var doc = new XDocument(source);
        new NumberingPatcher().PatchXml(doc, "word/numbering.xml");
    }
}

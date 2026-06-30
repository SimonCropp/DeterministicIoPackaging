[MemoryDiagnoser]
public class ConvertEndToEndBench
{
    byte[] sourceBytes = null!;

    [Params(500, 2000)]
    public int Paragraphs { get; set; }

    [GlobalSetup]
    public void Setup() =>
        sourceBytes = SampleXml.BuildDocxZip(Paragraphs, drawings: 200, hyperlinks: 200);

    [Benchmark]
    public MemoryStream Convert()
    {
        using var source = new MemoryStream(sourceBytes, writable: false);
        return DeterministicPackage.Convert(source);
    }
}

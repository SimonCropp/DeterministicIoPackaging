[MemoryDiagnoser]
public class StripRevisionMarkersBench
{
    XDocument source = null!;

    [Params(500, 2000)]
    public int Paragraphs { get; set; }

    [GlobalSetup]
    public void Setup() =>
        source = SampleXml.BuildWordDocument(Paragraphs, drawings: 0, hyperlinks: 0);

    [Benchmark]
    public void Strip()
    {
        // Clone per-iteration so the in-place mutation doesn't poison the source.
        var doc = new XDocument(source);
        WordRevisionMarkers.Strip(doc);
    }
}

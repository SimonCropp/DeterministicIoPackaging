[MemoryDiagnoser]
public class PngNormalizerBench
{
    byte[] png = null!;

    [Params(64 * 1024, 1024 * 1024)]
    public int ImageBytes { get; set; }

    [GlobalSetup]
    public void Setup() =>
        png = SampleXml.BuildPng(ImageBytes);

    [Benchmark]
    public void Normalize()
    {
        using var source = new MemoryStream(png, writable: false);
        using var target = new MemoryStream();
        PngNormalizer.Normalize(source, target);
    }
}

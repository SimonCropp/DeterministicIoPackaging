// https://github.com/SimonCropp/Determinize/issues/21
public class MediaEntryTests
{
    // PowerPoint sometimes saves a JPEG under a .png name. The entry must pass through untouched
    // rather than being parsed as PNG chunks (which read the JFIF bytes as a ~1.2 GB chunk length).
    [Test]
    public async Task JpegNamedPngPassesThrough()
    {
        var jpeg = BuildJpegLikeBytes();
        using var source = BuildZip(("ppt/media/image5.png", jpeg));

        using var result = DeterministicPackage.Convert(source);

        await Assert.That(ReadEntry(result, "ppt/media/image5.png")).IsEquivalentTo(jpeg, CollectionOrdering.Matching);
    }

    [Test]
    public async Task JpegNamedPngPassesThroughAsync()
    {
        var jpeg = BuildJpegLikeBytes();
        using var source = BuildZip(("ppt/media/image5.png", jpeg));

        using var result = await DeterministicPackage.ConvertAsync(source);

        await Assert.That(ReadEntry(result, "ppt/media/image5.png")).IsEquivalentTo(jpeg, CollectionOrdering.Matching);
    }

    [Test]
    public async Task JpegNamedPngReportsNoChange()
    {
        using var source = BuildZip(("ppt/media/image5.png", BuildJpegLikeBytes()));
        using var first = DeterministicPackage.Convert(source);

        using var second = DeterministicPackage.Convert(first, out var changes);

        await Assert.That(changes).IsEmpty();
    }

    [Test]
    public async Task FailureNamesTheEntry()
    {
        // A PNG signature followed by a chunk length that runs past the end.
        byte[] corrupt = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x7F, 0xFF, 0xFF, 0xFF, (byte) 'I', (byte) 'H', (byte) 'D', (byte) 'R'];
        using var source = BuildZip(("ppt/media/broken.png", corrupt));

        var exception = Assert.Throws<InvalidDataException>(() => DeterministicPackage.Convert(source));

        await Assert.That(exception.Message).Contains("ppt/media/broken.png");
    }

    // A large, poorly compressible media entry used to be streamed straight from the source's
    // inflate stream into the target's deflate stream, so the deflater saw whatever read sizes the
    // inflater happened to yield — which depend on the source's compressed form. The output was then
    // a function of the previous output, and repeated conversions never settled.
    [Test]
    public async Task LargeMediaEntryIsStableAcrossRepeatedConversions()
    {
        var media = BuildNoisyMedia(3_000_000);
        using var source = BuildZip(("ppt/media/image13.tiff", media));

        using var first = DeterministicPackage.Convert(source);
        using var second = DeterministicPackage.Convert(first);
        using var third = DeterministicPackage.Convert(second);

        await Assert.That(second.ToArray()).IsEquivalentTo(first.ToArray(), CollectionOrdering.Matching);
        await Assert.That(third.ToArray()).IsEquivalentTo(first.ToArray(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task LargeMediaEntryIsStableAcrossRepeatedConversionsAsync()
    {
        var media = BuildNoisyMedia(3_000_000);
        using var source = BuildZip(("ppt/media/image13.tiff", media));

        using var first = await DeterministicPackage.ConvertAsync(source);
        using var second = await DeterministicPackage.ConvertAsync(first);
        using var third = await DeterministicPackage.ConvertAsync(second);

        await Assert.That(second.ToArray()).IsEquivalentTo(first.ToArray(), CollectionOrdering.Matching);
        await Assert.That(third.ToArray()).IsEquivalentTo(first.ToArray(), CollectionOrdering.Matching);
    }

    // The same content compressed differently in the source must produce the same output.
    [Test]
    public async Task LargeMediaEntryIndependentOfSourceCompression()
    {
        var media = BuildNoisyMedia(3_000_000);
        using var fastest = BuildZip(CompressionLevel.Fastest, ("ppt/media/image13.tiff", media));
        using var optimal = BuildZip(CompressionLevel.Optimal, ("ppt/media/image13.tiff", media));
        using var stored = BuildZip(CompressionLevel.NoCompression, ("ppt/media/image13.tiff", media));

        using var fromFastest = DeterministicPackage.Convert(fastest);
        using var fromOptimal = DeterministicPackage.Convert(optimal);
        using var fromStored = DeterministicPackage.Convert(stored);

        await Assert.That(fromOptimal.ToArray()).IsEquivalentTo(fromFastest.ToArray(), CollectionOrdering.Matching);
        await Assert.That(fromStored.ToArray()).IsEquivalentTo(fromFastest.ToArray(), CollectionOrdering.Matching);
    }

    static byte[] BuildJpegLikeBytes()
    {
        // SOI + APP0 JFIF header, then filler and EOI. Enough to trip a PNG parser.
        byte[] header =
        [
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte) 'J', (byte) 'F', (byte) 'I', (byte) 'F', 0x00,
            0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00
        ];
        var bytes = new byte[header.Length + 1000 + 2];
        header.CopyTo(bytes, 0);
        var random = new Random(1);
        for (var i = header.Length; i < bytes.Length - 2; i++)
        {
            bytes[i] = (byte) random.Next(256);
        }

        bytes[^2] = 0xFF;
        bytes[^1] = 0xD9;
        return bytes;
    }

    // Alternating incompressible and highly compressible runs, which .NET's deflater (zlib-ng) is
    // sensitive to: the same bytes written in different chunk sizes give different output.
    static byte[] BuildNoisyMedia(int length)
    {
        var random = new Random(42);
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (i / 50000) % 2 == 0 ? (byte) random.Next(256) : (byte) (i % 13);
        }

        return bytes;
    }

    static MemoryStream BuildZip(params (string name, byte[] content)[] entries) =>
        BuildZip(CompressionLevel.Optimal, entries);

    static MemoryStream BuildZip(CompressionLevel level, params (string name, byte[] content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new Archive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name, level);
                using var entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }
        }

        stream.Position = 0;
        return stream;
    }

    static byte[] ReadEntry(MemoryStream zip, string name)
    {
        zip.Position = 0;
        using var archive = new Archive(zip, ZipArchiveMode.Read, leaveOpen: true);
        using var entryStream = archive.GetEntry(name)!.Open();
        using var buffer = new MemoryStream();
        entryStream.CopyTo(buffer);
        return buffer.ToArray();
    }
}

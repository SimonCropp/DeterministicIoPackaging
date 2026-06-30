[TestFixture]
public class ZipPlatformNormalizerTests
{
    // Simulates an archive produced on Unix (host byte 3, Unix mode bits in the
    // external attributes) and asserts the normalizer rewrites both to the
    // Windows/FAT-neutral values. This would fail before the normalizer existed.
    [Test]
    public void RewritesUnixHostByteAndExternalAttributes()
    {
        var archive = BuildArchive();
        var buffer = archive.GetBuffer();
        var length = (int) archive.Length;

        foreach (var record in CentralDirectoryRecords(buffer, length))
        {
            // host OS = Unix
            buffer[record + 5] = 3;
            // external attributes carrying Unix mode 0100644 in the high word
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(record + 38), 0x81A4_0000);
        }

        ZipPlatformNormalizer.Normalize(archive);

        AssertNormalized(archive);
    }

    // The end-to-end guarantee: whatever OS runs the conversion, the central
    // directory comes out OS-independent.
    [Test]
    public void ConvertProducesOsIndependentCentralDirectory()
    {
        using var result = DeterministicPackage.Convert(BuildArchive());

        AssertNormalized(result);
    }

    // The low byte of "version made by" encodes the spec version (a function of
    // the features used, not the OS) and must be left alone.
    [Test]
    public void PreservesSpecVersionLowByte()
    {
        var archive = BuildArchive();
        var buffer = archive.GetBuffer();
        var length = (int) archive.Length;

        var before = CentralDirectoryRecords(buffer, length)
            .Select(_ => buffer[_ + 4])
            .ToList();

        ZipPlatformNormalizer.Normalize(archive);

        var after = CentralDirectoryRecords(buffer, length)
            .Select(_ => buffer[_ + 4])
            .ToList();

        Assert.That(after, Is.EqualTo(before));
    }

    static void AssertNormalized(MemoryStream archive)
    {
        var buffer = archive.GetBuffer();
        var length = (int) archive.Length;
        var records = CentralDirectoryRecords(buffer, length);

        Assert.That(records, Is.Not.Empty);
        foreach (var record in records)
        {
            Assert.Multiple(() =>
            {
                Assert.That(buffer[record + 5], Is.EqualTo(0),
                    "host-OS byte must be normalized to 0");
                Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(record + 38)), Is.EqualTo(0u),
                    "external file attributes must be cleared");
            });
        }
    }

    static MemoryStream BuildArchive()
    {
        var stream = new MemoryStream();
        using (var archive = new Archive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in (string[]) ["alpha.txt", "beta.txt", "nested/gamma.txt"])
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                writer.Write("payload");
            }
        }

        stream.Position = 0;
        return stream;
    }

    // Walks the central directory via the EOCD record, returning the byte offset
    // of each central-directory file header.
    static List<int> CentralDirectoryRecords(byte[] buffer, int length)
    {
        var eocd = -1;
        for (var i = length - 22; i >= 0; i--)
        {
            if (buffer[i] == 0x50 &&
                buffer[i + 1] == 0x4B &&
                buffer[i + 2] == 0x05 &&
                buffer[i + 3] == 0x06)
            {
                eocd = i;
                break;
            }
        }

        Assert.That(eocd, Is.GreaterThanOrEqualTo(0), "EOCD record not found");

        var count = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(eocd + 10));
        var offset = (int) BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(eocd + 16));
        var records = new List<int>();
        for (var i = 0; i < count; i++)
        {
            records.Add(offset);
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset + 28));
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset + 30));
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset + 32));
            offset += 46 + nameLength + extraLength + commentLength;
        }

        return records;
    }
}

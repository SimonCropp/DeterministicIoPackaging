public class ZipPlatformNormalizerTests
{
    // Simulates an archive produced on Unix (host byte 3, Unix mode bits in the
    // external attributes) and asserts the normalizer rewrites both to the
    // Windows/FAT-neutral values. This would fail before the normalizer existed.
    [Test]
    public Task RewritesUnixHostByteAndExternalAttributes()
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

        return AssertNormalized(archive.ToArray());
    }

    // The end-to-end guarantee: whatever OS runs the conversion, the central
    // directory comes out OS-independent.
    [Test]
    public async Task ConvertProducesOsIndependentCentralDirectory()
    {
        using var result = await DeterministicPackage.ConvertAsync(BuildArchive());

        await AssertNormalized(result.ToArray());
    }

    // The low byte of "version made by" encodes the spec version (a function of
    // the features used, not the OS) and must be left alone.
    [Test]
    public async Task PreservesSpecVersionLowByte()
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

        await Assert.That(after).IsEquivalentTo(before, CollectionOrdering.Matching);
    }

    static async Task AssertNormalized(byte[] archive)
    {
        var records = CentralDirectoryRecords(archive, archive.Length);

        await Assert.That(records).IsNotEmpty();
        foreach (var record in records)
        {
            using (Assert.Multiple())
            {
                await Assert.That(archive[record + 5]).IsEqualTo((byte) 0).Because("host-OS byte must be normalized to 0");
                await Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(archive.AsSpan(record + 38))).IsEqualTo(0u).Because("external file attributes must be cleared");
            }
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

        if (eocd < 0)
        {
            throw new("EOCD record not found");
        }

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

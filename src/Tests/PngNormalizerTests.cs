using System.Buffers.Binary;
using System.IO.Compression;

[TestFixture]
public class PngNormalizerTests
{
    [Test]
    public void OutputIsValidPng()
    {
        var png = BuildPng(CompressionLevel.Fastest);

        var result = Normalize(png);

        AssertValidPng(result);
    }

    [Test]
    public void Deterministic()
    {
        var png = BuildPng(CompressionLevel.Fastest);

        var result1 = Normalize(png);
        var result2 = Normalize(png);

        Assert.That(result1, Is.EqualTo(result2));
    }

    [Test]
    public void DifferentCompressionLevelsProduceSameOutput()
    {
        var fastest = BuildPng(CompressionLevel.Fastest);
        var optimal = BuildPng(CompressionLevel.Optimal);

        var result1 = Normalize(fastest);
        var result2 = Normalize(optimal);

        Assert.That(result1, Is.EqualTo(result2));
    }

    [Test]
    public void MultipleIdatChunksProduceSameOutputAsSingle()
    {
        var singleIdat = BuildPng(CompressionLevel.Fastest);
        var multiIdat = BuildPngWithSplitIdat();

        var result1 = Normalize(singleIdat);
        var result2 = Normalize(multiIdat);

        Assert.That(result1, Is.EqualTo(result2));
    }

    [Test]
    public void PreservesNonIdatChunks()
    {
        var png = BuildPngWithTextChunk("Test", "Value");
        var result = Normalize(png);

        AssertValidPng(result);
        Assert.That(FindChunk(result, "tEXt"), Is.Not.Null);
    }

    [Test]
    public async Task AsyncProducesSameOutputAsSync()
    {
        var png = BuildPng(CompressionLevel.Fastest);

        var syncResult = Normalize(png);

        using var source = new MemoryStream(png);
        using var target = new MemoryStream();
        await PngNormalizer.NormalizeAsync(source, target, Cancel.None);
        var asyncResult = target.ToArray();

        Assert.That(asyncResult, Is.EqualTo(syncResult));
    }

    [Test]
    public void PackageConvertNormalizesPng()
    {
        var png = BuildPng(CompressionLevel.Fastest);

        using var zipSource = new MemoryStream();
        using (var archive = new Archive(zipSource, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/media/image1.png");
            using var entryStream = entry.Open();
            entryStream.Write(png, 0, png.Length);
        }

        zipSource.Position = 0;
        using var result1 = DeterministicPackage.Convert(zipSource);

        var png2 = BuildPng(CompressionLevel.Optimal);
        using var zipSource2 = new MemoryStream();
        using (var archive = new Archive(zipSource2, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/media/image1.png");
            using var entryStream = entry.Open();
            entryStream.Write(png2, 0, png2.Length);
        }

        zipSource2.Position = 0;
        using var result2 = DeterministicPackage.Convert(zipSource2);

        Assert.That(result1.ToArray(), Is.EqualTo(result2.ToArray()));
    }

    static byte[] Normalize(byte[] png)
    {
        using var source = new MemoryStream(png);
        using var target = new MemoryStream();
        PngNormalizer.Normalize(source, target);
        return target.ToArray();
    }

    static readonly byte[] pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    static readonly byte[] idatType = "IDAT"u8.ToArray();
    static readonly byte[] ihdrType = "IHDR"u8.ToArray();
    static readonly byte[] iendType = "IEND"u8.ToArray();
    static readonly byte[] textType = "tEXt"u8.ToArray();

    static void AssertValidPng(byte[] data)
    {
        Assert.That(data.Length, Is.GreaterThanOrEqualTo(8));
        Assert.That(data.AsSpan(0, 8).ToArray(), Is.EqualTo(pngSignature));

        var offset = 8;
        var foundIhdr = false;
        var foundIdat = false;
        var foundIend = false;

        while (offset + 12 <= data.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
            var type = Encoding.ASCII.GetString(data, offset + 4, 4);
            var totalSize = 12 + length;

            var expectedCrc = ComputeCrc32(data.AsSpan(offset + 4, 4 + length));
            var actualCrc = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8 + length));
            Assert.That(actualCrc, Is.EqualTo(expectedCrc), $"CRC mismatch for chunk {type}");

            switch (type)
            {
                case "IHDR":
                    foundIhdr = true;
                    break;
                case "IDAT":
                    foundIdat = true;
                    break;
                case "IEND":
                    foundIend = true;
                    break;
            }

            offset += totalSize;
        }

        Assert.That(foundIhdr, Is.True, "Missing IHDR chunk");
        Assert.That(foundIdat, Is.True, "Missing IDAT chunk");
        Assert.That(foundIend, Is.True, "Missing IEND chunk");
    }

    static byte[]? FindChunk(byte[] data, string chunkType)
    {
        var offset = 8;
        while (offset + 12 <= data.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
            var type = Encoding.ASCII.GetString(data, offset + 4, 4);

            if (type == chunkType)
            {
                return data.AsSpan(offset + 8, length).ToArray();
            }

            offset += 12 + length;
        }

        return null;
    }

    static byte[] BuildPng(CompressionLevel level)
    {
        using var ms = new MemoryStream();
        ms.Write(pngSignature, 0, pngSignature.Length);

        var ihdrData = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdrData.AsSpan(0), 2); // width
        BinaryPrimitives.WriteInt32BigEndian(ihdrData.AsSpan(4), 2); // height
        ihdrData[8] = 8; // bit depth
        ihdrData[9] = 2; // color type: RGB
        WriteChunk(ms, ihdrType, ihdrData);

        byte[] rawPixels =
        [
            0, 255, 0, 0, 255, 0, 0, // row 1: filter=0, red, red
            0, 255, 0, 0, 255, 0, 0 // row 2: filter=0, red, red
        ];

        WriteChunk(ms, idatType, ZlibCompress(rawPixels, level));
        WriteChunk(ms, iendType, Array.Empty<byte>());

        return ms.ToArray();
    }

    static byte[] BuildPngWithSplitIdat()
    {
        using var ms = new MemoryStream();
        ms.Write(pngSignature, 0, pngSignature.Length);

        var ihdrData = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdrData.AsSpan(0), 2);
        BinaryPrimitives.WriteInt32BigEndian(ihdrData.AsSpan(4), 2);
        ihdrData[8] = 8;
        ihdrData[9] = 2;
        WriteChunk(ms, ihdrType, ihdrData);

        byte[] rawPixels =
        [
            0, 255, 0, 0, 255, 0, 0,
            0, 255, 0, 0, 255, 0, 0
        ];

        var zlibData = ZlibCompress(rawPixels, CompressionLevel.Fastest);
        var mid = zlibData.Length / 2;
        WriteChunk(ms, idatType, zlibData.AsSpan(0, mid).ToArray());
        WriteChunk(ms, idatType, zlibData.AsSpan(mid).ToArray());
        WriteChunk(ms, iendType, Array.Empty<byte>());

        return ms.ToArray();
    }

    static byte[] BuildPngWithTextChunk(string keyword, string text)
    {
        using var ms = new MemoryStream();
        ms.Write(pngSignature, 0, pngSignature.Length);

        var ihdrData = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdrData.AsSpan(0), 1);
        BinaryPrimitives.WriteInt32BigEndian(ihdrData.AsSpan(4), 1);
        ihdrData[8] = 8;
        ihdrData[9] = 2;
        WriteChunk(ms, ihdrType, ihdrData);

        var latin1 = Encoding.GetEncoding(28591);
        var keywordBytes = latin1.GetBytes(keyword);
        var textBytes = latin1.GetBytes(text);
        var textChunkData = new byte[keywordBytes.Length + 1 + textBytes.Length];
        keywordBytes.CopyTo(textChunkData, 0);
        textChunkData[keywordBytes.Length] = 0;
        textBytes.CopyTo(textChunkData, keywordBytes.Length + 1);
        WriteChunk(ms, textType, textChunkData);

        WriteChunk(ms, idatType, ZlibCompress([0, 255, 0, 0], CompressionLevel.Fastest));
        WriteChunk(ms, iendType, Array.Empty<byte>());

        return ms.ToArray();
    }

    static byte[] ZlibCompress(byte[] data, CompressionLevel level)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, level, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    static void WriteChunk(Stream stream, byte[] type, byte[] data)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, data.Length);
        stream.Write(header, 0, 4);
        stream.Write(type, 0, 4);
        stream.Write(data, 0, data.Length);

        var crc = ComputeCrc32(type, data);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes, 0, 4);
    }

    static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc = crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }

    static uint ComputeCrc32(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFF;
        foreach (var b in type)
        {
            crc = crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        foreach (var b in data)
        {
            crc = crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }

    static readonly uint[] crc32Table = GenerateCrc32Table();

    static uint[] GenerateCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var crc = i;
            for (var j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }

            table[i] = crc;
        }

        return table;
    }
}

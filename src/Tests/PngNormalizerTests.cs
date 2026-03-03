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
        var result1 = DeterministicPackage.Convert(zipSource);

        // rebuild with different compression
        var png2 = BuildPng(CompressionLevel.Optimal);
        using var zipSource2 = new MemoryStream();
        using (var archive = new Archive(zipSource2, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/media/image1.png");
            using var entryStream = entry.Open();
            entryStream.Write(png2, 0, png2.Length);
        }

        zipSource2.Position = 0;
        var result2 = DeterministicPackage.Convert(zipSource2);

        Assert.That(result1.ToArray(), Is.EqualTo(result2.ToArray()));
    }

    static byte[] Normalize(byte[] png)
    {
        using var source = new MemoryStream(png);
        using var target = new MemoryStream();
        PngNormalizer.Normalize(source, target);
        return target.ToArray();
    }

    static readonly byte[] expectedSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    static void AssertValidPng(byte[] data)
    {
        Assert.That(data.Length, Is.GreaterThanOrEqualTo(8));

        var header = new byte[8];
        Array.Copy(data, 0, header, 0, 8);
        Assert.That(header, Is.EqualTo(expectedSignature));

        // Walk chunks, verify CRCs
        var offset = 8;
        var foundIhdr = false;
        var foundIdat = false;
        var foundIend = false;

        while (offset + 12 <= data.Length)
        {
            var length = ReadInt32BigEndian(data, offset);
            var type = Encoding.ASCII.GetString(data, offset + 4, 4);
            var totalSize = 12 + length;

            // Verify CRC over type + data
            var expectedCrc = ComputeCrc32(data, offset + 4, 4 + length);
            var actualCrc = ReadUInt32BigEndian(data, offset + 8 + length);
            Assert.That(actualCrc, Is.EqualTo(expectedCrc), $"CRC mismatch for chunk {type}");

            if (type == "IHDR")
            {
                foundIhdr = true;
            }

            if (type == "IDAT")
            {
                foundIdat = true;
            }

            if (type == "IEND")
            {
                foundIend = true;
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
            var length = ReadInt32BigEndian(data, offset);
            var type = Encoding.ASCII.GetString(data, offset + 4, 4);

            if (type == chunkType)
            {
                var chunkData = new byte[length];
                Array.Copy(data, offset + 8, chunkData, 0, length);
                return chunkData;
            }

            offset += 12 + length;
        }

        return null;
    }

    static readonly byte[] pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // Build a minimal 2x2 red PNG with the given compression level
    static byte[] BuildPng(CompressionLevel level)
    {
        using var ms = new MemoryStream();

        ms.Write(pngSignature, 0, pngSignature.Length);

        // IHDR: 2x2, 8-bit RGB
        var ihdrData = new byte[13];
        WriteInt32BigEndian(ihdrData, 0, 2); // width
        WriteInt32BigEndian(ihdrData, 4, 2); // height
        ihdrData[8] = 8; // bit depth
        ihdrData[9] = 2; // color type: RGB
        WriteChunk(ms, "IHDR", ihdrData);

        // Pixel data: 2 rows, each with filter byte (0) + 3 bytes per pixel * 2 pixels
        var rawPixels = new byte[]
        {
            0, 255, 0, 0, 255, 0, 0, // row 1: filter=0, red, red
            0, 255, 0, 0, 255, 0, 0 // row 2: filter=0, red, red
        };

        var zlibData = ZlibCompress(rawPixels, level);
        WriteChunk(ms, "IDAT", zlibData);

        WriteChunk(ms, "IEND", Array.Empty<byte>());

        return ms.ToArray();
    }

    // Build a PNG with IDAT data split across multiple chunks
    static byte[] BuildPngWithSplitIdat()
    {
        using var ms = new MemoryStream();

        ms.Write(pngSignature, 0, pngSignature.Length);

        var ihdrData = new byte[13];
        WriteInt32BigEndian(ihdrData, 0, 2);
        WriteInt32BigEndian(ihdrData, 4, 2);
        ihdrData[8] = 8;
        ihdrData[9] = 2;
        WriteChunk(ms, "IHDR", ihdrData);

        var rawPixels = new byte[]
        {
            0, 255, 0, 0, 255, 0, 0,
            0, 255, 0, 0, 255, 0, 0
        };

        var zlibData = ZlibCompress(rawPixels, CompressionLevel.Fastest);

        // Split into two IDAT chunks
        var mid = zlibData.Length / 2;
        var firstHalf = new byte[mid];
        var secondHalf = new byte[zlibData.Length - mid];
        Array.Copy(zlibData, 0, firstHalf, 0, mid);
        Array.Copy(zlibData, mid, secondHalf, 0, zlibData.Length - mid);
        WriteChunk(ms, "IDAT", firstHalf);
        WriteChunk(ms, "IDAT", secondHalf);

        WriteChunk(ms, "IEND", Array.Empty<byte>());

        return ms.ToArray();
    }

    static byte[] BuildPngWithTextChunk(string keyword, string text)
    {
        using var ms = new MemoryStream();

        ms.Write(pngSignature, 0, pngSignature.Length);

        var ihdrData = new byte[13];
        WriteInt32BigEndian(ihdrData, 0, 1);
        WriteInt32BigEndian(ihdrData, 4, 1);
        ihdrData[8] = 8;
        ihdrData[9] = 2;
        WriteChunk(ms, "IHDR", ihdrData);

        // tEXt chunk: keyword + null separator + text
        var keywordBytes = Encoding.ASCII.GetBytes(keyword);
        var textBytes = Encoding.ASCII.GetBytes(text);
        var textChunkData = new byte[keywordBytes.Length + 1 + textBytes.Length];
        Array.Copy(keywordBytes, textChunkData, keywordBytes.Length);
        textChunkData[keywordBytes.Length] = 0;
        Array.Copy(textBytes, 0, textChunkData, keywordBytes.Length + 1, textBytes.Length);
        WriteChunk(ms, "tEXt", textChunkData);

        var rawPixels = new byte[]
        {
            0, 255, 0, 0 // 1x1 RGB: filter=0, red
        };

        var zlibData = ZlibCompress(rawPixels, CompressionLevel.Fastest);
        WriteChunk(ms, "IDAT", zlibData);

        WriteChunk(ms, "IEND", Array.Empty<byte>());

        return ms.ToArray();
    }

    static byte[] ZlibCompress(byte[] data, CompressionLevel level)
    {
        using var output = new MemoryStream();
        output.WriteByte(0x78);
        output.WriteByte(0x9C);

        using (var deflate = new DeflateStream(output, level, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }

        // Adler-32
        uint a = 1, b = 0;
        foreach (var val in data)
        {
            a = (a + val) % 65521;
            b = (b + a) % 65521;
        }

        var adler = (b << 16) | a;
        output.WriteByte((byte) (adler >> 24));
        output.WriteByte((byte) (adler >> 16));
        output.WriteByte((byte) (adler >> 8));
        output.WriteByte((byte) adler);

        return output.ToArray();
    }

    static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var lengthBytes = new byte[4];
        WriteInt32BigEndian(lengthBytes, 0, data.Length);
        stream.Write(lengthBytes, 0, 4);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes, 0, 4);
        stream.Write(data, 0, data.Length);

        var typeAndData = new byte[4 + data.Length];
        Array.Copy(typeBytes, 0, typeAndData, 0, 4);
        Array.Copy(data, 0, typeAndData, 4, data.Length);
        var crc = ComputeCrc32(typeAndData, 0, typeAndData.Length);
        var crcBytes = new byte[4];
        WriteUInt32BigEndian(crcBytes, 0, crc);
        stream.Write(crcBytes, 0, 4);
    }

    static int ReadInt32BigEndian(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    static uint ReadUInt32BigEndian(byte[] data, int offset) =>
        (uint) ((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);

    static void WriteInt32BigEndian(byte[] data, int offset, int value)
    {
        data[offset] = (byte) (value >> 24);
        data[offset + 1] = (byte) (value >> 16);
        data[offset + 2] = (byte) (value >> 8);
        data[offset + 3] = (byte) value;
    }

    static void WriteUInt32BigEndian(byte[] data, int offset, uint value)
    {
        data[offset] = (byte) (value >> 24);
        data[offset + 1] = (byte) (value >> 16);
        data[offset + 2] = (byte) (value >> 8);
        data[offset + 3] = (byte) value;
    }

    static uint ComputeCrc32(byte[] data, int offset, int length)
    {
        var crc = 0xFFFFFFFF;
        for (var i = offset; i < offset + length; i++)
        {
            crc = crc32Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
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

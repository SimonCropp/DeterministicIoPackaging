using System.Buffers.Binary;

namespace DeterministicIoPackaging;

static class PngNormalizer
{
    static readonly byte[] pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    static readonly byte[] idatType = "IDAT"u8.ToArray();

    public static void Normalize(Stream source, Stream target)
    {
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        Normalize(buffer.GetBuffer(), (int) buffer.Length, target);
    }

    public static async Task NormalizeAsync(Stream source, Stream target, Cancel cancel)
    {
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancel);
        Normalize(buffer.GetBuffer(), (int) buffer.Length, target);
    }

    static void Normalize(byte[] data, int dataLength, Stream target)
    {
        target.Write(pngSignature, 0, pngSignature.Length);

        var idatData = new MemoryStream();
        var preIdatChunks = new List<byte[]>();
        var postIdatChunks = new List<byte[]>();
        var seenIdat = false;
        var offset = 8;

        while (offset + 12 <= dataLength)
        {
            var chunkLength = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
            var totalChunkSize = 4 + 4 + chunkLength + 4;

            var isIdat = data[offset + 4] == 'I' &&
                         data[offset + 5] == 'D' &&
                         data[offset + 6] == 'A' &&
                         data[offset + 7] == 'T';

            if (isIdat)
            {
                seenIdat = true;
                idatData.Write(data, offset + 8, chunkLength);
            }
            else
            {
                var rawChunk = new byte[totalChunkSize];
                Buffer.BlockCopy(data, offset, rawChunk, 0, totalChunkSize);

                if (seenIdat)
                {
                    postIdatChunks.Add(rawChunk);
                }
                else
                {
                    preIdatChunks.Add(rawChunk);
                }
            }

            offset += totalChunkSize;
        }

        foreach (var chunk in preIdatChunks)
        {
            target.Write(chunk, 0, chunk.Length);
        }

        if (idatData.Length > 0)
        {
            var zlibBytes = idatData.ToArray();

            byte[] decompressed;
            using (var zlibInput = new MemoryStream(zlibBytes))
            using (var zlibStream = new ZLibStream(zlibInput, CompressionMode.Decompress))
            using (var decompressedStream = new MemoryStream())
            {
                zlibStream.CopyTo(decompressedStream);
                decompressed = decompressedStream.ToArray();
            }

            byte[] newIdatData;
            using (var compressOutput = new MemoryStream())
            {
                using (var zlibStream = new ZLibStream(compressOutput, CompressionLevel.Optimal, leaveOpen: true))
                {
                    zlibStream.Write(decompressed, 0, decompressed.Length);
                }

                newIdatData = compressOutput.ToArray();
            }

            WriteChunk(target, idatType, newIdatData);
        }

        foreach (var chunk in postIdatChunks)
        {
            target.Write(chunk, 0, chunk.Length);
        }
    }

    static void WriteChunk(Stream target, byte[] type, byte[] data)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, data.Length);
        target.Write(header, 0, 4);
        target.Write(type, 0, 4);
        target.Write(data, 0, data.Length);

        var crc = 0xFFFFFFFF;
        for (var i = 0; i < 4; i++)
        {
            crc = crc32Table[(crc ^ type[i]) & 0xFF] ^ (crc >> 8);
        }

        for (var i = 0; i < data.Length; i++)
        {
            crc = crc32Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        }

        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xFFFFFFFF);
        target.Write(crcBytes, 0, 4);
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

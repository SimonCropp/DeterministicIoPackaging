using System.Buffers.Binary;
using System.IO.Hashing;

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
        target.Write(pngSignature);

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
            target.Write(chunk);
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
                    zlibStream.Write(decompressed);
                }

                newIdatData = compressOutput.ToArray();
            }

            WriteChunk(target, idatType, newIdatData);
        }

        foreach (var chunk in postIdatChunks)
        {
            target.Write(chunk);
        }
    }

    static void WriteChunk(Stream target, byte[] type, byte[] data)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, data.Length);
        target.Write(header);
        target.Write(type);
        target.Write(data);

        var crc = new Crc32();
        crc.Append(type);
        crc.Append(data);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc.GetCurrentHashAsUInt32());
        target.Write(crcBytes);
    }
}

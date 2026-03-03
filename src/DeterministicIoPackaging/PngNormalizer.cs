using System.Buffers.Binary;
using System.IO.Hashing;

namespace DeterministicIoPackaging;

static class PngNormalizer
{
    static readonly byte[] pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    static readonly byte[] idatType = "IDAT"u8.ToArray();

    public static void Normalize(Stream source, Stream target)
    {
        var header = new byte[8];
        source.ReadExactly(header, 0, 8);
        target.Write(pngSignature);

        using var idatData = new MemoryStream();
        var flushedIdat = false;

        while (true)
        {
            source.ReadExactly(header, 0, 8);
            var chunkLength = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4));

            var body = new byte[chunkLength + 4];
            source.ReadExactly(body, 0, body.Length);

            if (ProcessChunk(header, body, chunkLength, target, idatData, ref flushedIdat))
            {
                break;
            }
        }
    }

    public static async Task NormalizeAsync(Stream source, Stream target, Cancel cancel)
    {
        var header = new byte[8];
        await source.ReadExactlyAsync(header, 0, 8, cancel);
        target.Write(pngSignature);

        using var idatData = new MemoryStream();
        var flushedIdat = false;

        while (true)
        {
            await source.ReadExactlyAsync(header, 0, 8, cancel);
            var chunkLength = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4));

            var body = new byte[chunkLength + 4];
            await source.ReadExactlyAsync(body, 0, body.Length, cancel);

            if (ProcessChunk(header, body, chunkLength, target, idatData, ref flushedIdat))
            {
                break;
            }
        }
    }

    static bool ProcessChunk(byte[] header, byte[] body, int chunkLength, Stream target, MemoryStream idatData, ref bool flushedIdat)
    {
        var isIdat = header[4] == 'I' && header[5] == 'D' &&
                     header[6] == 'A' && header[7] == 'T';
        var isIend = header[4] == 'I' && header[5] == 'E' &&
                     header[6] == 'N' && header[7] == 'D';

        if (isIdat)
        {
            idatData.Write(body, 0, chunkLength);
        }
        else
        {
            if (!flushedIdat && idatData.Length > 0)
            {
                flushedIdat = true;
                WriteNormalizedIdat(target, idatData);
            }

            target.Write(header);
            target.Write(body);
        }

        return isIend;
    }

    static void WriteNormalizedIdat(Stream target, MemoryStream idatData)
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

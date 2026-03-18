using System.Buffers.Binary;

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
        var isIdat = header[4] == 'I' &&
                     header[5] == 'D' &&
                     header[6] == 'A' &&
                     header[7] == 'T';
        var isIend = header[4] == 'I' &&
                     header[5] == 'E' &&
                     header[6] == 'N' &&
                     header[7] == 'D';

        if (isIdat)
        {
            idatData.Write(body, 0, chunkLength);
        }
        else
        {
            if (!flushedIdat &&
                idatData.Length > 0)
            {
                flushedIdat = true;
                WriteNormalizedIdat(target, idatData.GetBuffer(), (int) idatData.Length);
            }

            target.Write(header);
            target.Write(body);
        }

        return isIend;
    }

    static void WriteNormalizedIdat(Stream target, byte[] zlibBytes, int zlibLength)
    {
        using var decompressedStream = new MemoryStream();
        using (var zlibInput = new MemoryStream(zlibBytes, 0, zlibLength))
        using (var zlibStream = new ZLibStream(zlibInput, CompressionMode.Decompress))
        {
            zlibStream.CopyTo(decompressedStream);
        }

        var raw = decompressedStream.GetBuffer();
        var rawLength = (int) decompressedStream.Length;

        // Write raw zlib stored format to avoid framework DEFLATE differences.
        // Format: CMF(0x78) FLG(0x01) + DEFLATE stored blocks + Adler-32
        using var compressOutput = new MemoryStream();
        compressOutput.WriteByte(0x78); // CMF: deflate, 32K window
        compressOutput.WriteByte(0x01); // FLG: no dict, check bits make CMF*256+FLG divisible by 31

        // Write DEFLATE stored blocks (max 65535 bytes each)
        var offset = 0;
        while (offset < rawLength)
        {
            var blockSize = Math.Min(rawLength - offset, 65535);
            var isFinal = offset + blockSize >= rawLength;
            compressOutput.WriteByte(isFinal ? (byte) 1 : (byte) 0); // BFINAL + BTYPE=00
            compressOutput.WriteByte((byte) (blockSize & 0xFF));
            compressOutput.WriteByte((byte) (blockSize >> 8));
            compressOutput.WriteByte((byte) (~blockSize & 0xFF));
            compressOutput.WriteByte((byte) ((~blockSize >> 8) & 0xFF));
            compressOutput.Write(raw, offset, blockSize);
            offset += blockSize;
        }

        if (rawLength == 0)
        {
            // Empty data: single final stored block with length 0
            compressOutput.WriteByte(1);
            compressOutput.Write([0, 0, 0xFF, 0xFF], 0, 4);
        }

        // Adler-32 checksum
        var adler = Adler32(raw, rawLength);
        compressOutput.WriteByte((byte) (adler >> 24));
        compressOutput.WriteByte((byte) (adler >> 16));
        compressOutput.WriteByte((byte) (adler >> 8));
        compressOutput.WriteByte((byte) adler);

        var length = (int) compressOutput.Length;
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, length);
        target.Write(header);
        target.Write(idatType);
        target.Write(compressOutput.GetBuffer(), 0, length);

        var crc = new Crc32();
        crc.Append(idatType);
        crc.Append(compressOutput.GetBuffer().AsSpan(0, length));
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc.GetCurrentHashAsUInt32());
        target.Write(crcBytes);
    }

    static uint Adler32(byte[] data, int length)
    {
        var a = 1u;
        var b = 0u;
        for (var i = 0; i < length; i++)
        {
            a = (a + data[i]) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }
}

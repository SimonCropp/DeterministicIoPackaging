using System.Buffers.Binary;

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

        // Emit the IDAT chunk straight to target instead of staging the whole
        // normalized zlib stream in a second MemoryStream. The chunk length is
        // computed analytically and a single Crc32 accumulates the same bytes as
        // they are written, so this avoids buffering a second full copy of the
        // image and two extra passes over it (GetBuffer for the write + the CRC).
        //
        // Raw zlib stored format (avoids framework DEFLATE differences):
        //   CMF(0x78) FLG(0x01) + DEFLATE stored blocks + Adler-32.
        // Each stored block is a 5-byte header (BFINAL/BTYPE + LEN + NLEN) plus
        // its payload; a zero-length image still emits one empty final block.
        var blockCount = rawLength == 0 ? 1 : (rawLength + 65534) / 65535;
        var idatLength = 2 + blockCount * 5 + rawLength + 4;

        var crc = new Crc32();

        // Chunk length (big-endian) + "IDAT". The CRC covers the type + data, not the length.
        Span<byte> lengthAndType = stackalloc byte[8];
        BinaryPrimitives.WriteInt32BigEndian(lengthAndType, idatLength);
        idatType.AsSpan().CopyTo(lengthAndType.Slice(4));
        target.Write(lengthAndType);
        crc.Append(lengthAndType.Slice(4, 4));

        // Reusable scratch for the 2-byte zlib header and the 5-byte block headers.
        Span<byte> block = stackalloc byte[5];
        // CMF: deflate, 32K window. FLG: no dict, CMF*256+FLG divisible by 31.
        block[0] = 0x78;
        block[1] = 0x01;
        target.Write(block.Slice(0, 2));
        crc.Append(block.Slice(0, 2));

        if (rawLength == 0)
        {
            // Empty data: single final stored block with length 0.
            block[0] = 1;
            block[1] = 0;
            block[2] = 0;
            block[3] = 0xFF;
            block[4] = 0xFF;
            target.Write(block);
            crc.Append(block);
        }
        else
        {
            // DEFLATE stored blocks (max 65535 bytes each).
            var offset = 0;
            while (offset < rawLength)
            {
                var blockSize = Math.Min(rawLength - offset, 65535);
                var isFinal = offset + blockSize >= rawLength;
                block[0] = isFinal ? (byte) 1 : (byte) 0; // BFINAL + BTYPE=00
                block[1] = (byte) (blockSize & 0xFF);
                block[2] = (byte) (blockSize >> 8);
                block[3] = (byte) (~blockSize & 0xFF);
                block[4] = (byte) ((~blockSize >> 8) & 0xFF);
                target.Write(block);
                crc.Append(block);

                target.Write(raw, offset, blockSize);
                crc.Append(raw.AsSpan(offset, blockSize));
                offset += blockSize;
            }
        }

        // Adler-32 checksum, then the chunk's CRC-32 (over type + data).
        Span<byte> trailer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(trailer, Adler32(raw, rawLength));
        target.Write(trailer);
        crc.Append(trailer);

        BinaryPrimitives.WriteUInt32BigEndian(trailer, crc.GetCurrentHashAsUInt32());
        target.Write(trailer);
    }

    // Adler-32 checksum as defined in RFC 1950.
    // a = 1 + sum of all bytes, b = sum of all intermediate a values, both mod 65521.
    // The modulo is deferred in batches of up to 5552 bytes to avoid per-byte division
    // while staying within uint32 overflow limits.
    static uint Adler32(byte[] data, int length)
    {
        // largest prime smaller than 2^16
        const uint mod = 65521;
        // max iterations before uint32 overflow of b
        const int nmax = 5552;
        var a = 1u;
        var b = 0u;
        var offset = 0;
        while (offset < length)
        {
            var count = Math.Min(length - offset, nmax);
            for (var i = 0; i < count; i++)
            {
                a += data[offset + i];
                b += a;
            }

            a %= mod;
            b %= mod;
            offset += count;
        }

        return (b << 16) | a;
    }
}

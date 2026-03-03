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
            var chunkLength = ReadInt32BigEndian(data, offset);
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

            // Decompress: skip 2-byte zlib header, DeflateStream handles the rest
            byte[] decompressed;
            using (var deflateInput = new MemoryStream(zlibBytes, 2, zlibBytes.Length - 2))
            using (var deflateStream = new DeflateStream(deflateInput, CompressionMode.Decompress))
            using (var decompressedStream = new MemoryStream())
            {
                deflateStream.CopyTo(decompressedStream);
                decompressed = decompressedStream.ToArray();
            }

            // Recompress with fixed settings
            byte[] newIdatData;
            using (var compressOutput = new MemoryStream())
            {
                // zlib header: CMF=0x78 (deflate, 32K window), FLG=0x9C (default compression)
                compressOutput.WriteByte(0x78);
                compressOutput.WriteByte(0x9C);

                using (var deflateStream = new DeflateStream(compressOutput, CompressionLevel.Optimal, leaveOpen: true))
                {
                    deflateStream.Write(decompressed, 0, decompressed.Length);
                }

                // Adler-32 checksum (big-endian)
                var adler = ComputeAdler32(decompressed);
                compressOutput.WriteByte((byte) (adler >> 24));
                compressOutput.WriteByte((byte) (adler >> 16));
                compressOutput.WriteByte((byte) (adler >> 8));
                compressOutput.WriteByte((byte) adler);

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
        WriteInt32BigEndian(target, data.Length);
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

        WriteUInt32BigEndian(target, crc ^ 0xFFFFFFFF);
    }

    static int ReadInt32BigEndian(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    static void WriteInt32BigEndian(Stream stream, int value)
    {
        stream.WriteByte((byte) (value >> 24));
        stream.WriteByte((byte) (value >> 16));
        stream.WriteByte((byte) (value >> 8));
        stream.WriteByte((byte) value);
    }

    static void WriteUInt32BigEndian(Stream stream, uint value)
    {
        stream.WriteByte((byte) (value >> 24));
        stream.WriteByte((byte) (value >> 16));
        stream.WriteByte((byte) (value >> 8));
        stream.WriteByte((byte) value);
    }

    static uint ComputeAdler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var val in data)
        {
            a = (a + val) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
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

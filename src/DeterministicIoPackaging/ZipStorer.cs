using System.Text;

namespace DeterministicIoPackaging;

/// <summary>
/// Rewrites a ZIP archive so every entry uses method 0 (Stored).
/// This ensures byte-identical output across all .NET runtimes,
/// since the built-in ZipArchive on net48 ignores CompressionLevel.NoCompression.
/// </summary>
static class ZipStorer
{
    public static void RewriteAsStored(MemoryStream source, Stream target)
    {
        source.Position = 0;
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        using var writer = new BinaryWriter(target, Encoding.UTF8, leaveOpen: true);
        var entries = new List<(string name, byte[] data, uint crc, long headerOffset)>();

        foreach (var entry in archive.Entries.OrderBy(_ => _.FullName, StringComparer.Ordinal))
        {
            var headerOffset = target.Position;

            byte[] data;
            using (var entryStream = entry.Open())
            using (var ms = new MemoryStream())
            {
                entryStream.CopyTo(ms);
                data = ms.ToArray();
            }

            var crc = Crc32(data);
            var nameBytes = Encoding.UTF8.GetBytes(entry.FullName);

            // Local file header
            writer.Write(0x04034b50u);       // signature
            writer.Write((ushort)20);        // version needed
            writer.Write((ushort)0);         // general purpose flags
            writer.Write((ushort)0);         // compression method: Stored
            writer.Write(DosTime);           // last mod time
            writer.Write(DosDate);           // last mod date
            writer.Write(crc);               // crc-32
            writer.Write((uint)data.Length);  // compressed size
            writer.Write((uint)data.Length);  // uncompressed size
            writer.Write((ushort)nameBytes.Length); // file name length
            writer.Write((ushort)0);         // extra field length
            writer.Write(nameBytes);
            writer.Write(data);

            entries.Add((entry.FullName, data, crc, headerOffset));
        }

        var centralStart = target.Position;

        foreach (var (name, data, crc, headerOffset) in entries)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);

            // Central directory header
            writer.Write(0x02014b50u);       // signature
            writer.Write((ushort)20);        // version made by
            writer.Write((ushort)20);        // version needed
            writer.Write((ushort)0);         // general purpose flags
            writer.Write((ushort)0);         // compression method: Stored
            writer.Write(DosTime);           // last mod time
            writer.Write(DosDate);           // last mod date
            writer.Write(crc);               // crc-32
            writer.Write((uint)data.Length);  // compressed size
            writer.Write((uint)data.Length);  // uncompressed size
            writer.Write((ushort)nameBytes.Length); // file name length
            writer.Write((ushort)0);         // extra field length
            writer.Write((ushort)0);         // file comment length
            writer.Write((ushort)0);         // disk number start
            writer.Write((ushort)0);         // internal file attributes
            writer.Write(0u);               // external file attributes
            writer.Write((uint)headerOffset); // relative offset of local header
            writer.Write(nameBytes);
        }

        var centralEnd = target.Position;
        var centralSize = centralEnd - centralStart;

        // End of central directory record
        writer.Write(0x06054b50u);           // signature
        writer.Write((ushort)0);             // disk number
        writer.Write((ushort)0);             // disk with central directory
        writer.Write((ushort)entries.Count);  // entries on this disk
        writer.Write((ushort)entries.Count);  // total entries
        writer.Write((uint)centralSize);      // central directory size
        writer.Write((uint)centralStart);     // central directory offset
        writer.Write((ushort)0);             // comment length
    }

    // DOS date/time for 2020-01-01 00:00:00
    const ushort DosTime = 0;
    const ushort DosDate = (40 << 9) | (1 << 5) | 1; // year=2020-1980=40, month=1, day=1

    static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc >> 1) ^ (0xEDB88320u & ~((crc & 1) - 1));
            }
        }

        return ~crc;
    }
}

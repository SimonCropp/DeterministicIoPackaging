using System.Buffers.Binary;

namespace DeterministicIoPackaging;

// ZipArchive stamps the host operating system into every central-directory
// record: the high byte of the "version made by" field is 0 on Windows and 3
// on Unix (per the .ZIP spec § 4.4.2), and Unix builds can additionally leak
// file-mode bits into the external-file-attributes field. Neither affects the
// archive's content, but both make the produced bytes depend on the OS that
// ran the conversion, defeating cross-platform determinism. This pass rewrites
// those fields to fixed values so the output is identical on every OS.
static class ZipPlatformNormalizer
{
    // Central-directory file header signature "PK\x01\x02".
    static readonly byte[] centralDirectoryHeader = [0x50, 0x4B, 0x01, 0x02];

    // End-of-central-directory record signature "PK\x05\x06".
    static readonly byte[] endOfCentralDirectory = [0x50, 0x4B, 0x05, 0x06];

    // Fixed size of a central-directory file header before the variable-length
    // file name, extra field and comment.
    const int centralHeaderSize = 46;

    // Minimum size of the end-of-central-directory record (no archive comment).
    const int eocdSize = 22;

    public static void Normalize(MemoryStream archive)
    {
        var buffer = archive.GetBuffer();
        var length = (int) archive.Length;

        if (!TryFindEndOfCentralDirectory(buffer, length, out var eocd))
        {
            return;
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(eocd + 10));
        var offset = (int) BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(eocd + 16));

        for (var record = 0; record < count; record++)
        {
            if (offset + centralHeaderSize > length ||
                !StartsWith(buffer, offset, centralDirectoryHeader))
            {
                // Not the structure we expect (e.g. a ZIP64 archive). Leave it
                // untouched rather than risk corrupting the output.
                return;
            }

            // "version made by" high byte (host OS): force to 0 (MS-DOS / FAT).
            buffer[offset + 5] = 0;
            // External file attributes (4 bytes): clear any Unix mode bits.
            buffer.AsSpan(offset + 38, 4).Clear();

            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset + 28));
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset + 30));
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset + 32));
            offset += centralHeaderSize + nameLength + extraLength + commentLength;
        }
    }

    // Scans backwards for the EOCD signature. ZipArchive writes no archive
    // comment, so it is normally the final 22 bytes, but scanning keeps this
    // robust to any trailing bytes.
    static bool TryFindEndOfCentralDirectory(byte[] buffer, int length, out int position)
    {
        for (var i = length - eocdSize; i >= 0; i--)
        {
            if (StartsWith(buffer, i, endOfCentralDirectory))
            {
                position = i;
                return true;
            }
        }

        position = -1;
        return false;
    }

    static bool StartsWith(byte[] buffer, int offset, byte[] signature)
    {
        if (offset < 0 ||
            offset + signature.Length > buffer.Length)
        {
            return false;
        }

        for (var i = 0; i < signature.Length; i++)
        {
            if (buffer[offset + i] != signature[i])
            {
                return false;
            }
        }

        return true;
    }
}

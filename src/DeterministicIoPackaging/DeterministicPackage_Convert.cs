namespace DeterministicIoPackaging;

public static partial class DeterministicPackage
{
    public static MemoryStream Convert(Stream source)
    {
        var target = new MemoryStream();
        Convert(source, target);
        target.Position = 0;
        return target;
    }

    public static async Task<MemoryStream> ConvertAsync(Stream source)
    {
        var target = new MemoryStream();
        await ConvertAsync(source, target);
        target.Position = 0;
        return target;
    }

    public static void Convert(Stream source, Stream target)
    {
        var patchers = CreatePatchers();
        using var sourceArchive = ReadArchive(source);
        using var targetArchive = CreateArchive(target);
        foreach (var sourceEntry in sourceArchive.OrderedEntries())
        {
            DuplicateEntry(sourceEntry, targetArchive, patchers);
        }
    }

    public static async Task ConvertAsync(Stream source, Stream target, Cancel token = default)
    {
        var patchers = CreatePatchers();
        using var sourceArchive = ReadArchive(source);
        using var targetArchive = CreateArchive(target);
        foreach (var sourceEntry in sourceArchive.OrderedEntries())
        {
            await DuplicateEntryAsync(sourceEntry, targetArchive, patchers, token);
        }
    }

    // ZIP local file header signature ("PK\x03\x04").
    // Used to detect nested ZIP packages (e.g. xlsx/docx/pptx embedded inside
    // word/embeddings/, ppt/embeddings/, xl/embeddings/) so they can be
    // recursively normalized rather than copied through verbatim.
    static readonly byte[] zipLocalFileHeader = [0x50, 0x4B, 0x03, 0x04];

    // ZIP end-of-central-directory signature ("PK\x05\x06") — appears at the
    // start of an empty ZIP archive that contains no entries.
    static readonly byte[] zipEndOfCentralDirectory = [0x50, 0x4B, 0x05, 0x06];

    static bool LooksLikeZip(byte[] head, int length)
    {
        if (length < 4)
        {
            return false;
        }

        return (head[0] == zipLocalFileHeader[0] &&
                head[1] == zipLocalFileHeader[1] &&
                head[2] == zipLocalFileHeader[2] &&
                head[3] == zipLocalFileHeader[3]) ||
               (head[0] == zipEndOfCentralDirectory[0] &&
                head[1] == zipEndOfCentralDirectory[1] &&
                head[2] == zipEndOfCentralDirectory[2] &&
                head[3] == zipEndOfCentralDirectory[3]);
    }

    // Copies source → target, recursively normalizing the entry if its bytes
    // begin with a ZIP magic signature (i.e. the entry is itself a ZIP package
    // such as an embedded xlsx inside a docx). Without this recursion, nested
    // packages flow through with whatever non-deterministic deflate/timestamps
    // their producer emitted, defeating the deterministic guarantee for the
    // outer package.
    static void CopyOrRecurseZip(Stream source, Stream target)
    {
        var head = new byte[4];
        var read = ReadUpTo(source, head, 4);

        if (LooksLikeZip(head, read))
        {
            using var buffer = new MemoryStream();
            buffer.Write(head, 0, read);
            source.CopyTo(buffer);
            buffer.Position = 0;
            Convert(buffer, target);
            return;
        }

        if (read > 0)
        {
            target.Write(head, 0, read);
        }

        source.CopyTo(target);
    }

    static async Task CopyOrRecurseZipAsync(Stream source, Stream target, Cancel cancel)
    {
        var head = new byte[4];
        var read = await ReadUpToAsync(source, head, 4, cancel);

        if (LooksLikeZip(head, read))
        {
            using var buffer = new MemoryStream();
            await buffer.WriteAsync(head, 0, read, cancel);
            await source.CopyToAsync(buffer, cancel);
            buffer.Position = 0;
            await ConvertAsync(buffer, target, cancel);
            return;
        }

        if (read > 0)
        {
            await target.WriteAsync(head, 0, read, cancel);
        }

        await source.CopyToAsync(target, cancel);
    }

    static int ReadUpTo(Stream source, byte[] buffer, int count) =>
        source.ReadAtLeast(buffer.AsSpan(0, count), count, throwOnEndOfStream: false);

    static async Task<int> ReadUpToAsync(Stream source, byte[] buffer, int count, Cancel cancel) =>
        await source.ReadAtLeastAsync(buffer.AsMemory(0, count), count, throwOnEndOfStream: false, cancel);

    static IOrderedEnumerable<Entry> OrderedEntries(this Archive archive) =>
        archive.Entries
            .OrderBy(_ => _.FullName, StringComparer.Ordinal);
}

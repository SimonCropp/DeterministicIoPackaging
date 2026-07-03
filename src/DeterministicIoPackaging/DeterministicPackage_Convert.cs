namespace DeterministicIoPackaging;

public static partial class DeterministicPackage
{
    // Normalizing a package is not a streaming operation: entries are reordered,
    // every part is rewritten, and the central directory is patched after the fact
    // (see ZipPlatformNormalizer) — all of which need the whole archive in a
    // seekable buffer. The result is therefore always a fresh MemoryStream, built
    // and patched in place with no extra copy.
    public static MemoryStream Convert(Stream source)
    {
        var target = new MemoryStream();
        using (var sourceArchive = ReadArchive(source))
        using (var targetArchive = CreateArchive(target))
        {
            // Part names must be known before patching begins so the
            // ContentTypesPatcher can canonicalize the content-type map against
            // every part in the package, not just the ones the input map lists.
            var patchers = CreatePatchers(CollectPartNames(sourceArchive));
            foreach (var sourceEntry in sourceArchive.OrderedEntries())
            {
                DuplicateEntry(sourceEntry, targetArchive, patchers);
            }
        }

        ZipPlatformNormalizer.Normalize(target);
        target.Position = 0;
        return target;
    }

    public static async Task<MemoryStream> ConvertAsync(Stream source, Cancel token = default)
    {
        var target = new MemoryStream();
        using (var sourceArchive = ReadArchive(source))
        using (var targetArchive = CreateArchive(target))
        {
            var patchers = CreatePatchers(CollectPartNames(sourceArchive));
            foreach (var sourceEntry in sourceArchive.OrderedEntries())
            {
                await DuplicateEntryAsync(sourceEntry, targetArchive, patchers, token);
            }
        }

        ZipPlatformNormalizer.Normalize(target);
        target.Position = 0;
        return target;
    }

    // Every part in the package, as leading-slash PartName values, for the
    // ContentTypesPatcher. [Content_Types].xml is not itself a part, and skipped
    // entries are dropped from the output, so neither belongs in the map.
    static IReadOnlyCollection<string> CollectPartNames(Archive archive)
    {
        var partNames = new List<string>();
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName is "[Content_Types].xml" ||
                IsSkippedEntry(entry))
            {
                continue;
            }

            partNames.Add(ContentTypesPatcher.ToPartName(entry.FullName));
        }

        return partNames;
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
    static void CopyOrRecurseZip(Stream source, Stream target, long sourceLength)
    {
        var head = new byte[4];
        var read = ReadUpTo(source, head, 4);

        if (LooksLikeZip(head, read))
        {
            // The whole entry (head + remainder) is buffered for the recursive
            // Convert. Its uncompressed size is known from the central directory,
            // so presize the buffer to avoid MemoryStream's grow-and-copy churn.
            using var buffer = new MemoryStream(InitialCapacity(sourceLength));
            buffer.Write(head, 0, read);
            source.CopyTo(buffer);
            buffer.Position = 0;
            using var normalized = Convert(buffer);
            normalized.CopyTo(target);
            return;
        }

        if (read > 0)
        {
            target.Write(head, 0, read);
        }

        source.CopyTo(target);
    }

    static async Task CopyOrRecurseZipAsync(Stream source, Stream target, long sourceLength, Cancel cancel)
    {
        var head = new byte[4];
        var read = await ReadUpToAsync(source, head, 4, cancel);

        if (LooksLikeZip(head, read))
        {
            // See CopyOrRecurseZip: presize the recursion buffer to the entry's
            // known uncompressed size to avoid grow-and-copy reallocations.
            using var buffer = new MemoryStream(InitialCapacity(sourceLength));
            await buffer.WriteAsync(head, 0, read, cancel);
            await source.CopyToAsync(buffer, cancel);
            buffer.Position = 0;
            using var normalized = await ConvertAsync(buffer, cancel);
            await normalized.CopyToAsync(target, cancel);
            return;
        }

        if (read > 0)
        {
            await target.WriteAsync(head, 0, read, cancel);
        }

        await source.CopyToAsync(target, cancel);
    }

    // Clamp a ZipArchiveEntry.Length to a valid MemoryStream initial capacity.
    // 0 (the parameterless-constructor default) for unknown/oversized lengths.
    static int InitialCapacity(long sourceLength)
    {
        if (sourceLength is > 0 and <= int.MaxValue)
        {
            return (int) sourceLength;
        }

        return 0;
    }

    static int ReadUpTo(Stream source, byte[] buffer, int count) =>
        source.ReadAtLeast(buffer.AsSpan(0, count), count, throwOnEndOfStream: false);

    static async Task<int> ReadUpToAsync(Stream source, byte[] buffer, int count, Cancel cancel) =>
        await source.ReadAtLeastAsync(buffer.AsMemory(0, count), count, throwOnEndOfStream: false, cancel);

    static IOrderedEnumerable<Entry> OrderedEntries(this Archive archive) =>
        archive.Entries
            .OrderBy(_ => _.FullName, StringComparer.Ordinal);
}

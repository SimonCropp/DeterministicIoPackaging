namespace DeterministicIoPackaging;

public static partial class DeterministicPackage
{
    // Normalizing a package is not a streaming operation: entries are reordered,
    // every part is rewritten, and the central directory is patched after the fact
    // (see ZipPlatformNormalizer) — all of which need the whole archive in a
    // seekable buffer. The result is therefore always a fresh MemoryStream, built
    // and patched in place with no extra copy.
    public static MemoryStream Convert(Stream source) => Convert(source, (ChangeRecorder?) null);

    /// <summary>
    /// Converts <paramref name="source"/> and reports what changed.
    /// </summary>
    /// <param name="source">The package to convert.</param>
    /// <param name="changes">
    /// What actually differed. Empty for a package that was already deterministic, since only
    /// differences are recorded — see <see cref="ConvertChange"/> for what is deliberately left out.
    /// </param>
    public static MemoryStream Convert(Stream source, out IReadOnlyList<ConvertChange> changes)
    {
        var recorder = new ChangeRecorder();
        var target = Convert(source, recorder);
        changes = recorder.Changes;
        return target;
    }

    static MemoryStream Convert(Stream source, ChangeRecorder? recorder)
    {
        var target = new MemoryStream();
        using (var sourceArchive = ReadArchive(source))
        using (var targetArchive = CreateArchive(target))
        {
            // Part names must be known before patching begins so the
            // ContentTypesPatcher can canonicalize the content-type map against
            // every part in the package, not just the ones the input map lists.
            var patchers = CreatePatchers(CollectPartNames(sourceArchive));
            var ordered = sourceArchive.OrderedEntries().ToList();
            RecordOrder(sourceArchive, ordered, recorder);
            foreach (var sourceEntry in ordered)
            {
                DuplicateEntry(sourceEntry, targetArchive, patchers, recorder);
            }
        }

        ZipPlatformNormalizer.Normalize(target);
        target.Position = 0;
        return target;
    }

    public static Task<MemoryStream> ConvertAsync(Stream source, Cancel token = default) =>
        ConvertAsync(source, null, token);

    /// <summary>
    /// Converts <paramref name="source"/> and reports what changed.
    /// </summary>
    /// <remarks>
    /// The report comes back beside the stream rather than through an <c>out</c> parameter, which an
    /// async method cannot have. The synchronous
    /// <see cref="Convert(Stream, out IReadOnlyList{ConvertChange})"/> uses <c>out</c>, since it can.
    /// </remarks>
    public static async Task<ConvertResult> ConvertWithChangesAsync(Stream source, Cancel token = default)
    {
        var recorder = new ChangeRecorder();
        var target = await ConvertAsync(source, recorder, token);
        return new(target, recorder.Changes);
    }

    static async Task<MemoryStream> ConvertAsync(Stream source, ChangeRecorder? recorder, Cancel token)
    {
        var target = new MemoryStream();
        using (var sourceArchive = ReadArchive(source))
        using (var targetArchive = CreateArchive(target))
        {
            var patchers = CreatePatchers(CollectPartNames(sourceArchive));
            var ordered = sourceArchive.OrderedEntries().ToList();
            RecordOrder(sourceArchive, ordered, recorder);
            foreach (var sourceEntry in ordered)
            {
                await DuplicateEntryAsync(sourceEntry, targetArchive, patchers, recorder, token);
            }
        }

        ZipPlatformNormalizer.Normalize(target);
        target.Position = 0;
        return target;
    }

    // The source listing against the order the entries are about to be written in, with the dropped
    // entries excluded from both: those are already reported as removed, and their absence is not a
    // reordering.
    static void RecordOrder(Archive sourceArchive, IEnumerable<Entry> ordered, ChangeRecorder? recorder) =>
        recorder?.RecordOrder(
            sourceArchive.Entries.Where(_ => !IsSkippedEntry(_)).Select(_ => _.FullName),
            ordered.Where(_ => !IsSkippedEntry(_)).Select(_ => _.FullName));

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
    static void CopyOrRecurseZip(Stream source, Stream target, long sourceLength, ChangeRecorder? recorder, string entryName)
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
            using var normalized = Convert(buffer, (ChangeRecorder?) null);
            // Both halves are already resident, so the comparison is the cheap part here. The nested
            // package's own changes are not reported individually: the outer entry is the part of
            // this package that differs.
            RecordIfDifferent(buffer, normalized, recorder, entryName);
            normalized.CopyTo(target);
            return;
        }

        if (read > 0)
        {
            target.Write(head, 0, read);
        }

        source.CopyTo(target);
    }

    static async Task CopyOrRecurseZipAsync(Stream source, Stream target, long sourceLength, ChangeRecorder? recorder, string entryName, Cancel cancel)
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
            using var normalized = await ConvertAsync(buffer, null, cancel);
            RecordIfDifferent(buffer, normalized, recorder, entryName);
            await normalized.CopyToAsync(target, cancel);
            return;
        }

        if (read > 0)
        {
            await target.WriteAsync(head, 0, read, cancel);
        }

        await source.CopyToAsync(target, cancel);
    }

    static void RecordIfDifferent(MemoryStream before, MemoryStream after, ChangeRecorder? recorder, string entryName)
    {
        if (recorder == null ||
            SameBytes(before, after))
        {
            return;
        }

        recorder.Record(ConvertChangeKind.Patched, entryName);
    }

    static bool SameBytes(MemoryStream left, MemoryStream right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        if (!left.TryGetBuffer(out var leftBuffer) ||
            !right.TryGetBuffer(out var rightBuffer))
        {
            return left.ToArray().AsSpan().SequenceEqual(right.ToArray());
        }

        return leftBuffer.AsSpan().SequenceEqual(rightBuffer.AsSpan());
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

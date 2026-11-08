namespace DeterministicIoPackaging;

public static partial class DeterministicPackage
{
    public static DateTime StableDate { get; } = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static DateTimeOffset StableDateOffset { get; } = new(StableDate);

    static PatcherSet CreatePatchers(IReadOnlyCollection<string> partNames)
    {
        var workbookRelsPatcher = new WorkbookRelationshipPatcher();
        var documentRelsPatcher = new DocumentRelationshipPatcher();
        var sheetRelsPatcher = new SheetRelationshipPatcher();
        var wordPartRelsPatcher = new WordPartRelationshipPatcher();
        var pptxRelsPatcher = new PptxRelationshipPatcher();
        var wordIdNumbering = new WordIdNumbering();
        IReadOnlyList<IPatcher> patchers =
        [
            new ContentTypesPatcher(partNames),
            new RelationshipPatcher(),
            sheetRelsPatcher,
            new SheetPatcher(sheetRelsPatcher),
            workbookRelsPatcher,
            new WorkbookPatcher(workbookRelsPatcher),
            new CorePatcher(),
            new CustomPropertiesPatcher(),
            documentRelsPatcher,
            new DocumentPatcher(documentRelsPatcher, wordIdNumbering),
            wordPartRelsPatcher,
            new WordPartPatcher(wordPartRelsPatcher, wordIdNumbering),
            pptxRelsPatcher,
            new PptxContentPatcher(pptxRelsPatcher),
            new NumberingPatcher()
        ];
        return new(patchers);
    }

    static Archive CreateArchive(Stream target) => new(target, ZipArchiveMode.Create, leaveOpen: true);

    static Archive ReadArchive(Stream source)
    {
        if (source is MemoryStream memoryStream)
        {
            memoryStream.Position = 0;
        }

        return new(source, ZipArchiveMode.Read, leaveOpen: true);
    }

    static void DuplicateEntry(Entry sourceEntry, Archive targetArchive, PatcherSet currentPatchers, ChangeRecorder? recorder)
    {
        if (IsSkippedEntry(sourceEntry))
        {
            recorder?.Record(ConvertChangeKind.Removed, sourceEntry.FullName);
            return;
        }

        using var sourceStream = sourceEntry.Open();
        var targetEntry = CreateEntry(sourceEntry, targetArchive);
        using var targetStream = targetEntry.Open();

        var patcher = currentPatchers.Find(sourceEntry);
        if (patcher != null)
        {
            var xml = XDocument.Load(sourceStream);
            var before = Snapshot(xml, recorder);
            patcher.PatchXml(xml, sourceEntry.FullName);
            RecordIfPatched(before, xml, recorder, sourceEntry.FullName);
            SaveXml(xml, targetStream);
            return;
        }

        if (IsSpreadsheetXml(sourceEntry))
        {
            var xml = XDocument.Load(sourceStream);
            var before = Snapshot(xml, recorder);
            FixPrefixedDefaultNamespace(xml);
            RecordIfPatched(before, xml, recorder, sourceEntry.FullName);
            SaveXml(xml, targetStream);
            return;
        }

        CopyBinary(sourceStream, targetStream, sourceEntry, recorder);
    }

    // Binary entries are sniffed by content rather than trusted by name: PowerPoint sometimes saves a
    // JPEG under a .png name, and parsing that as PNG chunks reads the JFIF header as a ~1.2 GB chunk
    // length. Only an entry that is both named .png and starts with the PNG signature is normalized.
    static void CopyBinary(Stream source, Stream target, Entry entry, ChangeRecorder? recorder)
    {
        var head = new byte[headLength];
        var read = ReadUpTo(source, head, headLength);

        if (IsPngName(entry) &&
            PngNormalizer.IsPng(head, read))
        {
            NormalizePng(source, target, recorder, entry.FullName);
            return;
        }

        if (LooksLikeZip(head, read))
        {
            RecurseZip(head, read, source, target, entry.Length, recorder, entry.FullName);
            return;
        }

        CopyInFixedChunks(head, read, source, target);
    }

    // The part as loaded, serialized, so it can be compared against the part as patched. Only when a
    // report was asked for: this is pure cost for a caller that just wants the bytes.
    //
    // Deliberately the loaded tree rather than the source bytes. Load drops insignificant
    // whitespace, so a prettified input and a compact one give the same snapshot, and the comparison
    // reports only what a patcher actually did — not the reserialization every XML part gets anyway.
    static string? Snapshot(XDocument xml, ChangeRecorder? recorder)
    {
        if (recorder == null)
        {
            return null;
        }

        return xml.ToString(SaveOptions.DisableFormatting);
    }

    // Serialized comparison rather than XNode.DeepEquals, which is wrong here: DeepEquals compares
    // the attribute lists, and RelationshipRenumber rebuilds the root's children in a way that drops
    // the root's xmlns declaration attribute. The element names still carry the namespace, so the
    // declaration is re-emitted on save and the output is byte-identical — but DeepEquals reports a
    // difference, which made every .rels part of an already converted package report as patched.
    static void RecordIfPatched(string? before, XDocument after, ChangeRecorder? recorder, string entryName)
    {
        if (before == null ||
            before == after.ToString(SaveOptions.DisableFormatting))
        {
            return;
        }

        recorder!.Record(ConvertChangeKind.Patched, entryName);
    }

    // PngNormalizer streams source to target, so the only way to know whether it changed anything is
    // to buffer the output and compare. Worth it only when a report was asked for; otherwise this
    // writes straight through as it always has.
    //
    // The caller has already read and checked the 8-byte signature from sourceStream.
    static void NormalizePng(Stream sourceStream, Stream targetStream, ChangeRecorder? recorder, string entryName)
    {
        if (recorder == null)
        {
            PngNormalizer.NormalizeAfterSignature(sourceStream, targetStream);
            return;
        }

        using var before = new MemoryStream();
        before.Write(PngNormalizer.Signature, 0, PngNormalizer.Signature.Length);
        sourceStream.CopyTo(before);
        before.Position = 0;

        using var after = new MemoryStream();
        PngNormalizer.Normalize(before, after);

        RecordIfDifferent(before, after, recorder, entryName);
        after.Position = 0;
        after.CopyTo(targetStream);
    }

    static async Task DuplicateEntryAsync(Entry sourceEntry, Archive targetArchive, PatcherSet currentPatchers, ChangeRecorder? recorder, Cancel cancel)
    {
        var name = sourceEntry.FullName;
        if (IsSkippedEntry(sourceEntry))
        {
            recorder?.Record(ConvertChangeKind.Removed, name);
            return;
        }

        using var sourceStream = await sourceEntry.OpenAsync(cancel);
        var targetEntry = CreateEntry(sourceEntry, targetArchive);
        using var targetStream = await targetEntry.OpenAsync(cancel);

        var patcher = currentPatchers.Find(sourceEntry);
        if (patcher != null)
        {
            var xml = await XDocument.LoadAsync(sourceStream, LoadOptions.None, cancel);
            var before = Snapshot(xml, recorder);
            patcher.PatchXml(xml, name);
            RecordIfPatched(before, xml, recorder, name);
            await SaveXml(xml, targetStream, cancel);
            return;
        }

        if (IsSpreadsheetXml(sourceEntry))
        {
            var xml = await XDocument.LoadAsync(sourceStream, LoadOptions.None, cancel);
            var before = Snapshot(xml, recorder);
            FixPrefixedDefaultNamespace(xml);
            RecordIfPatched(before, xml, recorder, name);
            await SaveXml(xml, targetStream, cancel);
            return;
        }

        await CopyBinaryAsync(sourceStream, targetStream, sourceEntry, recorder, cancel);
    }

    /// <inheritdoc cref="CopyBinary"/>
    static async Task CopyBinaryAsync(Stream source, Stream target, Entry entry, ChangeRecorder? recorder, Cancel cancel)
    {
        var head = new byte[headLength];
        var read = await ReadUpToAsync(source, head, headLength, cancel);

        if (IsPngName(entry) &&
            PngNormalizer.IsPng(head, read))
        {
            await NormalizePngAsync(source, target, recorder, entry.FullName, cancel);
            return;
        }

        if (LooksLikeZip(head, read))
        {
            await RecurseZipAsync(head, read, source, target, entry.Length, recorder, entry.FullName, cancel);
            return;
        }

        await CopyInFixedChunksAsync(head, read, source, target, cancel);
    }

    // Enough for the 8-byte PNG signature, which also covers the 4-byte ZIP signatures.
    const int headLength = 8;

    static bool IsPngName(Entry entry) =>
        entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc cref="NormalizePng"/>
    static async Task NormalizePngAsync(Stream sourceStream, Stream targetStream, ChangeRecorder? recorder, string entryName, Cancel cancel)
    {
        if (recorder == null)
        {
            await PngNormalizer.NormalizeAfterSignatureAsync(sourceStream, targetStream, cancel);
            return;
        }

        using var before = new MemoryStream();
        before.Write(PngNormalizer.Signature, 0, PngNormalizer.Signature.Length);
        await sourceStream.CopyToAsync(before, cancel);
        before.Position = 0;

        using var after = new MemoryStream();
        await PngNormalizer.NormalizeAsync(before, after, cancel);

        RecordIfDifferent(before, after, recorder, entryName);
        after.Position = 0;
        await after.CopyToAsync(targetStream, cancel);
    }

    static bool IsSpreadsheetXml(Entry entry) =>
        entry.FullName.StartsWith("xl/") &&
        entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    static Task SaveXml(XDocument xml, Stream targetStream, Cancel cancel) =>
        xml.SaveAsync(targetStream, SaveOptions.DisableFormatting, cancel);

    static void SaveXml(XDocument xml, Stream targetStream) =>
        xml.Save(targetStream, SaveOptions.DisableFormatting);

    // The OpenXml SDK may output spreadsheetml XML with a prefixed default namespace
    // (e.g. <x:worksheet xmlns:x="...">) instead of an unprefixed default namespace.
    // Rewrite to unprefixed form for compatibility with tools like Spreadsheet Compare.
    internal static void FixPrefixedDefaultNamespace(XDocument xml)
    {
        var root = xml.Root;
        if (root == null)
        {
            return;
        }

        var ns = root.Name.Namespace;
        if (ns.NamespaceName is not "http://schemas.openxmlformats.org/spreadsheetml/2006/main")
        {
            return;
        }

        var prefix = root.GetPrefixOfNamespace(ns);
        if (string.IsNullOrEmpty(prefix))
        {
            return;
        }

        root.Attribute(XNamespace.Xmlns + prefix)?.Remove();
    }

    static Entry CreateEntry(Entry source, Archive target)
    {
        // Use Deflate (Optimal) instead of NoCompression/Stored (method 0).
        // Some tools (e.g. Spreadsheet Compare) cannot open ZIP files with Stored entries.
        // Deflate output is deterministic within a given .NET runtime version,
        // but may differ across runtimes (e.g. net48 vs net10.0).
        var entry = target.CreateEntry(source.FullName, CompressionLevel.Optimal);
        entry.LastWriteTime = StableDateOffset;
        return entry;
    }

    // psmdcp: NuGet core-properties metadata with non-deterministic filenames
    // .signature.p7s: NuGet package signature — removed because the deterministic
    //   conversion modifies package contents, which invalidates the signature.
    //   Additionally, NuGet requires this entry to use Stored compression (method 0),
    //   which ZipArchive.CreateEntry cannot reliably produce on .NET Framework.
    static bool IsSkippedEntry(Entry entry) =>
        entry.FullName is ".signature.p7s" ||
        (entry.FullName.StartsWith("package/services/metadata/core-properties/") &&
         entry.Name.EndsWith("psmdcp"));
}

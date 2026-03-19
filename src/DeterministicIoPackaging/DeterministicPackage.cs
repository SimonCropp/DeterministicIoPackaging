namespace DeterministicIoPackaging;

public static partial class DeterministicPackage
{
    public static DateTime StableDate { get; } = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static DateTimeOffset StableDateOffset { get; } = new(StableDate);

    static IReadOnlyList<IPatcher> CreatePatchers(Archive archive)
    {
        var entryNames = archive.Entries.Select(_ => _.FullName).ToList();
        var workbookRelsPatcher = new WorkbookRelationshipPatcher();
        var documentRelsPatcher = new DocumentRelationshipPatcher();
        return
        [
            new ContentTypesPatcher(entryNames),
            new RelationshipPatcher(),
            new SheetPatcher(),
            workbookRelsPatcher,
            new WorkbookPatcher(workbookRelsPatcher),
            new CorePatcher(),
            new SheetRelationshipPatcher(),
            documentRelsPatcher,
            new DocumentPatcher(documentRelsPatcher),
            new NumberingPatcher()
        ];
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

    static void DuplicateEntry(Entry sourceEntry, Archive targetArchive, IReadOnlyList<IPatcher> currentPatchers)
    {
        if (IsPsmdcp(sourceEntry))
        {
            return;
        }

        using var sourceStream = sourceEntry.Open();
        var targetEntry = CreateEntry(sourceEntry, targetArchive);
        using var targetStream = targetEntry.Open();

        foreach (var patcher in currentPatchers)
        {
            if (!patcher.IsMatch(sourceEntry))
            {
                continue;
            }

            using var buffer = new MemoryStream();
            sourceStream.CopyTo(buffer);
            buffer.Position = 0;
            var xml = XDocument.Load(buffer);
            if (patcher.PatchXml(xml, sourceEntry.FullName))
            {
                ThrowIfPrefixedDefaultNamespace(xml, sourceEntry.FullName);
                SaveXml(xml, targetStream);
            }
            else
            {
                buffer.Position = 0;
                buffer.CopyTo(targetStream);
            }

            return;
        }

        if (sourceEntry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            PngNormalizer.Normalize(sourceStream, targetStream);
            return;
        }

        if (sourceEntry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            using var buffer = new MemoryStream();
            sourceStream.CopyTo(buffer);
            buffer.Position = 0;
            var xml = XDocument.Load(buffer);
            ThrowIfPrefixedDefaultNamespace(xml, sourceEntry.FullName);
            buffer.Position = 0;
            buffer.CopyTo(targetStream);
            return;
        }

        sourceStream.CopyTo(targetStream);
    }

    static async Task DuplicateEntryAsync(Entry sourceEntry, Archive targetArchive, IReadOnlyList<IPatcher> currentPatchers, Cancel cancel)
    {
        if (IsPsmdcp(sourceEntry))
        {
            return;
        }

        using var sourceStream = await sourceEntry.OpenAsync(cancel);
        var targetEntry = CreateEntry(sourceEntry, targetArchive);
        using var targetStream = await targetEntry.OpenAsync(cancel);
        foreach (var patcher in currentPatchers)
        {
            if (!patcher.IsMatch(sourceEntry))
            {
                continue;
            }

            using var buffer = new MemoryStream();
            await sourceStream.CopyToAsync(buffer, cancel);
            buffer.Position = 0;
            var xml = await XDocument.LoadAsync(buffer, LoadOptions.None, cancel);
            if (patcher.PatchXml(xml, sourceEntry.FullName))
            {
                ThrowIfPrefixedDefaultNamespace(xml, sourceEntry.FullName);
                await SaveXml(xml, targetStream, cancel);
            }
            else
            {
                buffer.Position = 0;
                await buffer.CopyToAsync(targetStream, cancel);
            }

            return;
        }

        if (sourceEntry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            await PngNormalizer.NormalizeAsync(sourceStream, targetStream, cancel);
            return;
        }

        if (sourceEntry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            using var buffer = new MemoryStream();
            await sourceStream.CopyToAsync(buffer, cancel);
            buffer.Position = 0;
            var xml = XDocument.Load(buffer);
            ThrowIfPrefixedDefaultNamespace(xml, sourceEntry.FullName);
            buffer.Position = 0;
            await buffer.CopyToAsync(targetStream, cancel);
            return;
        }

        await sourceStream.CopyToAsync(targetStream, cancel);
    }

    static Task SaveXml(XDocument xml, Stream targetStream, Cancel cancel) =>
        xml.SaveAsync(targetStream, SaveOptions.DisableFormatting, cancel);

    static void SaveXml(XDocument xml, Stream targetStream) =>
        xml.Save(targetStream, SaveOptions.DisableFormatting);

    static void ThrowIfPrefixedDefaultNamespace(XDocument xml, string entryName)
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

        throw new($"Entry '{entryName}' uses a namespace prefix '{prefix}' for its default namespace '{ns}'. " +
                   "This causes compatibility issues with tools like Spreadsheet Compare. " +
                   "Use a default namespace declaration (xmlns=\"...\") instead of a prefixed one (xmlns:{prefix}=\"...\").");
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

    static bool IsPsmdcp(Entry entry) =>
        entry.FullName.StartsWith("package/services/metadata/core-properties/") &&
        entry.Name.EndsWith("psmdcp");
}

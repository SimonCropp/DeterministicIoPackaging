namespace DeterministicIoPackaging;

public static partial class DeterministicPackage
{
    public static DateTime StableDate { get; } = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static DateTimeOffset StableDateOffset { get; } = new(StableDate);

    static IReadOnlyList<IPatcher> CreatePatchers()
    {
        var workbookRelsPatcher = new WorkbookRelationshipPatcher();
        var documentRelsPatcher = new DocumentRelationshipPatcher();
        return
        [
            new ContentTypesPatcher(),
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
        if (IsSkippedEntry(sourceEntry))
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

            var xml = XDocument.Load(sourceStream);
            patcher.PatchXml(xml, sourceEntry.FullName);
            SaveXml(xml, targetStream);
            return;
        }

        if (sourceEntry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            PngNormalizer.Normalize(sourceStream, targetStream);
            return;
        }

        if (sourceEntry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            var xml = XDocument.Load(sourceStream);
            FixPrefixedDefaultNamespace(xml);
            SaveXml(xml, targetStream);
            return;
        }

        sourceStream.CopyTo(targetStream);
    }

    static async Task DuplicateEntryAsync(Entry sourceEntry, Archive targetArchive, IReadOnlyList<IPatcher> currentPatchers, Cancel cancel)
    {
        if (IsSkippedEntry(sourceEntry))
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

            var xml = await XDocument.LoadAsync(sourceStream, LoadOptions.None, cancel);
            patcher.PatchXml(xml, sourceEntry.FullName);
            await SaveXml(xml, targetStream, cancel);
            return;
        }

        if (sourceEntry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            await PngNormalizer.NormalizeAsync(sourceStream, targetStream, cancel);
            return;
        }

        if (sourceEntry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            var xml = await XDocument.LoadAsync(sourceStream, LoadOptions.None, cancel);
            FixPrefixedDefaultNamespace(xml);
            await SaveXml(xml, targetStream, cancel);
            return;
        }

        await sourceStream.CopyToAsync(targetStream, cancel);
    }

    static Task SaveXml(XDocument xml, Stream targetStream, Cancel cancel) =>
        xml.SaveAsync(targetStream, SaveOptions.DisableFormatting, cancel);

    static void SaveXml(XDocument xml, Stream targetStream) =>
        xml.Save(targetStream, SaveOptions.DisableFormatting);

    // The OpenXml SDK may output spreadsheetml XML with a prefixed default namespace
    // (e.g. <x:worksheet xmlns:x="...">) instead of an unprefixed default namespace.
    // Rewrite to unprefixed form for compatibility with tools like Spreadsheet Compare.
    internal static bool FixPrefixedDefaultNamespace(XDocument xml)
    {
        var root = xml.Root;
        if (root == null)
        {
            return false;
        }

        var ns = root.Name.Namespace;
        if (ns.NamespaceName is not "http://schemas.openxmlformats.org/spreadsheetml/2006/main")
        {
            return false;
        }

        var prefix = root.GetPrefixOfNamespace(ns);
        if (string.IsNullOrEmpty(prefix))
        {
            return false;
        }

        var prefixedAttr = root.Attribute(XNamespace.Xmlns + prefix);
        if (prefixedAttr == null)
        {
            return false;
        }

        prefixedAttr.Remove();
        return true;
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

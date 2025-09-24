namespace DeterministicIoPackaging;

public static partial class DeterministicPackage
{
    public static DateTime StableDate { get; } = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static DateTimeOffset StableDateOffset { get; } = new(StableDate);

    static Archive CreateArchive(Stream target) => new(target, ZipArchiveMode.Create, leaveOpen: true);

    static Archive ReadArchive(Stream source)
    {
        if (source is MemoryStream memoryStream)
        {
            memoryStream.Position = 0;
        }

        return new(source, ZipArchiveMode.Read, leaveOpen: true);
    }

    static void DuplicateEntry(Entry sourceEntry, Archive targetArchive)
    {
        if (IsPsmdcp(sourceEntry))
        {
            return;
        }

        using var sourceStream = sourceEntry.Open();
        var targetEntry = CreateEntry(sourceEntry, targetArchive);
        using var targetStream = targetEntry.Open();

        if (sourceEntry.IsRelationships())
        {
            var xml = RelationshipPatcher.Patch(sourceStream);
            SaveXml(xml, targetStream);
            return;
        }

        if (sourceEntry.IsWorkbookRelationships())
        {
            var xml = WorkbookRelationshipPatcher.Patch(sourceStream);
            SaveXml(xml, targetStream);
            return;
        }

        if (sourceEntry.IsWorkbookXml())
        {
            var xml = WorkbookPatcher.Patch(sourceStream);
            SaveXml(xml, targetStream);
            return;
        }

        if (sourceEntry.IsWorksheetXml())
        {
            var xml = SheetPatcher.Patch(sourceStream);
            SaveXml(xml, targetStream);
            return;
        }

        sourceStream.CopyTo(targetStream);
    }

    static async Task DuplicateEntryAsync(Entry sourceEntry, Archive targetArchive, Cancel cancel)
    {
        if (IsPsmdcp(sourceEntry))
        {
            return;
        }

        using var sourceStream = await sourceEntry.OpenAsync(cancel);
        var targetEntry = CreateEntry(sourceEntry, targetArchive);
        using var targetStream = await targetEntry.OpenAsync(cancel);
        if (sourceEntry.IsRelationships())
        {
            var xml = await RelationshipPatcher.Patch(sourceStream, cancel);
            await SaveXml(xml, targetStream, cancel);
            return;
        }

        if (sourceEntry.IsWorkbookRelationships())
        {
            var xml = await WorkbookRelationshipPatcher.Patch(sourceStream, cancel);
            await SaveXml(xml, targetStream, cancel);
            return;
        }

        if (sourceEntry.IsWorkbookXml())
        {
            var xml = await WorkbookPatcher.Patch(sourceStream, cancel);
            await SaveXml(xml, targetStream, cancel);
            return;
        }

        if (sourceEntry.IsWorksheetXml())
        {
            var xml = await SheetPatcher.Patch(sourceStream, cancel);
            await SaveXml(xml, targetStream, cancel);
            return;
        }

        await sourceStream.CopyToAsync(targetStream, cancel);
    }

    static Task SaveXml(XDocument xml, Stream targetStream, Cancel cancel) =>
        xml.SaveAsync(targetStream, SaveOptions.DisableFormatting, cancel);

    static void SaveXml(XDocument xml, Stream targetStream) =>
        xml.Save(targetStream, SaveOptions.DisableFormatting);

    static Entry CreateEntry(Entry source, Archive target)
    {
        var entry = target.CreateEntry(source.FullName, CompressionLevel.Fastest);
        entry.LastWriteTime = StableDateOffset;
        return entry;
    }

    static bool IsPsmdcp(Entry entry) =>
        entry.FullName.StartsWith("package/services/metadata/core-properties/") &&
        entry.Name.EndsWith("psmdcp");
}
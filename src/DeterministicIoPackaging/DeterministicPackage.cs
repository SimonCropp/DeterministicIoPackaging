using Entry = System.IO.Compression.ZipArchiveEntry;
using Archive = System.IO.Compression.ZipArchive;

namespace DeterministicIoPackaging;

public static class DeterministicPackage
{
    public static DateTime StableDate { get; } = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static DateTimeOffset StableDateOffset { get; } = new(StableDate);

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
        using var sourceArchive = ReadArchive(source);
        using var targetArchive = CreateArchive(target);
        foreach (var sourceEntry in sourceArchive.Entries)
        {
            DuplicateEntry(sourceEntry, targetArchive);
        }
    }

    public static async Task ConvertAsync(Stream source, Stream target, Cancel token = default)
    {
        using var sourceArchive = ReadArchive(source);
        using var targetArchive = CreateArchive(target);
        foreach (var sourceEntry in sourceArchive.Entries)
        {
            await DuplicateEntryAsync(sourceEntry, targetArchive, token);
        }
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

    static void DuplicateEntry(Entry sourceEntry, Archive targetArchive)
    {
        if (IsPsmdcp(sourceEntry))
        {
            return;
        }

        using var sourceStream = sourceEntry.Open();
        var targetEntry = CreateEntry(sourceEntry, targetArchive);
        using var targetStream = targetEntry.Open();

        if (IsRelationships(sourceEntry))
        {
            var xml = PatchRelationships(sourceStream);
            SaveXml(xml, targetStream);
            return;
        }

        if (IsWorkbookXml(sourceEntry))
        {
            var xml = PatchWorkbook(sourceStream);
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
        if (IsRelationships(sourceEntry))
        {
            var xml = PatchRelationships(sourceStream);
            await SaveXml(xml, targetStream, cancel);
            return;
        }

        if (IsWorkbookXml(sourceEntry))
        {
            var xml = PatchWorkbook(sourceStream);
            await SaveXml(xml, targetStream, cancel);
            return;
        }

        await sourceStream.CopyToAsync(targetStream, cancel);
    }

     static Task SaveXml(XDocument xml, Stream targetStream, Cancel cancel) =>
        xml.SaveAsync(targetStream, SaveOptions.DisableFormatting, cancel);

     static void SaveXml(XDocument xml, Stream targetStream) =>
        xml.Save(targetStream, SaveOptions.DisableFormatting);

    static XName relationshipName = XName.Get("Relationship", "http://schemas.openxmlformats.org/package/2006/relationships");

    static XDocument PatchRelationships(Stream sourceStream)
    {
        var xml = XDocument.Load(sourceStream);
        var relationships = xml.Descendants(relationshipName).ToList();

        for (var index = 0; index < relationships.Count; index++)
        {
            var relationship = relationships[index];
            relationship.Attribute("Id")!.SetValue($"DeterministicId{index + 1}");
        }

        var psmdcp = relationships
            .Where(rel =>
            {
                var target = rel.Attribute("Target");
                return target != null &&
                       target.Value.EndsWith(".psmdcp");
            })
            .SingleOrDefault();
        psmdcp?.Remove();

        return xml;
    }

    static XNamespace mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    static XNamespace x15ac = "http://schemas.microsoft.com/office/spreadsheetml/2010/11/ac";

    static XDocument PatchWorkbook(Stream sourceStream)
    {
        var xml = XDocument.Load(sourceStream);

        var absPath = xml
            .Descendants(mc + "AlternateContent")
            .FirstOrDefault(_ => _.Descendants(x15ac + "absPath").Any());

        absPath?.Remove();

        return xml;
    }

    static Entry CreateEntry(Entry source, Archive target)
    {
        var entry = target.CreateEntry(source.FullName, CompressionLevel.Fastest);
        entry.LastWriteTime = StableDateOffset;
        return entry;
    }

    static bool IsPsmdcp(Entry entry) =>
        entry.FullName.StartsWith("package/services/metadata/core-properties/") &&
        entry.Name.EndsWith("psmdcp");

    static bool IsRelationships(Entry _) =>
        _.FullName == "_rels/.rels";

    static bool IsWorkbookXml(Entry _) =>
        _.FullName == "xl/workbook.xml";
}
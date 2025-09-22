using Entry = System.IO.Compression.ZipArchiveEntry;
using Archive = System.IO.Compression.ZipArchive;

namespace DeterministicIoPackaging;

public static class DeterministicPackage
{
    public static DateTime StableDate { get; } = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static DateTimeOffset StableDateOffset { get; } = new(StableDate);

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

    static Archive ReadArchive(Stream source) => new(source, ZipArchiveMode.Read, leaveOpen: false);

    static void DuplicateEntry(Entry sourceEntry, Archive targetArchive)
    {
        if (IsPsmdcp(sourceEntry))
        {
            return;
        }

        using var sourceStream = sourceEntry.Open();
        var targetEntry = CreateEntry(sourceEntry, targetArchive);
        using var targetStream = targetEntry.Open();
        if (IsRels(sourceEntry))
        {
            var xml = PatchRelationships(sourceStream);
            xml.Save(targetStream, SaveOptions.None);
        }
        else
        {
            sourceStream.CopyTo(targetStream);
        }
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
        if (IsRels(sourceEntry))
        {
            var xml = PatchRelationships(sourceStream);

            await xml.SaveAsync(targetStream, SaveOptions.None, cancel);
        }
        else
        {
            await sourceStream.CopyToAsync(targetStream, cancel);
        }
    }

    static XDocument PatchRelationships(Stream sourceStream)
    {
        var xml = XDocument.Load(sourceStream);
        var name = XName.Get("Relationship", "http://schemas.openxmlformats.org/package/2006/relationships");
        var relationships = xml.Descendants(name).ToList();

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

    static Entry CreateEntry(Entry source, Archive target)
    {
        var entry = target.CreateEntry(source.FullName, CompressionLevel.Fastest);
        entry.LastWriteTime = StableDateOffset;
        return entry;
    }

    static bool IsPsmdcp(Entry entry) =>
        entry.FullName.StartsWith("package/services/metadata/core-properties/") &&
        entry.Name.EndsWith("psmdcp");

    static bool IsRels(Entry _) =>
        _.FullName == "_rels/.rels";
}
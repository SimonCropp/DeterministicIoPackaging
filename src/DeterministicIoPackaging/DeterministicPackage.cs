using Entry = System.IO.Compression.ZipArchiveEntry;
using Archive = System.IO.Compression.ZipArchive;

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

        if (IsRelationships(sourceEntry))
        {
            var xml = PatchRelationships(sourceStream, true);
            SaveXml(xml, targetStream);
            return;
        }

        if (IsWorkbookRelationships(sourceEntry))
        {
            var xml = PatchRelationships(sourceStream, false);
            SaveXml(xml, targetStream);
            return;
        }

        if (sourceEntry.IsWorkbookXml())
        {
            var xml = Workbook.Patch(sourceStream);
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
            var xml = PatchRelationships(sourceStream, true);
            await SaveXml(xml, targetStream, cancel);
            return;
        }

        if (IsWorkbookRelationships(sourceEntry))
        {
            var xml = PatchRelationships(sourceStream, false);
            await SaveXml(xml, targetStream, cancel);
            return;
        }

        if (sourceEntry.IsWorkbookXml())
        {
            var xml = Workbook.Patch(sourceStream);
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

    static XDocument PatchRelationships(Stream sourceStream, bool patchIds)
    {
        var xml = XDocument.Load(sourceStream);
        return PatchRelationships(xml, patchIds);
    }

    internal static XDocument PatchRelationships(XDocument xml, bool patchIds)
    {
        var root = xml.Root!;
        var relationships = root.Elements(relationshipName)
            .Where(_ => !IsPsmdcpElement(_))
            .OrderBy(_ => _.Attribute("Type")!.Value)
            .ToList();

        root.Elements(relationshipName).Remove();

        if (patchIds)
        {
            for (var index = 0; index < relationships.Count; index++)
            {
                var relationship = relationships[index];
                relationship.Attribute("Id")!.SetValue($"DeterministicId{index + 1}");
            }
        }

        root.Add(relationships);
        return xml;

        static bool IsPsmdcpElement(XElement rel)
        {
            var target = rel.Attribute("Target")!;
            return target.Value.EndsWith(".psmdcp");
        }
    }
    static XDocument PatchSheet(Stream sourceStream)
    {
        var xml = XDocument.Load(sourceStream);
        return PatchSheet(xml);
    }

    internal static XDocument PatchSheet(XDocument xml)
    {
        XNamespace xr = "http://schemas.microsoft.com/office/spreadsheetml/2014/revision";
        xml.Root!.Attribute(xr + "uid")?.Remove();
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
        _.FullName is "_rels/.rels";

    static bool IsWorkbookRelationships(Entry _) =>
        _.FullName is "xl/_rels/workbook.xml.rels";

}
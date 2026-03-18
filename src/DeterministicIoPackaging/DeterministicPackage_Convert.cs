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

    private static IOrderedEnumerable<Entry> OrderedEntries(this Archive archive) =>
        archive.Entries
            .OrderBy(_ => _.FullName);
}
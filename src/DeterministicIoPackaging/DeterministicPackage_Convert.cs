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
        var intermediate = new MemoryStream();
        using (var sourceArchive = ReadArchive(source))
        using (var targetArchive = CreateArchive(intermediate))
        {
            foreach (var sourceEntry in sourceArchive.OrderedEntries())
            {
                DuplicateEntry(sourceEntry, targetArchive, patchers);
            }
        }

        ZipStorer.RewriteAsStored(intermediate, target);
    }

    public static async Task ConvertAsync(Stream source, Stream target, Cancel token = default)
    {
        var patchers = CreatePatchers();
        var intermediate = new MemoryStream();
        using (var sourceArchive = ReadArchive(source))
        using (var targetArchive = CreateArchive(intermediate))
        {
            foreach (var sourceEntry in OrderedEntries(sourceArchive))
            {
                await DuplicateEntryAsync(sourceEntry, targetArchive, patchers, token);
            }
        }

        ZipStorer.RewriteAsStored(intermediate, target);
    }

    private static IOrderedEnumerable<Entry> OrderedEntries(this Archive archive) =>
        archive.Entries
            .OrderBy(_ => _.FullName);
}
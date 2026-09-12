[TestFixture]
public class ConvertChangeTests
{
    static string directory = ProjectFiles.ProjectDirectory;

    [Test]
    public void ReportsTheRemovedNuGetParts()
    {
        var changes = Convert("sample.nupkg");

        var removed = changes
            .Where(_ => _.Kind == ConvertChangeKind.Removed)
            .Select(_ => _.Entry)
            .ToList();

        Assert.That(removed, Is.Not.Empty);
        Assert.That(removed, Has.Some.Contains("psmdcp"));
    }

    [Test]
    public void ReportsThePatchedRelationships()
    {
        var changes = Convert("sample.docx");

        var patched = changes
            .Where(_ => _.Kind == ConvertChangeKind.Patched)
            .Select(_ => _.Entry)
            .ToList();

        Assert.That(patched, Is.Not.Empty);
    }

    [Test]
    public void EveryChangeNamesAnEntryExceptReordering()
    {
        var changes = Convert("sample.docx");

        foreach (var change in changes)
        {
            if (change.Kind == ConvertChangeKind.Reordered)
            {
                Assert.That(change.Entry, Is.Null);
                continue;
            }

            Assert.That(change.Entry, Is.Not.Null.And.Not.Empty);
        }
    }

    // The property the report hangs on. Converting an already converted package must report nothing,
    // or the report could never answer "why is this package not deterministic?" — every part is
    // rewritten on every conversion, so "we touched it" would always be true of everything.
    [Test]
    public void ReportsNothingOnASecondConversion()
    {
        using var first = Convert("sample.docx", out _);

        first.Position = 0;
        using var second = DeterministicPackage.Convert(first, out var changes);

        Assert.That(changes, Is.Empty);
    }

    [Test]
    public void ReportsNothingOnASecondConversionOfANuGetPackage()
    {
        using var first = Convert("sample.nupkg", out _);

        first.Position = 0;
        using var second = DeterministicPackage.Convert(first, out var changes);

        Assert.That(changes, Is.Empty);
    }

    [Test]
    public void TheReportingOverloadProducesIdenticalBytes()
    {
        using var plainSource = File.OpenRead(Path.Combine(directory, "sample.docx"));
        using var plain = DeterministicPackage.Convert(plainSource);

        using var reported = Convert("sample.docx", out _);

        Assert.That(reported.ToArray(), Is.EqualTo(plain.ToArray()));
    }

    [Test]
    public async Task TheAsyncOverloadReportsTheSameChanges()
    {
        var expected = Convert("sample.docx");

        using var source = File.OpenRead(Path.Combine(directory, "sample.docx"));
        using var result = await DeterministicPackage.ConvertWithChangesAsync(source);

        Assert.That(
            result.Changes.Select(_ => $"{_.Kind} {_.Entry}"),
            Is.EqualTo(expected.Select(_ => $"{_.Kind} {_.Entry}")));
    }

    [Test]
    public async Task TheAsyncOverloadProducesIdenticalBytes()
    {
        using var plainSource = File.OpenRead(Path.Combine(directory, "sample.docx"));
        using var plain = await DeterministicPackage.ConvertAsync(plainSource);

        using var source = File.OpenRead(Path.Combine(directory, "sample.docx"));
        using var result = await DeterministicPackage.ConvertWithChangesAsync(source);

        Assert.That(result.Stream.ToArray(), Is.EqualTo(plain.ToArray()));
    }

    [Test]
    public void ReportsReorderingWhenTheSourceIsNotSorted()
    {
        using var unsorted = new MemoryStream();
        using (var archive = new ZipArchive(unsorted, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Written in reverse of the ordinal order the conversion writes them in.
            Write(archive, "b.txt", "b");
            Write(archive, "a.txt", "a");
        }

        unsorted.Position = 0;
        using var target = DeterministicPackage.Convert(unsorted, out var changes);

        Assert.That(changes.Select(_ => _.Kind), Does.Contain(ConvertChangeKind.Reordered));
    }

    [Test]
    public void ReportsNoReorderingWhenTheSourceIsAlreadySorted()
    {
        using var sorted = new MemoryStream();
        using (var archive = new ZipArchive(sorted, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "a.txt", "a");
            Write(archive, "b.txt", "b");
        }

        sorted.Position = 0;
        using var target = DeterministicPackage.Convert(sorted, out var changes);

        Assert.That(changes.Select(_ => _.Kind), Does.Not.Contain(ConvertChangeKind.Reordered));
    }

    static void Write(ZipArchive archive, string name, string content)
    {
        using var stream = archive.CreateEntry(name).Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    static IReadOnlyList<ConvertChange> Convert(string fileName)
    {
        using var target = Convert(fileName, out var changes);
        return changes;
    }

    static MemoryStream Convert(string fileName, out IReadOnlyList<ConvertChange> changes)
    {
        using var source = File.OpenRead(Path.Combine(directory, fileName));
        return DeterministicPackage.Convert(source, out changes);
    }
}

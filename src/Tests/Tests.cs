[TestFixture]
public class Tests
{
    [Test]
    public Task AbsPathZip()
    {
        var file = Path.Combine(directory, "sample.WithAbsPath.xlsx");
        var stream = Convert(file);

        return VerifyZip(stream);
    }

    [Test]
    public Task WithWorkbookRelsZip()
    {
        var file = Path.Combine(directory, "sample.WithWorkbookRels.xlsx");
        var stream = Convert(file);

        return VerifyZip(stream);
    }

    [Test]
    public Task AbsPath()
    {
        var file = Path.Combine(directory, "sample.WithAbsPath.xlsx");
        var stream = Convert(file);

        return Verify(stream, extension: "xlsx")
            .UniqueForRuntime();
    }
    [Test]
    public Task Numbering()
    {
        var file = Path.Combine(directory, "samples.numbering1_1.docx");
        var stream = Convert(file);

        return Verify(stream, extension: "docx")
            .UniqueForRuntime();
    }

    [Test]
    public void NumberingBinaryEquality()
    {
        var file1 = Path.Combine(directory, "samples.numbering1_1.docx");
        var file2 = Path.Combine(directory, "samples.numbering1_2.docx");

        using var stream1 = Convert(file1);
        using var stream2 = Convert(file2);

        var bytes1 = stream1.ToArray();
        var bytes2 = stream2.ToArray();

        Assert.That(bytes1, Is.EqualTo(bytes2));
    }

    [Test]
    public void NumberingBinaryEquality2()
    {
        var file1 = Path.Combine(directory, "samples.numbering2_1.docx");
        var file2 = Path.Combine(directory, "samples.numbering2_2.docx");

        using var stream1 = Convert(file1);
        using var stream2 = Convert(file2);

        var bytes1 = stream1.ToArray();
        var bytes2 = stream2.ToArray();

        Assert.That(bytes1, Is.EqualTo(bytes2));
    }

    [Test]
    public void PngImageBinaryEquality()
    {
        var file1 = Path.Combine(directory, "samples.pngImage_1.docx");
        var file2 = Path.Combine(directory, "samples.pngImage_2.docx");

        using var stream1 = Convert(file1);
        using var stream2 = Convert(file2);

        var bytes1 = stream1.ToArray();
        var bytes2 = stream2.ToArray();

        Assert.That(bytes1, Is.EqualTo(bytes2));
    }

    [Test]
    public Task WithWorkbookRels()
    {
        var file = Path.Combine(directory, "sample.WithWorkbookRels.xlsx");
        var stream = Convert(file);

        return Verify(stream, extension: "xlsx")
            .UniqueForRuntime();
    }

    [Test]
    public Task Run([Values] Extension extension)
    {
        var stream = Convert(extension);

        return VerifyZip(stream);
    }

    [Test]
    public async Task RunAsync([Values] Extension extension)
    {
        var stream = await ConvertAsync(extension);

        await VerifyZip(stream);
    }

    [Test]
    public Task RunBinary([Values] Extension extension)
    {
        var stream = Convert(extension);

        return Verify(stream, extension: extension.ToString())
            .UniqueForRuntime();
    }

    [Test]
    public async Task RunBinaryAsync([Values] Extension extension)
    {
        var stream = await ConvertAsync(extension);

        await Verify(stream, extension: extension.ToString())
            .UniqueForRuntime();
    }

    [Test]
    public void RelationshipIdsAreDeterministic([Values] Extension extension)
    {
        var stream = Convert(extension);
        stream.Position = 0;
        using var archive = new Archive(stream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".rels"))
            {
                continue;
            }

            using var entryStream = entry.Open();
            var xml = XDocument.Load(entryStream);
            var ids = xml.Root!.Elements()
                .Select(_ => _.Attribute("Id")?.Value)
                .Where(_ => _ != null)
                .ToList();

            foreach (var id in ids)
            {
                Assert.That(id, Does.StartWith("DeterministicId"),
                    $"Entry '{entry.FullName}' has non-deterministic relationship Id '{id}'");
            }
        }
    }

    [Test]
    public void ContentTypesAreSorted([Values] Extension extension)
    {
        var stream = Convert(extension);
        stream.Position = 0;
        using var archive = new Archive(stream, ZipArchiveMode.Read);
        var contentTypes = archive.GetEntry("[Content_Types].xml")!;
        using var entryStream = contentTypes.Open();
        var xml = XDocument.Load(entryStream);
        var elements = xml.Root!.Elements().ToList();

        var sorted = elements
            .OrderBy(_ => _.Name.LocalName)
            .ThenBy(_ => (string?)_.Attribute("Extension") ?? "")
            .ThenBy(_ => (string?)_.Attribute("PartName") ?? "")
            .ToList();

        for (var i = 0; i < elements.Count; i++)
        {
            Assert.That(elements[i].ToString(), Is.EqualTo(sorted[i].ToString()),
                $"[Content_Types].xml element at index {i} is not in sorted order");
        }
    }

    public enum Extension
    {
        xlsx,
        nupkg,
        docx
    }

    static string directory = ProjectFiles.ProjectDirectory;

    static MemoryStream Convert(Extension extension)
    {
        var packagePath = Path.Combine(directory, $"sample.{extension}");
        return Convert(packagePath);
    }

    static MemoryStream Convert(string packagePath)
    {
        var targetStream = new MemoryStream();

        #region Convert

        using var sourceStream = File.OpenRead(packagePath);
        DeterministicPackage.Convert(sourceStream, targetStream);

        #endregion

        return targetStream;
    }


    static async Task<MemoryStream> ConvertAsync(Extension extension)
    {
        var packagePath = Path.Combine(directory, $"sample.{extension}");
        var targetStream = new MemoryStream();

        #region ConvertAsync

        using var sourceStream = File.OpenRead(packagePath);
        await DeterministicPackage.ConvertAsync(sourceStream, targetStream);

        #endregion

        return targetStream;
    }
}

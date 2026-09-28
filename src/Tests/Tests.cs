namespace DeterministicIoPackagingTests;

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
    public async Task NumberingBinaryEquality()
    {
        var file1 = Path.Combine(directory, "samples.numbering1_1.docx");
        var file2 = Path.Combine(directory, "samples.numbering1_2.docx");

        using var stream1 = Convert(file1);
        using var stream2 = Convert(file2);

        var bytes1 = stream1.ToArray();
        var bytes2 = stream2.ToArray();

        await Assert.That(bytes1).IsEquivalentTo(bytes2, CollectionOrdering.Matching);
    }

    [Test]
    public async Task NumberingBinaryEquality2()
    {
        var file1 = Path.Combine(directory, "samples.numbering2_1.docx");
        var file2 = Path.Combine(directory, "samples.numbering2_2.docx");

        using var stream1 = Convert(file1);
        using var stream2 = Convert(file2);

        var bytes1 = stream1.ToArray();
        var bytes2 = stream2.ToArray();

        await Assert.That(bytes1).IsEquivalentTo(bytes2, CollectionOrdering.Matching);
    }

    [Test]
    public async Task PngImageBinaryEquality()
    {
        var file1 = Path.Combine(directory, "samples.pngImage_1.docx");
        var file2 = Path.Combine(directory, "samples.pngImage_2.docx");

        using var stream1 = Convert(file1);
        using var stream2 = Convert(file2);

        var bytes1 = stream1.ToArray();
        var bytes2 = stream2.ToArray();

        await Assert.That(bytes1).IsEquivalentTo(bytes2, CollectionOrdering.Matching);
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
    [Arguments(Extension.xlsx)]
    [Arguments(Extension.nupkg)]
    [Arguments(Extension.docx)]
    public Task Run(Extension extension)
    {
        var stream = Convert(extension);

        return VerifyZip(stream);
    }

    [Test]
    [Arguments(Extension.xlsx)]
    [Arguments(Extension.nupkg)]
    [Arguments(Extension.docx)]
    public async Task RunAsync(Extension extension)
    {
        var stream = await ConvertAsync(extension);

        await VerifyZip(stream);
    }

    [Test]
    [Arguments(Extension.xlsx)]
    [Arguments(Extension.nupkg)]
    [Arguments(Extension.docx)]
    public Task RunBinary(Extension extension)
    {
        var stream = Convert(extension);

        return Verify(stream, extension: extension.ToString())
            .UniqueForRuntime();
    }

    [Test]
    [Arguments(Extension.xlsx)]
    [Arguments(Extension.nupkg)]
    [Arguments(Extension.docx)]
    public async Task RunBinaryAsync(Extension extension)
    {
        var stream = await ConvertAsync(extension);

        await Verify(stream, extension: extension.ToString())
            .UniqueForRuntime();
    }

    [Test]
    [Arguments(Extension.xlsx)]
    [Arguments(Extension.nupkg)]
    [Arguments(Extension.docx)]
    public async Task RelationshipIdsAreDeterministic(Extension extension)
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
                await Assert.That(id).StartsWith("DeterministicId").Because($"Entry '{entry.FullName}' has non-deterministic relationship Id '{id}'");
            }
        }
    }

    [Test]
    [Arguments(Extension.xlsx)]
    [Arguments(Extension.nupkg)]
    [Arguments(Extension.docx)]
    public async Task ContentTypesAreSorted(Extension extension)
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
            await Assert.That(elements[i].ToString()).IsEqualTo(sorted[i].ToString()).Because($"[Content_Types].xml element at index {i} is not in sorted order");
        }
    }

    [Test]
    public async Task ValidateDocx()
    {
        var file = Path.Combine(directory, "sample.docx");
        await AssertNoNewValidationErrors(file, () => WordprocessingDocument.Open);
    }

    [Test]
    public async Task ValidateXlsx()
    {
        var file = Path.Combine(directory, "sample.xlsx");
        await AssertNoNewValidationErrors(file, () => SpreadsheetDocument.Open);
    }

    [Test]
    public async Task ValidateNumberingDocx()
    {
        var file = Path.Combine(directory, "samples.numbering1_1.docx");
        await AssertNoNewValidationErrors(file, () => WordprocessingDocument.Open);
    }

    [Test]
    public async Task ValidateAbsPathXlsx()
    {
        var file = Path.Combine(directory, "sample.WithAbsPath.xlsx");
        await AssertNoNewValidationErrors(file, () => SpreadsheetDocument.Open);
    }

    [Test]
    public async Task ValidateWithWorkbookRelsXlsx()
    {
        var file = Path.Combine(directory, "sample.WithWorkbookRels.xlsx");
        await AssertNoNewValidationErrors(file, () => SpreadsheetDocument.Open);
    }

    static async Task AssertNoNewValidationErrors(string file, Func<Func<Stream, bool, OpenXmlPackage>> openFactory)
    {
        var open = openFactory();
        var validator = new OpenXmlValidator();

        // Validate source
        using var sourceStream = File.OpenRead(file);
        using var sourceDoc = open(sourceStream, false);
        var sourceErrors = validator.Validate(sourceDoc)
            .Select(_ => _.Description)
            .ToHashSet();

        // Validate converted
        var convertedStream = Convert(file);
        convertedStream.Position = 0;
        using var convertedDoc = open(convertedStream, false);
        var newErrors = validator.Validate(convertedDoc)
            .Where(_ => !sourceErrors.Contains(_.Description))
            .ToList();

        await Assert.That(newErrors).IsEmpty().Because("Conversion introduced new validation errors: " +
            string.Join(Environment.NewLine, newErrors.Select(_ => $"{_.Description} ({_.Path})")));
    }

    [Test]
    public async Task NupkgSignatureIsRemoved()
    {
        var stream = Convert(Extension.nupkg);
        stream.Position = 0;
        using var archive = new Archive(stream, ZipArchiveMode.Read);
        await Assert.That(archive.GetEntry(".signature.p7s")).IsNull();
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
        #region Convert

        using var sourceStream = File.OpenRead(packagePath);
        var target = DeterministicPackage.Convert(sourceStream);

        #endregion

        return target;
    }

    static MemoryStream ConvertReportingChanges(string packagePath)
    {
        #region ConvertChanges

        using var sourceStream = File.OpenRead(packagePath);
        var target = DeterministicPackage.Convert(sourceStream, out var changes);
        foreach (var change in changes)
        {
            Console.WriteLine($"{change.Kind} {change.Entry}");
        }

        #endregion

        return target;
    }

    static async Task<byte[]> ConvertReportingChangesAsync(string packagePath)
    {
        #region ConvertChangesAsync

        using var sourceStream = File.OpenRead(packagePath);
        using var result = await DeterministicPackage.ConvertWithChangesAsync(sourceStream);
        foreach (var change in result.Changes)
        {
            Console.WriteLine($"{change.Kind} {change.Entry}");
        }

        #endregion

        return result.Stream.ToArray();
    }


    static async Task<MemoryStream> ConvertAsync(Extension extension)
    {
        var packagePath = Path.Combine(directory, $"sample.{extension}");

        #region ConvertAsync

        using var sourceStream = File.OpenRead(packagePath);
        var target = await DeterministicPackage.ConvertAsync(sourceStream);

        #endregion

        return target;
    }
}

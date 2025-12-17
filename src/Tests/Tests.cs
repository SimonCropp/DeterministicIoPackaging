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

        return Verify(stream, extension: "xlsx");
    }

    [Test]
    public Task WithWorkbookRels()
    {
        var file = Path.Combine(directory, "sample.WithWorkbookRels.xlsx");
        var stream = Convert(file);

        return Verify(stream, extension: "xlsx");
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

        return Verify(stream, extension: extension.ToString());
    }

    [Test]
    public async Task RunBinaryAsync([Values] Extension extension)
    {
        var stream = await ConvertAsync(extension);

        await Verify(stream, extension: extension.ToString());
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
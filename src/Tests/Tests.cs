using DeterministicIoPackaging;

[TestFixture]
public class Tests
{
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
        nupkg
    }

    static string directory = AttributeReader.GetProjectDirectory();

    static MemoryStream Convert(Extension extension)
    {
        var packagePath = Path.Combine(directory, $"sample.{extension}");
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
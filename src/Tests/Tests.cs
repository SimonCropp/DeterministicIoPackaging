using DeterministicIoPackaging;

[TestFixture]
public class Tests
{
    [Test]
    public Task Run()
    {
        var directory = AttributeReader.GetProjectDirectory();
        var path = Path.Combine(directory, "sample.zip");
        using var source = File.OpenRead(path);
        var target = new MemoryStream();
        DeterministicPackage.Convert(source, target);
        return VerifyZip(target);
    }
    [Test]
    public async Task RunAsync()
    {
        var directory = AttributeReader.GetProjectDirectory();
        var path = Path.Combine(directory, "sample.zip");
        using var source = File.OpenRead(path);
        var target = new MemoryStream();
        await DeterministicPackage.ConvertAsync(source, target);
        await VerifyZip(target);
    }
}
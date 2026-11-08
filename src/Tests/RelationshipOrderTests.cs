// A content part's r:ids are remapped from its .rels part's renumbering, which used to work only
// when the .rels part happened to sort first. "_" sorts after upper-case letters and digits, so a
// part named word/Footer1.xml was patched before word/_rels/Footer1.xml.rels and kept stale ids.
public class RelationshipOrderTests
{
    const string relationships =
        """
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            <Relationship Id="rId9" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink" Target="https://example.com" TargetMode="External" />
        </Relationships>
        """;

    static XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    [Test]
    public async Task UpperCaseWordPart()
    {
        using var source = BuildPackage(
            ("word/_rels/Footer1.xml.rels", relationships),
            ("word/Footer1.xml", $"""<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="{r}"><w:p><w:hyperlink r:id="rId9" /></w:p></w:ftr>"""));

        using var result = DeterministicPackage.Convert(source);

        await AssertReferenceMatches(result, "word/_rels/Footer1.xml.rels", "word/Footer1.xml");
    }

    [Test]
    public async Task UpperCaseWordPartAsync()
    {
        using var source = BuildPackage(
            ("word/_rels/Footer1.xml.rels", relationships),
            ("word/Footer1.xml", $"""<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="{r}"><w:p><w:hyperlink r:id="rId9" /></w:p></w:ftr>"""));

        using var result = await DeterministicPackage.ConvertAsync(source);

        await AssertReferenceMatches(result, "word/_rels/Footer1.xml.rels", "word/Footer1.xml");
    }

    [Test]
    public async Task DigitLeadingSlidePart()
    {
        using var source = BuildPackage(
            ("ppt/slides/_rels/1slide.xml.rels", relationships),
            ("ppt/slides/1slide.xml", $"""<p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="{r}"><a:hlinkClick r:id="rId9" /></p:sld>"""));

        using var result = DeterministicPackage.Convert(source);

        await AssertReferenceMatches(result, "ppt/slides/_rels/1slide.xml.rels", "ppt/slides/1slide.xml");
    }

    static async Task AssertReferenceMatches(MemoryStream result, string relationshipsName, string partName)
    {
        using var archive = new Archive(result, ZipArchiveMode.Read, leaveOpen: true);
        var id = Load(archive, relationshipsName).Root!.Elements().Single().Attribute("Id")!.Value;
        var reference = Load(archive, partName).Descendants().Attributes(r + "id").Single().Value;

        await Assert.That(reference).IsEqualTo(id);
    }

    static MemoryStream BuildPackage(params (string name, string content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new Archive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    static XDocument Load(Archive archive, string name)
    {
        using var stream = archive.GetEntry(name)!.Open();
        return XDocument.Load(stream);
    }
}

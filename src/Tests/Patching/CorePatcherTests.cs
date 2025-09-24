[TestFixture]
public class CorePatcherTests
{
    [Test]
    public Task Patch()
    {
        var xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <cp:coreProperties
                xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
                xmlns:dc="http://purl.org/dc/elements/1.1/"
                xmlns:dcterms="http://purl.org/dc/terms/"
                xmlns:dcmitype="http://purl.org/dc/dcmitype/"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                <dc:title>The Title</dc:title>
                <dc:subject />
                <cp:keywords />
                <dc:description />
                <cp:lastModifiedBy>Simon Cropp</cp:lastModifiedBy>
                <dc:creator>simon</dc:creator>
                <dcterms:created xsi:type="dcterms:W3CDTF">2020-01-01T00:00:00Z</dcterms:created>
                <dcterms:modified xsi:type="dcterms:W3CDTF">2020-01-01T00:00:00Z</dcterms:modified>
                <cp:category />
            </cp:coreProperties>
            """;

        var document = PatchHelper.Patch<CorePatcher>(xml);
        return Verify(document);
    }
}
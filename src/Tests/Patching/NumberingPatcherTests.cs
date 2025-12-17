[TestFixture]
public class NumberingPatcherTests
{
    [Test]
    public Task Patch()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <w:numbering xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                <w:abstractNum w:abstractNumId="1">
                    <w:nsid w:val="02D7C04B" />
                    <w:multiLevelType w:val="hybridMultilevel" />
                </w:abstractNum>
                <w:abstractNum w:abstractNumId="0">
                    <w:nsid w:val="4CD4DDD8" />
                    <w:multiLevelType w:val="hybridMultilevel" />
                </w:abstractNum>
                <w:num w:numId="1">
                    <w:abstractNumId w:val="0" />
                </w:num>
                <w:num w:numId="2">
                    <w:abstractNumId w:val="1" />
                </w:num>
            </w:numbering>
            """;

        var document = PatchHelper.Patch<NumberingPatcher>(xml);
        return Verify(document);
    }
}

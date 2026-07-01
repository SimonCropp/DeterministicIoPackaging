[TestFixture]
public class RelationshipRenumberTests
{
    [Test]
    public Task RemapsTransitionalAndStrictIds()
    {
        // r: is the Transitional relationships namespace; rs: is the ISO 29500 Strict variant that
        // "Strict Open XML Document" (Word) and Aspose emit. RemapIds must rewrite both so that,
        // after the ids in the .rels are renumbered, no r:id/r:embed/r:link is left dangling.
        var xml =
            """
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                        xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                        xmlns:rs="http://purl.oclc.org/ooxml/officeDocument/relationships">
                <w:body>
                    <w:hyperlink r:id="rId1" />
                    <w:blip r:embed="rId2" />
                    <w:externalLink r:link="rId3" />
                    <w:hyperlink rs:id="rId1" />
                    <w:blip rs:embed="rId2" />
                    <w:externalLink rs:link="rId3" />
                </w:body>
            </w:document>
            """;
        var document = XDocument.Parse(xml);

        var mapping = new Dictionary<string, string>
        {
            ["rId1"] = "DeterministicId1",
            ["rId2"] = "DeterministicId2",
            ["rId3"] = "DeterministicId3",
        };
        RelationshipRenumber.RemapIds(document, mapping);

        return Verify(document);
    }
}

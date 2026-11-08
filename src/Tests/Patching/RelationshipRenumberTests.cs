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

    // SmartArt references its four parts through r:dm/r:lo/r:qs/r:cs, and VML images use r:pict,
    // r:href and o:relid. None of those were remapped, leaving them pointing at replaced ids.
    [Test]
    public async Task RemapsSmartArtAndVmlIds()
    {
        var xml =
            """
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                        xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                        xmlns:dgm="http://schemas.openxmlformats.org/drawingml/2006/diagram"
                        xmlns:v="urn:schemas-microsoft-com:vml"
                        xmlns:o="urn:schemas-microsoft-com:office:office">
                <dgm:relIds r:dm="rId1" r:lo="rId2" r:qs="rId3" r:cs="rId4" />
                <v:imagedata r:pict="rId5" r:href="rId6" o:relid="rId7" o:title="rId1" />
            </w:document>
            """;
        var document = XDocument.Parse(xml);

        var mapping = Enumerable.Range(1, 7).ToDictionary(_ => $"rId{_}", _ => $"DeterministicId{_}");
        RelationshipRenumber.RemapIds(document, mapping);

        XNamespace o = "urn:schemas-microsoft-com:office:office";
        var values = document.Descendants().Attributes()
            .Where(_ => !_.IsNamespaceDeclaration && _.Name != o + "title")
            .Select(_ => _.Value)
            .ToList();
        using (Assert.Multiple())
        {
            await Assert.That(values).IsEquivalentTo(mapping.Values, CollectionOrdering.Matching);
            // Not a relationship id, so left alone even though its value matches one.
            await Assert.That(document.Descendants().Attributes(o + "title").Single().Value).IsEqualTo("rId1");
        }
    }
}

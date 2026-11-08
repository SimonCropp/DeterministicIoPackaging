public class RelationshipPatcherTests
{
    [Test]
    public Task AbsoluteTargetsAreNormalized()
    {
        var xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship
                Id="R1"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"
                Target="/xl/workbook.xml"/>
            </Relationships>
            """;
        var document = XDocument.Parse(xml);
        new RelationshipPatcher().PatchXml(document, "_rels/.rels");
        return Verify(document);
    }

    // An external target is a URI, not a part name, so an absolute one stays as it is.
    [Test]
    public async Task ExternalAbsoluteTargetIsUntouched()
    {
        var xml =
            """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship
                Id="R1"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"
                Target="/word/notes.pdf"
                TargetMode="External"/>
              <Relationship
                Id="R2"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"
                Target="/word/media/image1.png"/>
            </Relationships>
            """;
        var document = XDocument.Parse(xml);
        new DocumentRelationshipPatcher().PatchXml(document, "word/_rels/document.xml.rels");

        var targets = document.Root!.Elements().ToDictionary(_ => (string) _.Attribute("Type")!, _ => (string) _.Attribute("Target")!);
        using (Assert.Multiple())
        {
            await Assert.That(targets["http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"]).IsEqualTo("/word/notes.pdf");
            // An internal absolute target is still made relative.
            await Assert.That(targets["http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"]).IsEqualTo("media/image1.png");
        }
    }

    [Test]
    public Task Patch()
    {
        var xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship
                Id="rId2"
                Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"
                Target="docProps/core.xml"/>
              <Relationship
                Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties"
                Target="docProps/app.xml"/>
              <Relationship
                Id="rId1"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"
                Target="xl/workbook.xml"/>
              <Relationship
                Id="rId4"
                Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"
                Target="package/services/metadata/core-properties/f81c51cec5594be694368ed6b7beba9.psmdcp"/>
            </Relationships>
            """;
        var document = PatchHelper.Patch<RelationshipPatcher>(xml);
        return Verify(document);
    }
}
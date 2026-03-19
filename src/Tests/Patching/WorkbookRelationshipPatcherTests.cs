[TestFixture]
public class WorkbookRelationshipPatcherTests
{
    [Test]
    public Task AbsoluteTargetsAreNormalized()
    {
        var xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="R1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="/xl/worksheets/sheet1.xml"/>
              <Relationship Id="R2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="/xl/styles.xml"/>
            </Relationships>
            """;
        var document = XDocument.Parse(xml);
        new WorkbookRelationshipPatcher().PatchXml(document, "xl/_rels/workbook.xml.rels");
        return Verify(document);
    }

    [Test]
    public Task Patch()
    {
        var xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
              <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """;

        var document = PatchHelper.Patch<WorkbookRelationshipPatcher>(xml);
        return Verify(document);
    }
}
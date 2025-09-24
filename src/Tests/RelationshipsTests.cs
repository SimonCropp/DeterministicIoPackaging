[TestFixture]
public class RelationshipsTests
{
    [Test]
    public Task Run()
    {
        var xml = XDocument.Parse(
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
            """);
        Relationships.PatchRelationships(xml, true);

        return Verify(xml);
    }
    [Test]
    public Task PatchWorkbook()
    {
        var xml = XDocument.Parse(
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
              <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);
        Relationships.PatchWorkbookRelationships(xml);

        return Verify(xml);
    }
}


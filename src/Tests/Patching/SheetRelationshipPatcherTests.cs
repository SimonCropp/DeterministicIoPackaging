[TestFixture]
public class SheetRelationshipPatcherTests
{
    [Test]
    public Task Patch()
    {
        var xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships
              xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship
                Id="rId2"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"
                Target="https://somesite//invitations/24a64e93-7752-4caa-be5d-66b6a0d513e2"
                TargetMode="External" />
              <Relationship
                Id="rId1"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"
                Target="https://somesite//invitations/2be3ce9e-5921-4184-b9c0-435f8bb9805b"
                TargetMode="External" />
            </Relationships>
            """;
        var document = PatchHelper.Patch<SheetRelationshipPatcher>(xml);
        return Verify(document);
    }
}
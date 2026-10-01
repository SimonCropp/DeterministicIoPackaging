public class ProtectionHashesTests
{
    [Test]
    public Task Sheet()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetData />
              <sheetProtection algorithmName="SHA-512" hashValue="3q2+7w==" saltValue="AAECAwQFBgcICQoLDA0ODw==" spinCount="100000" sheet="1" />
              <protectedRanges>
                <protectedRange name="Range1" sqref="A1:B2" algorithmName="SHA-512" hashValue="3q2+7w==" saltValue="AAECAw==" spinCount="100000" />
              </protectedRanges>
            </worksheet>
            """;

        var document = XDocument.Parse(xml);
        ProtectionHashes.Scrub(document);
        return Verify(document);
    }

    [Test]
    public Task Workbook()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <fileSharing userName="user" algorithmName="SHA-512" hashValue="3q2+7w==" saltValue="AAECAw==" spinCount="100000" />
              <workbookProtection workbookAlgorithmName="SHA-512" workbookHashValue="3q2+7w==" workbookSaltValue="AAECAw==" workbookSpinCount="100000" revisionsAlgorithmName="SHA-512" revisionsHashValue="3q2+7w==" revisionsSaltValue="AAECAw==" revisionsSpinCount="100000" lockStructure="1" />
            </workbook>
            """;

        var document = XDocument.Parse(xml);
        ProtectionHashes.Scrub(document);
        return Verify(document);
    }

    [Test]
    public Task WordSettings()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <w:settings xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:writeProtection w:cryptProviderType="rsaAES" w:cryptAlgorithmSid="14" w:cryptSpinCount="100000" w:hash="3q2+7w==" w:salt="AAECAw==" />
              <w:documentProtection w:edit="readOnly" w:enforcement="1" w:algorithmName="SHA-512" w:hashValue="3q2+7w==" w:saltValue="AAECAw==" w:spinCount="100000" />
            </w:settings>
            """;

        var document = XDocument.Parse(xml);
        ProtectionHashes.Scrub(document);
        return Verify(document);
    }

    // hash/salt attributes on elements that are not protection must be left alone.
    [Test]
    public Task LeavesUnrelatedElementsUntouched()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <other hashValue="3q2+7w==" saltValue="AAECAw==" />
              <sheetProtection hashValue="not base64!" saltValue="AAECAw==" />
            </worksheet>
            """;

        var document = XDocument.Parse(xml);
        ProtectionHashes.Scrub(document);
        return Verify(document);
    }

    [Test]
    public void EmptyDocumentIsNoOp()
    {
        var document = new XDocument();
        ProtectionHashes.Scrub(document);
    }
}
